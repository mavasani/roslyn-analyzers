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

        public PointsToAnalysisData MergeAnalysisDataForBackEdge(PointsToAnalysisData map1, PointsToAnalysisData map2, Func<PointsToAbstractValue, IEnumerable<AnalysisEntity>> getChildAnalysisEntities)
        {
            // Ensure we don't overrwrite the input map.
            map1 = new Dictionary<AnalysisEntity, PointsToAbstractValue>(map1);

            // Stop tracking points to values present in both branches.
            List<AnalysisEntity> keysInMap1 = map1.Keys.ToList();
            foreach (var key in keysInMap1)
            {
                if (map2.TryGetValue(key, out var value2) &&
                    value2 != map1[key])
                {
                    foreach (var childEntity in getChildAnalysisEntities(map1[key]))
                    {
                        map1[childEntity] = PointsToAbstractValue.Unknown;
                    }

                    foreach (var childEntity in getChildAnalysisEntities(value2))
                    {
                        map1[childEntity] = PointsToAbstractValue.Unknown;
                    }

                    map1[key] = PointsToAbstractValue.Unknown;
                }
            }

            return Merge(map1, map2);
        }
    }
}