// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.ValueContentAnalysis
{
    using CoreValueContentAnalysisData = IDictionary<AnalysisEntity, StringContentAbstractValue>;

    /// <summary>
    /// Aggregated string content analysis data tracked by <see cref="ValueContentAnalysis"/>.
    /// Contains the <see cref="CoreValueContentAnalysisData"/> for entity string content values and
    /// the predicated values based on true/false runtime values of predicated entities.
    /// </summary>
    /// <summary>
    internal sealed class ValueContentAnalysisData : AnalysisEntityBasedPredicateAnalysisData<StringContentAbstractValue>
    {
        public ValueContentAnalysisData()
        {
        }

        private ValueContentAnalysisData(ValueContentAnalysisData fromData)
            : base(fromData)
        {
        }

        private ValueContentAnalysisData(ValueContentAnalysisData data1, ValueContentAnalysisData data2, MapAbstractDomain<AnalysisEntity, StringContentAbstractValue> coreDataAnalysisDomain)
            : base(data1, data2, coreDataAnalysisDomain)
        {
        }

        public override AnalysisEntityBasedPredicateAnalysisData<StringContentAbstractValue> Clone() => new ValueContentAnalysisData(this);

        public override int Compare(AnalysisEntityBasedPredicateAnalysisData<StringContentAbstractValue> other, MapAbstractDomain<AnalysisEntity, StringContentAbstractValue> coreDataAnalysisDomain)
            => BaseCompareHelper(other, coreDataAnalysisDomain);

        public override AnalysisEntityBasedPredicateAnalysisData<StringContentAbstractValue> WithMergedData(AnalysisEntityBasedPredicateAnalysisData<StringContentAbstractValue> data, MapAbstractDomain<AnalysisEntity, StringContentAbstractValue> coreDataAnalysisDomain)
            => new ValueContentAnalysisData(this, (ValueContentAnalysisData)data, coreDataAnalysisDomain);
    }
}
