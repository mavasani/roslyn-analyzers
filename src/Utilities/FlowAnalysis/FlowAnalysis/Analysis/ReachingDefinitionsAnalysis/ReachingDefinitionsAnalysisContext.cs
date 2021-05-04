// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.CopyAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.PointsToAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.ValueContentAnalysis;

namespace Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.ReachingDefinitionsAnalysis
{
    using CopyAnalysisResult = DataFlowAnalysisResult<CopyBlockAnalysisResult, CopyAbstractValue>;
    using ValueContentAnalysisResult = DataFlowAnalysisResult<ValueContentBlockAnalysisResult, ValueContentAbstractValue>;
    using ReachingDefinitionsAnalysisData = DictionaryAnalysisData<AnalysisEntity, ReachingDefinitionsAbstractValue>;
    using InterproceduralReachingDefinitionsAnalysisData = InterproceduralAnalysisData<DictionaryAnalysisData<AnalysisEntity, ReachingDefinitionsAbstractValue>, ReachingDefinitionsAnalysisContext, ReachingDefinitionsAbstractValue>;
    using ReachingDefinitionsAnalysisResult = DataFlowAnalysisResult<ReachingDefinitionsBlockAnalysisResult, ReachingDefinitionsAbstractValue>;

    /// <summary>
    /// Analysis context for execution of <see cref="ReachingDefinitionsAnalysis"/> on a control flow graph.
    /// </summary>
    public sealed class ReachingDefinitionsAnalysisContext : AbstractDataFlowAnalysisContext<ReachingDefinitionsAnalysisData, ReachingDefinitionsAnalysisContext, ReachingDefinitionsAnalysisResult, ReachingDefinitionsAbstractValue>
    {
        private ReachingDefinitionsAnalysisContext(
            AbstractValueDomain<ReachingDefinitionsAbstractValue> valueDomain,
            WellKnownTypeProvider wellKnownTypeProvider,
            ControlFlowGraph controlFlowGraph,
            ISymbol owningSymbol,
            AnalyzerOptions analyzerOptions,
            InterproceduralAnalysisConfiguration interproceduralAnalysisConfig,
            bool pessimisticAnalysis,
            Func<ReachingDefinitionsAnalysisContext, ReachingDefinitionsAnalysisResult?> tryGetOrComputeAnalysisResult,
            ControlFlowGraph? parentControlFlowGraph,
            InterproceduralReachingDefinitionsAnalysisData? interproceduralAnalysisData,
            InterproceduralAnalysisPredicate? interproceduralAnalysisPredicate)
            : base(valueDomain, wellKnownTypeProvider, controlFlowGraph, owningSymbol, analyzerOptions, interproceduralAnalysisConfig,
                  pessimisticAnalysis, predicateAnalysis: false, exceptionPathsAnalysis: false, copyAnalysisResult: null,
                  pointsToAnalysisResult: null, valueContentAnalysisResult: null, tryGetOrComputeAnalysisResult, parentControlFlowGraph,
                  interproceduralAnalysisData, interproceduralAnalysisPredicate)
        {
        }

        internal static ReachingDefinitionsAnalysisContext Create(
            AbstractValueDomain<ReachingDefinitionsAbstractValue> valueDomain,
            WellKnownTypeProvider wellKnownTypeProvider,
            ControlFlowGraph controlFlowGraph,
            ISymbol owningSymbol,
            AnalyzerOptions analyzerOptions,
            InterproceduralAnalysisConfiguration interproceduralAnalysisConfig,
            bool pessimisticAnalysis,
            Func<ReachingDefinitionsAnalysisContext, ReachingDefinitionsAnalysisResult?> tryGetOrComputeAnalysisResult,
            InterproceduralAnalysisPredicate? interproceduralAnalysisPredicate)
        {
            return new ReachingDefinitionsAnalysisContext(
                valueDomain, wellKnownTypeProvider, controlFlowGraph, owningSymbol, analyzerOptions,
                interproceduralAnalysisConfig, pessimisticAnalysis,
                tryGetOrComputeAnalysisResult, parentControlFlowGraph: null, interproceduralAnalysisData: null, interproceduralAnalysisPredicate);
        }

        public override ReachingDefinitionsAnalysisContext ForkForInterproceduralAnalysis(
            IMethodSymbol invokedMethod,
            ControlFlowGraph invokedCfg,
            PointsToAnalysisResult? pointsToAnalysisResult,
            CopyAnalysisResult? copyAnalysisResult,
            ValueContentAnalysisResult? valueContentAnalysisResult,
            InterproceduralReachingDefinitionsAnalysisData? interproceduralAnalysisData)
        {
            return new ReachingDefinitionsAnalysisContext(ValueDomain, WellKnownTypeProvider, invokedCfg, invokedMethod, AnalyzerOptions, InterproceduralAnalysisConfiguration,
                PessimisticAnalysis, TryGetOrComputeAnalysisResult, ControlFlowGraph, interproceduralAnalysisData, InterproceduralAnalysisPredicate);
        }

        protected override void ComputeHashCodePartsSpecific(ref RoslynHashCode hashCode)
        {
        }

        protected override bool ComputeEqualsByHashCodeParts(AbstractDataFlowAnalysisContext<ReachingDefinitionsAnalysisData, ReachingDefinitionsAnalysisContext, ReachingDefinitionsAnalysisResult, ReachingDefinitionsAbstractValue> obj)
        {
            return true;
        }
    }
}
