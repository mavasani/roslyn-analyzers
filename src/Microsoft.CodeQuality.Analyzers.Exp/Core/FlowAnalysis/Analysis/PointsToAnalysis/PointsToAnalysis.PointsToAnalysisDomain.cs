// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.CodeAnalysis.Operations.DataFlow.PointsToAnalysis
{
    using PointsToAnalysisData = IDictionary<SymbolWithInstance, PointsToAbstractValue>;

    internal partial class PointsToAnalysis : ForwardDataFlowAnalysis<PointsToAnalysisData, PointsToBlockAnalysisResult, PointsToAbstractValue>
    {
        private sealed class PointsToAnalysisDomain : AbstractDomain<PointsToAnalysisData>
        {
            public static readonly PointsToAnalysisDomain Instance = new PointsToAnalysisDomain();

            private readonly MapAbstractDomain<SymbolWithInstance, PointsToAbstractValue> _valuesDomain;

            private PointsToAnalysisDomain()
            {
                _valuesDomain = new MapAbstractDomain<SymbolWithInstance, PointsToAbstractValue>(PointsToAbstractValueDomain.Default);
                }

            public override PointsToAnalysisData Bottom => PointsToAnalysisData.Empty;

            public override int Compare(PointsToAnalysisData oldValue, PointsToAnalysisData newValue)
            {
                if (oldValue == null && newValue != null)
                {
                    return -1;
                }

                if (oldValue != null && newValue == null)
                {
                    return 1;
                }

                if (oldValue == null || ReferenceEquals(oldValue, newValue))
                {
                    return 0;
                }

                int result1 = _valuesDomain.Compare(oldValue.Values, newValue.Values);
                int result2 = _trackedIndexersDomain.Compare(oldValue.Indexers, newValue.Indexers);
                if (result1 > 0 || result2 > 0)
                {
                    return 1;
                }

                if (result1 < 0 || result2 < 0)
                {
                    return -1;
                }

                return 0;
            }

            public override PointsToAnalysisData Merge(PointsToAnalysisData value1, PointsToAnalysisData value2)
            {
                if (value1 == null && value2 != null)
                {
                    return value2;
                }

                if (value1 != null && value2 == null)
                {
                    return value1;
                }

                if (value1 == null)
                {
                    return null;
                }

                var mergedValues = _valuesDomain.Merge(value1.Values, value2.Values);
                var mergedTrackedIndexers = _trackedIndexersDomain.Merge(value1.Indexers, value2.Indexers);
                return PointsToAnalysisData.Create(mergedValues, mergedTrackedIndexers);
            }
        }
    }
}
