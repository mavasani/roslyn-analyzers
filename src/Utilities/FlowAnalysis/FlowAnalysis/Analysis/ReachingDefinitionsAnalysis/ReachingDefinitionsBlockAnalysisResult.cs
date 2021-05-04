// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;

namespace Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.ReachingDefinitionsAnalysis
{
    using ReachingDefinitionsAnalysisData = DictionaryAnalysisData<AnalysisEntity, ReachingDefinitionsAbstractValue>;

    /// <summary>
    /// Result from execution of <see cref="ReachingDefinitionsAnalysis"/> on a basic block.
    /// It stores data values for each <see cref="AnalysisEntity"/> at the start and end of the basic block.
    /// </summary>
    public class ReachingDefinitionsBlockAnalysisResult : AbstractBlockAnalysisResult
    {
        internal ReachingDefinitionsBlockAnalysisResult(BasicBlock basicBlock, ReachingDefinitionsAnalysisData blockAnalysisData)
            : base(basicBlock)
        {
            Data = blockAnalysisData?.ToImmutableDictionary() ?? ImmutableDictionary<AnalysisEntity, ReachingDefinitionsAbstractValue>.Empty;
        }

        public ImmutableDictionary<AnalysisEntity, ReachingDefinitionsAbstractValue> Data { get; }
    }
}
