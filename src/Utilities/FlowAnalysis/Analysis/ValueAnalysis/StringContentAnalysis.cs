// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.ValueContentAnalysis
{
    using ValueContentAnalysisDomain = PredicatedAnalysisDataDomain<ValueContentAnalysisData, IAbstractValue>;

    /// <summary>
    /// Dataflow analysis to track string content of <see cref="AnalysisEntity"/>/<see cref="IOperation"/>.
    /// </summary>
    internal partial class ValueContentAnalysis : ForwardDataFlowAnalysis<ValueContentAnalysisData, ValueContentBlockAnalysisResult, IAbstractValue>
    {
        private static readonly ValueContentAnalysisDomain s_AnalysisDomain = new ValueContentAnalysisDomain(CoreAnalysisDataDomain.Instance);

        private ValueContentAnalysis(StringContentDataFlowOperationVisitor operationVisitor)
            : base(s_AnalysisDomain, operationVisitor)
        {
        }

        public static DataFlowAnalysisResult<ValueContentBlockAnalysisResult, IAbstractValue> GetOrComputeResult(
            ControlFlowGraph cfg,
            ISymbol owningSymbol,
            WellKnownTypeProvider wellKnownTypeProvider,
            DataFlowAnalysisResult<CopyAnalysis.CopyBlockAnalysisResult, CopyAnalysis.CopyAbstractValue> copyAnalysisResultOpt,
            DataFlowAnalysisResult<PointsToAnalysis.PointsToBlockAnalysisResult, PointsToAnalysis.PointsToAbstractValue> pointsToAnalysisResultOpt = null,
            bool pessimisticAnalsysis = true)
        {
            var operationVisitor = new StringContentDataFlowOperationVisitor(IAbstractValueDomain.Default, owningSymbol,
                wellKnownTypeProvider, cfg, pessimisticAnalsysis, copyAnalysisResultOpt, pointsToAnalysisResultOpt);
            var nullAnalysis = new ValueContentAnalysis(operationVisitor);
            return nullAnalysis.GetOrComputeResultCore(cfg, cacheResult: false);
        }

        internal override ValueContentBlockAnalysisResult ToResult(BasicBlock basicBlock, DataFlowAnalysisInfo<ValueContentAnalysisData> blockAnalysisData) => new ValueContentBlockAnalysisResult(basicBlock, blockAnalysisData);
    }
}
