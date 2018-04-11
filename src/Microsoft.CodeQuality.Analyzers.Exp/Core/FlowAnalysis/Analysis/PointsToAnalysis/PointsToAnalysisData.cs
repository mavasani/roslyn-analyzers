// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.CodeAnalysis.Operations.DataFlow.PointsToAnalysis
{
    using CorePointsToAnalysisData = IDictionary<AnalysisEntity, PointsToAbstractValue>;

    internal sealed class PointsToAnalysisData : AnalysisEntityBasedPredicateAnalysisData<PointsToAbstractValue>
    {
        public PointsToAnalysisData()
        {
        }

        public PointsToAnalysisData(CorePointsToAnalysisData fromData)
            : base(fromData)
        {
        }

        private PointsToAnalysisData(PointsToAnalysisData fromData)
            : base(fromData)
        {
        }

        private PointsToAnalysisData(PointsToAnalysisData data1, PointsToAnalysisData data2, MapAbstractDomain<AnalysisEntity, PointsToAbstractValue> coreDataAnalysisDomain)
            : base(data1, data2, coreDataAnalysisDomain)
        {
        }

        public PointsToAnalysisData(
            CorePointsToAnalysisData mergedCoreAnalysisData,
            PredicatedAnalysisData<AnalysisEntity, PointsToAbstractValue> predicatedData1,
            PredicatedAnalysisData<AnalysisEntity, PointsToAbstractValue> predicatedData2,
            MapAbstractDomain<AnalysisEntity, PointsToAbstractValue> coreDataAnalysisDomain)
            : base(mergedCoreAnalysisData, predicatedData1, predicatedData2, coreDataAnalysisDomain)
        {
        }

        public override AnalysisEntityBasedPredicateAnalysisData<PointsToAbstractValue> Clone() => new PointsToAnalysisData(this);

        public override int Compare(AnalysisEntityBasedPredicateAnalysisData<PointsToAbstractValue> other, MapAbstractDomain<AnalysisEntity, PointsToAbstractValue> coreDataAnalysisDomain)
            => BaseCompareHelper(other, coreDataAnalysisDomain);

        public override AnalysisEntityBasedPredicateAnalysisData<PointsToAbstractValue> WithMergedData(AnalysisEntityBasedPredicateAnalysisData<PointsToAbstractValue> data, MapAbstractDomain<AnalysisEntity, PointsToAbstractValue> coreDataAnalysisDomain)
            => new PointsToAnalysisData(this, (PointsToAnalysisData)data, coreDataAnalysisDomain);
    }
}
