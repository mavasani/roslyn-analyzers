// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Threading;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.ReachingDefinitionsAnalysis
{
    using ReachingDefinitionsAnalysisData = DictionaryAnalysisData<AnalysisEntity, ReachingDefinitionsAbstractValue>;
    using ReachingDefinitionsAnalysisDomain = MapAbstractDomain<AnalysisEntity, ReachingDefinitionsAbstractValue>;
    using ReachingDefinitionsAnalysisResult = DataFlowAnalysisResult<ReachingDefinitionsBlockAnalysisResult, ReachingDefinitionsAbstractValue>;

    /// <summary>
    /// Dataflow analysis to track value content of <see cref="AnalysisEntity"/>/<see cref="IOperation"/>.
    /// </summary>
    public partial class ReachingDefinitionsAnalysis : ForwardDataFlowAnalysis<ReachingDefinitionsAnalysisData, ReachingDefinitionsAnalysisContext, ReachingDefinitionsAnalysisResult, ReachingDefinitionsBlockAnalysisResult, ReachingDefinitionsAbstractValue>
    {
        public static readonly ReachingDefinitionsAnalysisDomain ReachingDefinitionsAnalysisDomainInstance = new(ReachingDefinitionsAbstractValueDomain.Default);

        private ReachingDefinitionsAnalysis(ReachingDefinitionsAnalysisDomain analysisDomain, ReachingDefinitionsDataFlowOperationVisitor operationVisitor)
            : base(analysisDomain, operationVisitor)
        {
        }

        public static ReachingDefinitionsAnalysisResult? TryGetOrComputeResult(
            ControlFlowGraph cfg,
            ISymbol owningSymbol,
            WellKnownTypeProvider wellKnownTypeProvider,
            AnalyzerOptions analyzerOptions,
            DiagnosticDescriptor rule,
            CancellationToken cancellationToken,
            InterproceduralAnalysisKind interproceduralAnalysisKind = InterproceduralAnalysisKind.None,
            bool pessimisticAnalysis = true,
            InterproceduralAnalysisPredicate? interproceduralAnalysisPredicate = null)
        {
            if (cfg == null)
            {
                throw new ArgumentNullException(nameof(cfg));
            }

            var interproceduralAnalysisConfig = InterproceduralAnalysisConfiguration.Create(
                analyzerOptions, rule, cfg, wellKnownTypeProvider.Compilation, interproceduralAnalysisKind, cancellationToken);
            return TryGetOrComputeResult(cfg, owningSymbol, wellKnownTypeProvider, analyzerOptions,
                interproceduralAnalysisConfig, pessimisticAnalysis, interproceduralAnalysisPredicate);
        }

        public static ReachingDefinitionsAnalysisResult? TryGetOrComputeResult(
            ControlFlowGraph cfg,
            ISymbol owningSymbol,
            WellKnownTypeProvider wellKnownTypeProvider,
            AnalyzerOptions analyzerOptions,
            InterproceduralAnalysisConfiguration interproceduralAnalysisConfig,
            bool pessimisticAnalysis = true,
            InterproceduralAnalysisPredicate? interproceduralAnalysisPredicate = null)
        {
            if (cfg == null)
            {
                throw new ArgumentNullException(nameof(cfg));
            }

            var analysisContext = ReachingDefinitionsAnalysisContext.Create(
                ReachingDefinitionsAbstractValueDomain.Default, wellKnownTypeProvider, cfg, owningSymbol, analyzerOptions,
                interproceduralAnalysisConfig, pessimisticAnalysis, TryGetOrComputeResultForAnalysisContext,
                interproceduralAnalysisPredicate);
            return TryGetOrComputeResultForAnalysisContext(analysisContext);
        }

        private static ReachingDefinitionsAnalysisResult? TryGetOrComputeResultForAnalysisContext(ReachingDefinitionsAnalysisContext analysisContext)
        {
            var operationVisitor = new ReachingDefinitionsDataFlowOperationVisitor(analysisContext);
            var analysis = new ReachingDefinitionsAnalysis(ReachingDefinitionsAnalysisDomainInstance, operationVisitor);
            return analysis.TryGetOrComputeResultCore(analysisContext, cacheResult: false);
        }

        protected override ReachingDefinitionsAnalysisResult ToResult(ReachingDefinitionsAnalysisContext analysisContext, ReachingDefinitionsAnalysisResult dataFlowAnalysisResult)
            => dataFlowAnalysisResult;

        protected override ReachingDefinitionsBlockAnalysisResult ToBlockResult(BasicBlock basicBlock, ReachingDefinitionsAnalysisData blockAnalysisData)
            => new(basicBlock, blockAnalysisData);
    }
}
