// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.FlightEnabledAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.ValueContentAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeQuality.Analyzers.Maintainability
{
    /// <summary>
    /// CA1508: Flags conditional expressions which are always true/false and null checks for operations that are always null/non-null based on predicate analysis.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class FlightEnabledAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1510";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftMaintainabilityAnalyzersResources.FlightEnabledTitle), MicrosoftMaintainabilityAnalyzersResources.ResourceManager, typeof(MicrosoftMaintainabilityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftMaintainabilityAnalyzersResources.FlightEnabledMessage), MicrosoftMaintainabilityAnalyzersResources.ResourceManager, typeof(MicrosoftMaintainabilityAnalyzersResources));

        internal static DiagnosticDescriptor Rule = new DiagnosticDescriptor(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessage,
                                                                             DiagnosticCategory.Maintainability,
                                                                             DiagnosticHelpers.DefaultDiagnosticSeverity,
                                                                             isEnabledByDefault: true,
                                                                             helpLinkUri: null, // TODO: Add helplink
                                                                             customTags: WellKnownDiagnosticTagsExtensions.DataflowAndTelemetry);
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(compilationContext =>
            {
                compilationContext.RegisterOperationBlockAction(operationBlockContext =>
                {
                    var owningSymbol = operationBlockContext.OwningSymbol;

                    if (owningSymbol is IMethodSymbol method &&
                        FlightEnabledAnalysis.IsFlightEnablingMethod(method))
                    {
                        return;
                    }

                    foreach (var operationRoot in operationBlockContext.OperationBlocks)
                    {
                        if (operationRoot.Kind == OperationKind.ParameterInitializer)
                        {
                            continue;
                        }

                        var cfg = operationBlockContext.GetControlFlowGraph(operationRoot);
                        var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(operationBlockContext.Compilation);
                        var flightEnabledResult = FlightEnabledAnalysis.GetOrComputeResult(cfg, owningSymbol, wellKnownTypeProvider,
                            operationBlockContext.Options, Rule, operationBlockContext.CancellationToken, out _);

                        // Method '{0}', enable flights at invocations and property accesses: '{1}'
                        var arg1 = owningSymbol.ToString();
                        var enabledFlights = flightEnabledResult.EnabledFlightsForInvocationsAndPropertyAccessesOpt ?? ImmutableHashSet<string>.Empty;
                        var arg2 = string.Join(", ", enabledFlights.Order());
                        operationBlockContext.ReportDiagnostic(owningSymbol.CreateDiagnostic(Rule, arg1, arg2));
                    }
                });
            });
        }
    }
}
