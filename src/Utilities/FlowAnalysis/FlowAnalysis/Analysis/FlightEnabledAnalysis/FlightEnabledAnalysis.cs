// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.PointsToAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.FlightEnabledAnalysis
{
    using FlightEnabledAnalysisDomain = MapAbstractDomain<AnalysisEntity, FlightEnabledAbstractValue>;
    using FlightEnabledAnalysisData = DictionaryAnalysisData<AnalysisEntity, FlightEnabledAbstractValue>;

    /// <summary>
    /// Dataflow analysis to track FlightEnabled state.
    /// </summary>
    internal partial class FlightEnabledAnalysis : ForwardDataFlowAnalysis<FlightEnabledAnalysisData, FlightEnabledAnalysisContext, FlightEnabledAnalysisResult, FlightEnabledBlockAnalysisResult, FlightEnabledAbstractValue>
    {
        internal static readonly FlightEnabledAnalysisDomain FlightEnabledAnalysisDomainInstance = new FlightEnabledAnalysisDomain(FlightEnabledAbstractValueDomain.Instance);

        private FlightEnabledAnalysis(FlightEnabledAnalysisDomain analysisDomain, FlightEnabledDataFlowOperationVisitor operationVisitor)
            : base(analysisDomain, operationVisitor)
        {
        }

        public static FlightEnabledAnalysisResult GetOrComputeResult(
            ControlFlowGraph cfg,
            ISymbol owningSymbol,
            WellKnownTypeProvider wellKnownTypeProvider,
            AnalyzerOptions analyzerOptions,
            DiagnosticDescriptor rule,
            CancellationToken cancellationToken,
            out PointsToAnalysisResult pointsToAnalysisResult,
            InterproceduralAnalysisKind interproceduralAnalysisKind = InterproceduralAnalysisKind.ContextSensitive,
            bool pessimisticAnalysis = true,
            bool performCopyAnalysisIfNotUserConfigured = false,
            InterproceduralAnalysisPredicate interproceduralAnalysisPredicateOpt = null)
        {
            var interproceduralAnalysisConfig = InterproceduralAnalysisConfiguration.Create(
                analyzerOptions, rule, interproceduralAnalysisKind, cancellationToken);
            interproceduralAnalysisPredicateOpt ??= new InterproceduralAnalysisPredicate(
                skipAnalysisForInvokedMethodPredicateOpt: IsFlightEnablingMethod,
                skipAnalysisForInvokedLambdaOrLocalFunctionPredicateOpt: null,
                skipAnalysisForInvokedContextPredicateOpt: null);
            return GetOrComputeResult(cfg, owningSymbol, wellKnownTypeProvider,
                interproceduralAnalysisConfig, interproceduralAnalysisPredicateOpt, pessimisticAnalysis,
                performCopyAnalysis: analyzerOptions.GetCopyAnalysisOption(rule, defaultValue: performCopyAnalysisIfNotUserConfigured, cancellationToken),
                out pointsToAnalysisResult);
        }

        private static FlightEnabledAnalysisResult GetOrComputeResult(
            ControlFlowGraph cfg,
            ISymbol owningSymbol,
            WellKnownTypeProvider wellKnownTypeProvider,
            InterproceduralAnalysisConfiguration interproceduralAnalysisConfig,
            InterproceduralAnalysisPredicate interproceduralAnalysisPredicateOpt,
            bool pessimisticAnalysis,
            bool performCopyAnalysis,
            out PointsToAnalysisResult pointsToAnalysisResult)
        {
            Debug.Assert(cfg != null);
            Debug.Assert(wellKnownTypeProvider.IDisposable != null);
            Debug.Assert(owningSymbol != null);

            pointsToAnalysisResult = PointsToAnalysis.PointsToAnalysis.GetOrComputeResult(
                cfg, owningSymbol, wellKnownTypeProvider, interproceduralAnalysisConfig,
                interproceduralAnalysisPredicateOpt, pessimisticAnalysis, performCopyAnalysis);
            var valueContentAnalysisResult = ValueContentAnalysis.ValueContentAnalysis.GetOrComputeResult(
                cfg, owningSymbol, wellKnownTypeProvider, interproceduralAnalysisConfig, out _,
                out pointsToAnalysisResult, pessimisticAnalysis, performPointsToAnalysis: true, performCopyAnalysis, interproceduralAnalysisPredicateOpt);
            var analysisContext = FlightEnabledAnalysisContext.Create(
                FlightEnabledAbstractValueDomain.Instance, wellKnownTypeProvider, cfg, owningSymbol, interproceduralAnalysisConfig, interproceduralAnalysisPredicateOpt,
                pessimisticAnalysis, pointsToAnalysisResult, valueContentAnalysisResult, GetOrComputeResultForAnalysisContext);
            return GetOrComputeResultForAnalysisContext(analysisContext);
        }

        private static FlightEnabledAnalysisResult GetOrComputeResultForAnalysisContext(FlightEnabledAnalysisContext FlightEnabledAnalysisContext)
        {
            var operationVisitor = new FlightEnabledDataFlowOperationVisitor(FlightEnabledAnalysisContext);
            var FlightEnabledAnalysis = new FlightEnabledAnalysis(FlightEnabledAnalysisDomainInstance, operationVisitor);
            return FlightEnabledAnalysis.GetOrComputeResultCore(FlightEnabledAnalysisContext, cacheResult: false);
        }

        protected override FlightEnabledAnalysisResult ToResult(FlightEnabledAnalysisContext analysisContext, DataFlowAnalysisResult<FlightEnabledBlockAnalysisResult, FlightEnabledAbstractValue> dataFlowAnalysisResult)
            => new FlightEnabledAnalysisResult(dataFlowAnalysisResult, ((FlightEnabledDataFlowOperationVisitor)OperationVisitor).EnabledFlightsForInvocationsAndPropertyAccessesOpt);

        protected override FlightEnabledBlockAnalysisResult ToBlockResult(BasicBlock basicBlock, FlightEnabledAnalysisData data)
            => new FlightEnabledBlockAnalysisResult(basicBlock, data);

        public static bool IsFlightEnablingMethod(IMethodSymbol method)
            => method.Name.Equals("IsFlightEnabled", StringComparison.Ordinal) &&
                method.ContainingType.Name.Equals("FlightApi", StringComparison.Ordinal) &&
                method.ReturnType.SpecialType == SpecialType.System_Boolean &&
                method.Parameters.Length == 1 &&
                method.Parameters[0].Type.SpecialType == SpecialType.System_String;
    }
}
