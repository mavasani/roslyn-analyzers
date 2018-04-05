// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.CodeAnalysis.Operations.DataFlow.PointsToAnalysis
{
    using PointsToAnalysisData = IDictionary<AnalysisEntity, PointsToAbstractValue>;

    /// <summary>
    /// An abstract analysis domain implementation <see cref="PointsToAnalysis"/>.
    /// </summary>
    internal class PointsToAnalysisDomain: AnalysisEntityMapAbstractDomain<PointsToAbstractValue>
    {
        public PointsToAnalysisDomain(DefaultPointsToValueGenerator defaultPointsToValueGenerator, AbstractValueDomain<PointsToAbstractValue> valueDomain)
            : base(valueDomain)
        {
            DefaultPointsToValueGenerator = defaultPointsToValueGenerator;
        }

        public DefaultPointsToValueGenerator DefaultPointsToValueGenerator { get; }

        protected override PointsToAbstractValue GetDefaultValue(AnalysisEntity analysisEntity) => DefaultPointsToValueGenerator.GetOrCreateDefaultValue(analysisEntity);

        public PointsToAnalysisData MergeAnalysisDataForBackEdge(PointsToAnalysisData forwardEdgeAnalysisData, PointsToAnalysisData backEdgeAnalysisData, Func<PointsToAbstractValue, IEnumerable<AnalysisEntity>> getChildAnalysisEntities)
        {
            // Stop tracking points to values present in both branches if their is an assignment to a may-be null value from the back edge.
            // Clone the input forwardEdgeAnalysisData to ensure we don't overwrite the input dictionary.
            forwardEdgeAnalysisData = new Dictionary<AnalysisEntity, PointsToAbstractValue>(forwardEdgeAnalysisData);
            List<AnalysisEntity> keysInMap1 = forwardEdgeAnalysisData.Keys.ToList();
            foreach (var key in keysInMap1)
            {
                var forwardEdgeValue = forwardEdgeAnalysisData[key];
                if (backEdgeAnalysisData.TryGetValue(key, out var backEdgeValue) &&
                    backEdgeValue != forwardEdgeValue)
                {
                    switch (backEdgeValue.NullState)
                    {
                        case NullAnalysis.NullAbstractValue.MaybeNull:
                            StopTrackingAnalysisDataForKey();
                            break;

                        case NullAnalysis.NullAbstractValue.NotNull:
                            if (backEdgeValue.MakeMayBeNull() != forwardEdgeAnalysisData[key])
                            {
                                StopTrackingAnalysisDataForKey();
                            }
                            break;

                    }

                    void StopTrackingAnalysisDataForKey()
                    {
                        var childEntities = getChildAnalysisEntities(forwardEdgeValue)
                            .Union(getChildAnalysisEntities(backEdgeValue));
                        foreach (var childEntity in childEntities)
                        {
                            forwardEdgeAnalysisData[childEntity] = PointsToAbstractValue.Unknown;
                        }

                        forwardEdgeAnalysisData[key] = PointsToAbstractValue.Unknown;
                    }
                }
            }

            return Merge(forwardEdgeAnalysisData, backEdgeAnalysisData);
        }
    }
}