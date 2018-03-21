// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Operations.ControlFlow;
using Microsoft.CodeAnalysis.Operations.DataFlow;
using Microsoft.CodeAnalysis.Operations.DataFlow.CopyAnalysis;
using Microsoft.CodeAnalysis.Operations.DataFlow.NullAnalysis;
using Microsoft.CodeAnalysis.Operations.DataFlow.PointsToAnalysis;
using Microsoft.CodeAnalysis.Operations.DataFlow.StringContentAnalysis;

namespace Microsoft.CodeQuality.Analyzers.Exp.Globalization
{
    /// <summary>
    /// CA1303: A method passes a string literal as a parameter to a constructor or method in the .NET Framework class library and that string should be localizable.
    /// This warning is raised when a literal string is passed as a value to a parameter or property and one or more of the following cases is true:
    ///   1. The LocalizableAttribute attribute of the parameter or property is set to true.
    ///   2. The parameter or property name contains "Text", "Message", or "Caption".
    ///   3. The name of the string parameter that is passed to a Console.Write or Console.WriteLine method is either "value" or "format".
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotPassLiteralsAsLocalizedParameters : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1303";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftGlobalizationAnalyzersResources.DoNotPassLiteralsAsLocalizedParametersTitle), MicrosoftGlobalizationAnalyzersResources.ResourceManager, typeof(MicrosoftGlobalizationAnalyzersResources));
        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftGlobalizationAnalyzersResources.DoNotPassLiteralsAsLocalizedParametersMessage), MicrosoftGlobalizationAnalyzersResources.ResourceManager, typeof(MicrosoftGlobalizationAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftGlobalizationAnalyzersResources.DoNotPassLiteralsAsLocalizedParametersDescription), MicrosoftGlobalizationAnalyzersResources.ResourceManager, typeof(MicrosoftGlobalizationAnalyzersResources));

        internal static DiagnosticDescriptor Rule = new DiagnosticDescriptor(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessage,
                                                                             DiagnosticCategory.Globalization,
                                                                             DiagnosticHelpers.DefaultDiagnosticSeverity,
                                                                             isEnabledByDefault: DiagnosticHelpers.EnabledByDefaultIfNotBuildingVSIX,
                                                                             description: s_localizableDescription,
                                                                             helpLinkUri: "https://docs.microsoft.com/en-us/visualstudio/code-quality/ca1303-do-not-pass-literals-as-localized-parameters",
                                                                             customTags: WellKnownDiagnosticTags.Telemetry);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(compilationContext =>
            {
                INamedTypeSymbol localizableStateAttributeSymbol = WellKnownTypes.LocalizableAttribute(compilationContext.Compilation);
                INamedTypeSymbol conditionalAttributeSymbol = WellKnownTypes.ConditionalAttribute(compilationContext.Compilation);
                INamedTypeSymbol systemConsoleSymbol = WellKnownTypes.Console(compilationContext.Compilation);
                ImmutableHashSet<INamedTypeSymbol> typesToIgnore = GetTypesToIgnore(compilationContext.Compilation);

                compilationContext.RegisterOperationBlockStartAction(operationBlockStartContext =>
                {
                    if (!(operationBlockStartContext.OwningSymbol is IMethodSymbol containingMethod))
                    {
                        return;
                    }

                    foreach (var operationRoot in operationBlockStartContext.OperationBlocks)
                    {
                        IBlockOperation topmostBlock = operationRoot.GetTopmostParentBlock();
                        if (topmostBlock != null && topmostBlock.HasAnyOperationDescendant(op => (op as IBinaryOperation)?.IsComparisonOperator() == true || op.Kind == OperationKind.Coalesce || op.Kind == OperationKind.ConditionalAccess))
                        {
                            var cfg = ControlFlowGraph.Create(topmostBlock);
                            var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(operationBlockStartContext.Compilation);
                            var nullAnalysisResult = NullAnalysis.GetOrComputeResult(cfg, containingMethod, wellKnownTypeProvider);
                            var pointsToAnalysisResult = PointsToAnalysis.GetOrComputeResult(cfg, containingMethod, wellKnownTypeProvider, nullAnalysisResult);
                            var copyAnalysisResult = CopyAnalysis.GetOrComputeResult(cfg, containingMethod, wellKnownTypeProvider, nullAnalysisResultOpt: nullAnalysisResult, pointsToAnalysisResultOpt: pointsToAnalysisResult);
                            // Do another null analysis pass to improve the results from PointsTo and Copy analysis.
                            nullAnalysisResult = NullAnalysis.GetOrComputeResult(cfg, containingMethod, wellKnownTypeProvider, copyAnalysisResult, pointsToAnalysisResultOpt: pointsToAnalysisResult);
                            var stringContentAnalysisResult = StringContentAnalysis.GetOrComputeResult(cfg, containingMethod, wellKnownTypeProvider, copyAnalysisResult, nullAnalysisResult, pointsToAnalysisResult);

                            operationBlockStartContext.RegisterOperationAction(operationContext =>
                            {
                                PredicateValueKind GetPredicateKind(IBinaryOperation operation)
                                {
                                    if (operation.IsComparisonOperator())
                                    {
                                        PredicateValueKind binaryPredicateKind = nullAnalysisResult.GetPredicateKind(operation);
                                        if (binaryPredicateKind != PredicateValueKind.Unknown)
                                        {
                                            return binaryPredicateKind;
                                        }

                                        binaryPredicateKind = copyAnalysisResult.GetPredicateKind(operation);
                                        if (binaryPredicateKind != PredicateValueKind.Unknown)
                                        {
                                            return binaryPredicateKind;
                                        }

                                        binaryPredicateKind = stringContentAnalysisResult.GetPredicateKind(operation);
                                        if (binaryPredicateKind != PredicateValueKind.Unknown)
                                        {
                                            return binaryPredicateKind;
                                        };
                                    }

                                    return PredicateValueKind.Unknown;
                                }

                                var binaryOperation = (IBinaryOperation)operationContext.Operation;
                                PredicateValueKind predicateKind = GetPredicateKind(binaryOperation);
                                if (predicateKind != PredicateValueKind.Unknown &&
                                    (!(binaryOperation.LeftOperand is IBinaryOperation leftBinary) || GetPredicateKind(leftBinary) == PredicateValueKind.Unknown) &&
                                    (!(binaryOperation.RightOperand is IBinaryOperation rightBinary) || GetPredicateKind(rightBinary) == PredicateValueKind.Unknown))
                                {
                                    // '{0}' is always '{1}'. Remove or refactor the condition(s) to avoid dead code.
                                    var arg1 = binaryOperation.Syntax.ToString();
                                    var arg2 = predicateKind == PredicateValueKind.AlwaysTrue ? 
                                        (binaryOperation.Language == LanguageNames.VisualBasic ? "True" : "true") :
                                        (binaryOperation.Language == LanguageNames.VisualBasic ? "False" : "false");
                                    var diagnostic = binaryOperation.CreateDiagnostic(Rule, arg1, arg2);
                                    operationContext.ReportDiagnostic(diagnostic);
                                }
                            }, OperationKind.BinaryOperator);

                            operationBlockStartContext.RegisterOperationAction(operationContext =>
                            {
                                IOperation nullCheckedOperation = operationContext.Operation.Kind == OperationKind.Coalesce ?
                                    ((ICoalesceOperation)operationContext.Operation).Value :
                                    ((IConditionalAccessOperation)operationContext.Operation).Operation;

                                // '{0}' is always/never '{1}'. Remove or refactor the condition(s) to avoid dead code.
                                DiagnosticDescriptor rule;
                                switch (nullAnalysisResult[nullCheckedOperation])
                                {
                                    case NullAbstractValue.Null:
                                        rule = Rule;
                                        break;

                                    case NullAbstractValue.NotNull:
                                        rule = NeverNullRule;
                                        break;

                                    default:
                                        return;
                                }

                                var arg1 = nullCheckedOperation.Syntax.ToString();
                                var arg2 = nullCheckedOperation.Language == LanguageNames.VisualBasic ? "Nothing" : "null";
                                var diagnostic = nullCheckedOperation.CreateDiagnostic(rule, arg1, arg2);
                                operationContext.ReportDiagnostic(diagnostic);
                            }, OperationKind.Coalesce, OperationKind.ConditionalAccess);
                        }
                    }
                });

            });
        }

        private static ImmutableHashSet<INamedTypeSymbol> GetTypesToIgnore(Compilation compilation)
        {
            var builder = ImmutableHashSet.CreateBuilder<INamedTypeSymbol>();

            var xmlWriter = WellKnownTypes.XmlWriter(compilation);
            if (xmlWriter != null)
            {
                builder.Add(xmlWriter);
            }

            var webUILiteralControl = WellKnownTypes.WebUILiteralControl(compilation);
            if (webUILiteralControl != null)
            {
                builder.Add(webUILiteralControl);
            }

            var xunitAssert = WellKnownTypes.XunitAssert(compilation);
            if (xunitAssert != null)
            {
                builder.Add(xunitAssert);
            }

            var nunitAssert = WellKnownTypes.NunitAssert(compilation);
            if (nunitAssert != null)
            {
                builder.Add(nunitAssert);
            }

            var unitTestingAssert = WellKnownTypes.UnitTestingAssert(compilation);
            if (unitTestingAssert != null)
            {
                builder.Add(unitTestingAssert);
            }

            var unitTestingCollectionAssert = WellKnownTypes.UnitTestingCollectionAssert(compilation);
            if (unitTestingCollectionAssert != null)
            {
                builder.Add(unitTestingCollectionAssert);
            }

            var unitTestingCollectionStringAssert = WellKnownTypes.UnitTestingCollectionStringAssert(compilation);
            if (unitTestingCollectionStringAssert != null)
            {
                builder.Add(unitTestingCollectionStringAssert);
            }

            return builder.ToImmutable();
        }

        private static bool ShouldBeLocalized(
            IParameterSymbol parameterSymbol,
            INamedTypeSymbol localizableStateAttributeSymbol,
            INamedTypeSymbol conditionalAttributeSymbol,
            INamedTypeSymbol systemConsoleSymbol,
            ImmutableHashSet<INamedTypeSymbol> typesToIgnore)
        {
            Debug.Assert(parameterSymbol.ContainingSymbol.Kind == SymbolKind.Method);

            if (parameterSymbol.Type.SpecialType != SpecialType.System_String)
            {
                return false;
            }

            LocalizableAttributeState localizableAttributeState = GetLocalizableAttributeState(parameterSymbol, localizableStateAttributeSymbol);
            switch (localizableAttributeState)
            {
                case LocalizableAttributeState.False:
                    return false;

                case LocalizableAttributeState.True:
                    return true;

                default:
                    break;
            }

            if (IsNameHeuristicException(parameterSymbol, typesToIgnore, conditionalAttributeSymbol))
            {
                return false;
            }

            var method = (IMethodSymbol)parameterSymbol.ContainingSymbol;
            if (method.IsOverride &&
                method.OverriddenMethod.Parameters.Length == method.Parameters.Length)
            {
                int parameterIndex = method.GetParameterIndex(parameterSymbol);
                IParameterSymbol overridenParameter = method.OverriddenMethod.Parameters[parameterIndex];
                if (overridenParameter.Type == parameterSymbol.Type &&
                    !ShouldBeLocalized(overridenParameter, localizableStateAttributeSymbol, conditionalAttributeSymbol, typesToIgnore))
                {
                    return false;
                }
            }

            bool IsLocalizableSymbolName(ISymbol symbol) =>
                symbol.Name.Equals("message", StringComparison.OrdinalIgnoreCase) ||
                symbol.Name.Equals("text", StringComparison.OrdinalIgnoreCase) ||
                symbol.Name.Equals("caption", StringComparison.OrdinalIgnoreCase);

            // If a localizable attribute isn't defined then fall back to name heuristics
            if (IsLocalizableSymbolName(parameterSymbol))
            {
                return true;
            }
            else if (parameterSymbol.Name.Equals("value", StringComparison.OrdinalIgnoreCase) &&
                method.AssociatedSymbol?.Kind == SymbolKind.Property)
            {
                if (IsLocalizableSymbolName(method.AssociatedSymbol))
                {
                    return true;
                }
            }

            if (s_consoleWrite.MatchesFunctionSymbol(functionSymbol) ||
                s_consoleWriteLine.MatchesFunctionSymbol(functionSymbol))
            {
                if (nameString.Equals("format", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (nameString.Equals("value", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static LocalizableAttributeState GetLocalizableAttributeState(ISymbol symbol, INamedTypeSymbol localizableAttributeTypeSymbol)
        {
            Debug.Assert(localizableAttributeTypeSymbol != null);
            if (symbol == null)
            {
                return LocalizableAttributeState.Undefined;
            }

            LocalizableAttributeState localizedState = GetLocalizableAttributeStateCore(symbol.GetAttributes(), localizableAttributeTypeSymbol);
            if (localizedState != LocalizableAttributeState.Undefined)
            {
                return localizedState;
            }

            return GetLocalizableAttributeState(symbol.ContainingSymbol, localizableAttributeTypeSymbol);
        }

        private static LocalizableAttributeState GetLocalizableAttributeStateCore(ImmutableArray<AttributeData> attributeList, INamedTypeSymbol localizableAttributeTypeSymbol)
        {
            Debug.Assert(localizableAttributeTypeSymbol != null);

            var localizableAttribute = attributeList.FirstOrDefault(attr => localizableAttributeTypeSymbol.Equals(attr.AttributeClass));
            if (localizableAttribute != null &&
                localizableAttribute.AttributeConstructor.Parameters.Length == 1 &&
                localizableAttribute.AttributeConstructor.Parameters[0].Type.SpecialType == SpecialType.System_Boolean &&
                localizableAttribute.ConstructorArguments.Length == 1 &&
                localizableAttribute.ConstructorArguments[0].Kind == TypedConstantKind.Primitive &&
                localizableAttribute.ConstructorArguments[0].Value is bool isLocalizable)
            {
                return isLocalizable ? LocalizableAttributeState.True : LocalizableAttributeState.False;
            }

            return LocalizableAttributeState.Undefined;
        }

        private static bool IsNameHeuristicException(IParameterSymbol parameterSymbol, ImmutableHashSet<INamedTypeSymbol> typesToIgnore, INamedTypeSymbol conditionalAttributeSymbol)
        {
            if (typesToIgnore.Contains(parameterSymbol.ContainingType) ||
                parameterSymbol.ContainingSymbol.GetAttributes().Any(n => n.AttributeClass.Equals(conditionalAttributeSymbol)))
            {
                return true;
            }

            if (

            AggregateType enclosingType = functionSymbol.EnclosingAggregateType;
            if (enclosingType != null && s_typeHeuristicExceptions[enclosingType])
            {
                return true;
            }

            // if we have a conditional attribute applied the method
            // it is not likely to be a candidate for localization
            if (SymbolExtensions.HasAttribute(functionSymbol, s_conditionalAttribute))
            {
                return true;
            }

            return false;
        }
    }
}
