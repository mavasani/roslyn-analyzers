// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.PointsToAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.FlightEnabledAnalysis
{
    using FlightEnabledAnalysisData = DictionaryAnalysisData<AnalysisEntity, FlightEnabledAbstractValue>;

    internal partial class FlightEnabledAnalysis : ForwardDataFlowAnalysis<FlightEnabledAnalysisData, FlightEnabledAnalysisContext, FlightEnabledAnalysisResult, FlightEnabledBlockAnalysisResult, FlightEnabledAbstractValue>
    {
        /// <summary>
        /// Operation visitor to flow the FlightEnabled values across a given statement in a basic block.
        /// </summary>
        private sealed class FlightEnabledDataFlowOperationVisitor : AnalysisEntityDataFlowOperationVisitor<FlightEnabledAnalysisData, FlightEnabledAnalysisContext, FlightEnabledAnalysisResult, FlightEnabledAbstractValue>
        {
            private readonly AnalysisEntity _globalEntity;
            private HashSet<string> _lazyEnabledFlightsForInvocationsAndPropertyAccesses;

            public FlightEnabledDataFlowOperationVisitor(FlightEnabledAnalysisContext analysisContext)
                : base(analysisContext)
            {
                _globalEntity = GetGlobalEntity(analysisContext);
            }

            public ImmutableHashSet<string> EnabledFlightsForInvocationsAndPropertyAccesses
                => _lazyEnabledFlightsForInvocationsAndPropertyAccesses?.ToImmutableHashSet() ?? ImmutableHashSet<string>.Empty;

            private void UpdateEnabledFlightsOnInvocationOrPropertyAccess(ImmutableHashSet<string> enabledFlights = null)
            {
                enabledFlights ??= GetAbstractValue(_globalEntity).EnabledFlights;
                if (_lazyEnabledFlightsForInvocationsAndPropertyAccesses == null)
                {
                    _lazyEnabledFlightsForInvocationsAndPropertyAccesses = new HashSet<string>(enabledFlights, StringComparer.OrdinalIgnoreCase);
                }
                else
                {
                    _lazyEnabledFlightsForInvocationsAndPropertyAccesses.IntersectWith(enabledFlights);
                }
            }

            private static AnalysisEntity GetGlobalEntity(FlightEnabledAnalysisContext analysisContext)
            {
                ISymbol owningSymbol;
                if (analysisContext.InterproceduralAnalysisDataOpt == null)
                {
                    owningSymbol = analysisContext.OwningSymbol;
                }
                else
                {
                    owningSymbol = analysisContext.InterproceduralAnalysisDataOpt.MethodsBeingAnalyzed
                        .Single(m => m.InterproceduralAnalysisDataOpt == null)
                        .OwningSymbol;
                }

                return AnalysisEntity.Create(
                    owningSymbol,
                    ImmutableArray<AbstractIndex>.Empty,
                    owningSymbol.GetMemerOrLocalOrParameterType(),
                    instanceLocation: PointsToAbstractValue.Unknown,
                    parentOpt: null);
            }

            public override FlightEnabledAnalysisData Flow(IOperation statement, BasicBlock block, FlightEnabledAnalysisData input)
            {
                if (block.Kind == BasicBlockKind.Entry &&
                    input != null &&
                    !input.ContainsKey(_globalEntity))
                {
                    input[_globalEntity] = ValueDomain.Bottom;
                }

                return base.Flow(statement, block, input);
            }

            public override (FlightEnabledAnalysisData output, bool isFeasibleBranch) FlowBranch(BasicBlock fromBlock, BranchWithInfo branch, FlightEnabledAnalysisData input)
            {
                var result = base.FlowBranch(fromBlock, branch, input);

                if (result.isFeasibleBranch &&
                    branch.BranchValueOpt != null &&
                    FlowBranchConditionKind != ControlFlowConditionKind.None)
                {
                    var branchValue = GetCachedAbstractValue(branch.BranchValueOpt);
                    if (branchValue.EnabledFlights.Count > 0)
                    {
                        var currentGlobalValue = GetAbstractValue(_globalEntity);
                        var newEnabledFlights = branchValue.EnabledFlights
                            .Select(enabledFlight => FlowBranchConditionKind == ControlFlowConditionKind.WhenTrue ?
                                                     enabledFlight.ToLower() :
                                                     enabledFlight.ToUpper());
                        var newGlobalValue = new FlightEnabledAbstractValue(currentGlobalValue.EnabledFlights.Union(newEnabledFlights));
                        SetAbstractValue(_globalEntity, newGlobalValue);
                    }
                }

                return result;
            }

            protected override void AddTrackedEntities(FlightEnabledAnalysisData analysisData, PooledHashSet<AnalysisEntity> builder, bool forInterproceduralAnalysis)
                => builder.UnionWith(analysisData.Keys);

            protected override void ResetAbstractValue(AnalysisEntity analysisEntity)
                => SetAbstractValue(analysisEntity, ValueDomain.UnknownOrMayBeValue);

            protected override void StopTrackingEntity(AnalysisEntity analysisEntity, FlightEnabledAnalysisData analysisData)
                => analysisData.Remove(analysisEntity);

            protected override FlightEnabledAbstractValue GetAbstractValue(AnalysisEntity analysisEntity)
                => CurrentAnalysisData.TryGetValue(analysisEntity, out var value) ? value : ValueDomain.UnknownOrMayBeValue;

            protected override FlightEnabledAbstractValue GetAbstractDefaultValue(ITypeSymbol type)
                => FlightEnabledAbstractValue.Empty;

            protected override bool HasAbstractValue(AnalysisEntity analysisEntity)
                => CurrentAnalysisData.ContainsKey(analysisEntity);

            protected override bool HasAnyAbstractValue(FlightEnabledAnalysisData data)
                => data.Count > 0;

            protected override void SetAbstractValue(AnalysisEntity analysisEntity, FlightEnabledAbstractValue value)
                => SetAbstractValue(CurrentAnalysisData, analysisEntity, value);

            private static void SetAbstractValue(FlightEnabledAnalysisData analysisData, AnalysisEntity analysisEntity, FlightEnabledAbstractValue value)
            {
                // PERF: Avoid creating an entry if the value is the default unknown value.
                if (value.Kind != FlightEnabledAbstractValueKind.Known &&
                    !analysisData.ContainsKey(analysisEntity))
                {
                    return;
                }

                analysisData[analysisEntity] = value;
            }

            protected override void ResetCurrentAnalysisData()
                => ResetAnalysisData(CurrentAnalysisData);

            protected override FlightEnabledAnalysisData MergeAnalysisData(FlightEnabledAnalysisData value1, FlightEnabledAnalysisData value2)
                => FlightEnabledAnalysisDomainInstance.Merge(value1, value2);
            protected override void UpdateValuesForAnalysisData(FlightEnabledAnalysisData targetAnalysisData)
                => UpdateValuesForAnalysisData(targetAnalysisData, CurrentAnalysisData);
            protected override FlightEnabledAnalysisData GetClonedAnalysisData(FlightEnabledAnalysisData analysisData)
                => new FlightEnabledAnalysisData(analysisData);
            public override FlightEnabledAnalysisData GetEmptyAnalysisData()
                => new FlightEnabledAnalysisData();
            protected override FlightEnabledAnalysisData GetExitBlockOutputData(FlightEnabledAnalysisResult analysisResult)
                => new FlightEnabledAnalysisData(analysisResult.ExitBlockOutput.Data);
            protected override void ApplyMissingCurrentAnalysisDataForUnhandledExceptionData(FlightEnabledAnalysisData dataAtException, ThrownExceptionInfo throwBranchWithExceptionType)
                => ApplyMissingCurrentAnalysisDataForUnhandledExceptionData(dataAtException, CurrentAnalysisData, throwBranchWithExceptionType);
            protected override bool Equals(FlightEnabledAnalysisData value1, FlightEnabledAnalysisData value2)
                => FlightEnabledAnalysisDomainInstance.Equals(value1, value2);
            protected override void ApplyInterproceduralAnalysisResultCore(FlightEnabledAnalysisData resultData)
                => ApplyInterproceduralAnalysisResultHelper(resultData);
            protected override FlightEnabledAnalysisData GetTrimmedCurrentAnalysisData(IEnumerable<AnalysisEntity> withEntities)
                => GetTrimmedCurrentAnalysisDataHelper(withEntities, CurrentAnalysisData, SetAbstractValue);

            #region Visitor methods

            public override FlightEnabledAbstractValue VisitInvocation_NonLambdaOrDelegateOrLocalFunction(IMethodSymbol method, IOperation visitedInstance, ImmutableArray<IArgumentOperation> visitedArguments, bool invokedAsDelegate, IOperation originalOperation, FlightEnabledAbstractValue defaultValue)
            {
                bool isFlightEnablingInvocation =
                    method.Name.Equals("IsFlightEnabled", StringComparison.Ordinal) &&
                    method.ContainingType.Name.Equals("FlightApi", StringComparison.Ordinal) &&
                    method.ReturnType.SpecialType == SpecialType.System_Boolean &&
                    method.Parameters.Length == 1 &&
                    method.Parameters[0].Type.SpecialType == SpecialType.System_String &&
                    visitedArguments.Length == 1;

                if (!isFlightEnablingInvocation)
                {
                    UpdateEnabledFlightsOnInvocationOrPropertyAccess();
                }

                var value = base.VisitInvocation_NonLambdaOrDelegateOrLocalFunction(method, visitedInstance, visitedArguments, invokedAsDelegate, originalOperation, defaultValue);

                if (TryGetInterproceduralAnalysisResult(originalOperation, out var interproceduralAnalysisResult))
                {
                    UpdateEnabledFlightsOnInvocationOrPropertyAccess(interproceduralAnalysisResult.EnabledFlightsForInvocationsAndPropertyAccesses);
                }

                if (isFlightEnablingInvocation)
                {
                    var argumentValue = DataFlowAnalysisContext.ValueContentAnalysisResult[visitedArguments[0]];
                    if (argumentValue.IsLiteralState &&
                        argumentValue.LiteralValues.Count == 1 &&
                        argumentValue.LiteralValues.Single() is string enabledFlight)
                    {
                        value = new FlightEnabledAbstractValue(enabledFlight);
                    }
                    else
                    {
                        value = FlightEnabledAbstractValue.Unknown;
                    }
                }

                return value;
            }

            public override FlightEnabledAbstractValue VisitPropertyReference(IPropertyReferenceOperation operation, object argument)
            {
                var value = base.VisitPropertyReference(operation, argument);
                UpdateEnabledFlightsOnInvocationOrPropertyAccess();
                return value;
            }

            #endregion
        }
    }
}
