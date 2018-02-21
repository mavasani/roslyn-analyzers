// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.CodeAnalysis.Operations.DataFlow.StringContentAnalysis
{
    using StringContentAnalysisData = IDictionary<AnalysisEntity, StringContentAbstractValue>;

    internal partial class StringContentAnalysis : ForwardDataFlowAnalysis<StringContentAnalysisData, StringContentBlockAnalysisResult, StringContentAbstractValue>
    {
        /// <summary>
        /// Operation visitor to flow the string content values across a given statement in a basic block.
        /// </summary>
        private sealed class StringContentDataFlowOperationVisitor : AnalysisEntityDataFlowOperationVisitor<StringContentAnalysisData, StringContentAbstractValue>
        {
            public StringContentDataFlowOperationVisitor(
                StringContentAbstractValueDomain valueDomain,
                INamedTypeSymbol containingTypeSymbol,
                DataFlowAnalysisResult<NullAnalysis.NullBlockAnalysisResult, NullAnalysis.NullAbstractValue> nullAnalysisResultOpt,
                DataFlowAnalysisResult<PointsToAnalysis.PointsToBlockAnalysisResult, PointsToAnalysis.PointsToAbstractValue> pointsToAnalysisResultOpt)
                : base(valueDomain, containingTypeSymbol, nullAnalysisResultOpt, pointsToAnalysisResultOpt)
            {
            }

            protected override IEnumerable<AnalysisEntity> TrackedEntities => CurrentAnalysisData.Keys;

            protected override void SetAbstractValue(AnalysisEntity analysisEntity, StringContentAbstractValue value) => CurrentAnalysisData[analysisEntity] = value;

            protected override bool HasAbstractValue(AnalysisEntity analysisEntity) => CurrentAnalysisData.ContainsKey(analysisEntity);

            protected override StringContentAbstractValue GetAbstractValue(AnalysisEntity analysisEntity) => CurrentAnalysisData.TryGetValue(analysisEntity, out var value) ? value : ValueDomain.UnknownOrMayBeValue;

            protected override StringContentAbstractValue GetAbstractDefaultValue(ITypeSymbol type) => StringContentAbstractValue.DoesNotContainNonLiteralState;

            protected override void ResetCurrentAnalysisData(StringContentAnalysisData newAnalysisDataOpt = null) => ResetAnalysisData(CurrentAnalysisData, newAnalysisDataOpt);

            // TODO: Remove these temporary methods once we move to compiler's CFG
            // https://github.com/dotnet/roslyn-analyzers/issues/1567
            #region Temporary methods to workaround lack of *real* CFG
            protected override StringContentAnalysisData MergeAnalysisData(StringContentAnalysisData value1, StringContentAnalysisData value2)
                => StringContentAnalysisDomainInstance.Merge(value1, value2);
            protected override StringContentAnalysisData GetClonedAnalysisData()
                => GetClonedAnalysisData(CurrentAnalysisData);
            protected override bool Equals(StringContentAnalysisData value1, StringContentAnalysisData value2)
                => EqualsHelper(value1, value2);
            #endregion

            #region Visitor methods
            public override StringContentAbstractValue DefaultVisit(IOperation operation, object argument)
            {
                var _ = base.DefaultVisit(operation, argument);
                if (operation.Type == null)
                {
                    return StringContentAbstractValue.DoesNotContainNonLiteralState;
                }

                if (operation.Type.SpecialType == SpecialType.System_String)
                {
                    if (operation.ConstantValue.HasValue && operation.ConstantValue.Value is string value)
                    {
                        return StringContentAbstractValue.Create(value);
                    }
                    else
                    {
                        return StringContentAbstractValue.ContainsNonLiteralState;
                    }
                }

                return ValueDomain.UnknownOrMayBeValue;
            }

            public override StringContentAbstractValue VisitBinaryOperator(IBinaryOperation operation, object argument)
            {
                switch (operation.OperatorKind)
                {
                    case BinaryOperatorKind.Add:
                    case BinaryOperatorKind.Concatenate:
                        var leftValue = Visit(operation.LeftOperand, argument);
                        var rightValue = Visit(operation.RightOperand, argument);
                        return leftValue.MergeBinaryAdd(rightValue);

                    default:
                        return base.VisitBinaryOperator(operation, argument);
                }
            }

            public override StringContentAbstractValue VisitCompoundAssignment(ICompoundAssignmentOperation operation, object argument)
            {
                StringContentAbstractValue value;
                switch (operation.OperatorKind)
                {
                    case BinaryOperatorKind.Add:
                    case BinaryOperatorKind.Concatenate:
                        var leftValue = Visit(operation.Target, argument);
                        var rightValue = Visit(operation.Value, argument);
                        value = leftValue.MergeBinaryAdd(rightValue);
                        break;

                    default:
                        value = base.VisitCompoundAssignment(operation, argument);
                        break;
                }

                SetAbstractValueForAssignment(operation.Target, operation.Value, value);
                return value;
            }

            public override StringContentAbstractValue VisitNameOf(INameOfOperation operation, object argument)
            {
                var nameofValue = base.VisitNameOf(operation, argument);
                if (operation.ConstantValue.HasValue && operation.ConstantValue.Value is string value)
                {
                    return StringContentAbstractValue.Create(value);
                }

                return nameofValue;
            }

            public override StringContentAbstractValue VisitObjectCreation(IObjectCreationOperation operation, object argument)
            {
                // TODO: Analyze string constructor
                // https://github.com/dotnet/roslyn-analyzers/issues/1547
                return base.VisitObjectCreation(operation, argument);
            }

            public override StringContentAbstractValue VisitFieldReference(IFieldReferenceOperation operation, object argument)
            {
                var value = base.VisitFieldReference(operation, argument);

                // Handle "string.Empty"
                if (operation.Field.Name.Equals("Empty", StringComparison.Ordinal) &&
                    operation.Field.ContainingType.SpecialType == SpecialType.System_String)
                {
                    return StringContentAbstractValue.Create(string.Empty);
                }

                return value;
            }

            public override StringContentAbstractValue VisitInvocation_NonLambdaOrDelegateOrLocalFunction(IInvocationOperation operation, object argument)
            {
                // TODO: Handle invocations of string methods (Format, SubString, Replace, Concat, etc.)
                // https://github.com/dotnet/roslyn-analyzers/issues/1547
                var value = base.VisitInvocation_NonLambdaOrDelegateOrLocalFunction(operation, argument);
                if (operation.TargetMethod.ContainingType.SpecialType == SpecialType.System_String)
                {
                    if (operation.Instance != null)
                    {
                        // Instance methods on string type.

                        // 1. "string.Clone": Returns a reference to this instance of String.
                        // See https://docs.microsoft.com/en-us/dotnet/api/system.string.clone
                        if (operation.TargetMethod.Name.Equals("Clone", StringComparison.Ordinal) &&
                            operation.TargetMethod.MethodKind == MethodKind.Ordinary &&
                            operation.TargetMethod.Parameters.IsEmpty &&
                            operation.TargetMethod.ReturnType.SpecialType == SpecialType.System_Object)
                        {
                            return GetCachedAbstractValue(operation.Instance);
                        }


                    }
                    else if (operation.TargetMethod.IsStatic)
                    {
                        // Static method methods on string type

                        // 1. "string.Concat(values)": Concatenates the members of a constructed IEnumerable<T> collection of type String.
                        // See https://docs.microsoft.com/en-us/dotnet/api/system.string.concat
                        if (operation.TargetMethod.Name.Equals("Concat", StringComparison.Ordinal) &&
                            operation.TargetMethod.MethodKind == MethodKind.Ordinary &&
                            operation.TargetMethod.Parameters.Length > 0 &&
                            operation.TargetMethod.ReturnType.SpecialType == SpecialType.System_String)
                        {
                            if (operation.TargetMethod.Parameters.Length > 1)
                            {
                                Debug.Assert(!operation.Arguments.IsEmpty);

                                StringContentAbstractValue mergedValue = GetCachedAbstractValue(operation.Arguments[0]);
                                for (int i = 1; i < operation.Arguments.Length; i++)
                                {
                                    var newValue = GetCachedAbstractValue(operation.Arguments[i]);
                                    mergedValue = mergedValue.MergeBinaryAdd(newValue);
                                }
                            }
                            else
                            {
                                var singleParameter = operation.TargetMethod.Parameters[0];
                                if (singleParameter.Type.TypeKind == TypeKind.Array)
                                {
                                }
                            }

                            return mergedValue;
                        }
                    }
                }

                return ;
            }

            public override StringContentAbstractValue VisitInterpolatedString(IInterpolatedStringOperation operation, object argument)
            {
                if (operation.Parts.IsEmpty)
                {
                    return StringContentAbstractValue.Create(string.Empty);
                }

                StringContentAbstractValue mergedValue = Visit(operation.Parts[0], argument);
                for (int i = 1; i < operation.Parts.Length; i++)
                {
                    var newValue = Visit(operation.Parts[i], argument);
                    mergedValue = mergedValue.MergeBinaryAdd(newValue);
                }

                return mergedValue;
            }

            #endregion
        }
    }
}
