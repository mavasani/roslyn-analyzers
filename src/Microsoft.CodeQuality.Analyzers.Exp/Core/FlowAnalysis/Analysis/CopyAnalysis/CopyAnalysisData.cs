// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.CodeAnalysis.Operations.DataFlow.CopyAnalysis
{
    using CoreCopyAnalysisData = IDictionary<AnalysisEntity, CopyAbstractValue>;

    /// <summary>
    /// Aggregated copy analysis data tracked by <see cref="CopyAnalysis"/>.
    /// Contains the <see cref="CoreCopyAnalysisData"/> and optional <see cref="AnalysisEntityBasedPredicateAnalysisData{CopyAbstractValue}"/>
    /// </summary>
    internal sealed class CopyAnalysisData : AnalysisEntityBasedPredicateAnalysisData<CopyAbstractValue>
    {
        public CopyAnalysisData()
        {
        }

        public CopyAnalysisData(CoreCopyAnalysisData fromData)
            : base(fromData)
        {
        }

        private CopyAnalysisData(CopyAnalysisData fromData)
            : base(fromData)
        {
        }

        private CopyAnalysisData(CopyAnalysisData data1, CopyAnalysisData data2, MapAbstractDomain<AnalysisEntity, CopyAbstractValue> coreDataAnalysisDomain)
            : base(data1, data2, coreDataAnalysisDomain)
        {
        }

        public override AnalysisEntityBasedPredicateAnalysisData<CopyAbstractValue> Clone() => new CopyAnalysisData(this);

        public override int Compare(AnalysisEntityBasedPredicateAnalysisData<CopyAbstractValue> other, MapAbstractDomain<AnalysisEntity, CopyAbstractValue> coreDataAnalysisDomain)
            => BaseCompareHelper(other, coreDataAnalysisDomain);

        public override AnalysisEntityBasedPredicateAnalysisData<CopyAbstractValue> WithMergedData(AnalysisEntityBasedPredicateAnalysisData<CopyAbstractValue> data, MapAbstractDomain<AnalysisEntity, CopyAbstractValue> coreDataAnalysisDomain)
        {
            Debug.Assert(IsReachableBlockData || !data.IsReachableBlockData);
            var mergedData = new CopyAnalysisData(this, (CopyAnalysisData)data, coreDataAnalysisDomain);
            mergedData.AssertValidCopyAnalysisData();
            return mergedData;
        }

        public void SetAbstactValue(AnalysisEntity key, CopyAbstractValue value, bool isEntityBeingAssigned)
        {
            if (HasPredicatedData && isEntityBeingAssigned)
            {
                RemoveEntries(key);
            }

            CoreAnalysisData[key] = value;
        }

        public override void SetAbstactValue(AnalysisEntity key, CopyAbstractValue value)
        {
            throw new NotSupportedException("Use the other overload of SetAbstactValue");
        }

        protected override void RemoveEntryInPredicatedData(AnalysisEntity key, CoreCopyAnalysisData predicatedData)
        {
            Debug.Assert(HasPredicatedData);
            Debug.Assert(predicatedData != null);

            var hasEntry = predicatedData.TryGetValue(key, out CopyAbstractValue value);
            base.RemoveEntryInPredicatedData(key, predicatedData);
            if (hasEntry && value.AnalysisEntities.Count > 1)
            {
                var newValueForOldCopyEntities = value.WithEntityRemoved(key);
                if (newValueForOldCopyEntities.AnalysisEntities.Count == 1)
                {
                    predicatedData.Remove(newValueForOldCopyEntities.AnalysisEntities.Single());
                }
                else
                {
                    foreach (var copyEntity in newValueForOldCopyEntities.AnalysisEntities)
                    {
                        predicatedData[copyEntity] = newValueForOldCopyEntities;
                    }
                }
            }
        }

        protected override void ApplyPredicatedData(CoreCopyAnalysisData coreAnalysisData, CoreCopyAnalysisData predicatedData)
        {
            if (predicatedData.Count == 0)
            {
                return;
            }

#if DEBUG
            var originalCoreAnalysisData = new Dictionary<AnalysisEntity, CopyAbstractValue>(coreAnalysisData);
#endif

            AssertValidCopyAnalysisData(coreAnalysisData);
            AssertValidCopyAnalysisData(predicatedData);

            foreach (var kvp in predicatedData)
            {
                var predicatedValue = kvp.Value;
                if (coreAnalysisData.TryGetValue(kvp.Key, out var currentValue))
                {
                    var newCopyEntities = currentValue.AnalysisEntities;
                    foreach (var predicatedCopyEntity in predicatedValue.AnalysisEntities)
                    {
                        if (!newCopyEntities.Contains(predicatedCopyEntity))
                        {
                            if (coreAnalysisData.TryGetValue(predicatedCopyEntity, out var predicatedCopyEntityValue))
                            {
                                newCopyEntities = newCopyEntities.Union(predicatedCopyEntityValue.AnalysisEntities);
                            }
                            else
                            {
                                newCopyEntities = newCopyEntities.Add(predicatedCopyEntity);
                            }
                        }
                    }

                    if (newCopyEntities.Count != currentValue.AnalysisEntities.Count)
                    {
                        var newCopyValue = new CopyAbstractValue(newCopyEntities);
                        foreach (var copyEntity in newCopyEntities)
                        {
                            coreAnalysisData[copyEntity] = newCopyValue;
                        }
                    }
                    else
                    {
                        Debug.Assert(newCopyEntities.SetEquals(currentValue.AnalysisEntities));
                    }
                }
                else
                {
                    coreAnalysisData[kvp.Key] = kvp.Value;
                }
            }

            Debug.Assert(predicatedData.All(kvp => kvp.Value.AnalysisEntities.IsSubsetOf(coreAnalysisData[kvp.Key].AnalysisEntities)));
            AssertValidCopyAnalysisData(coreAnalysisData);
        }

        public override void Reset(CopyAbstractValue resetValue)
        {
            if (CoreAnalysisData.Count > 0)
            {
                var keys = CoreAnalysisData.Keys.ToImmutableArray();
                foreach (var key in keys)
                {
                    if (CoreAnalysisData[key].AnalysisEntities.Count > 1)
                    {
                        CoreAnalysisData[key] = new CopyAbstractValue(key);
                    }
                }
            }

            ResetPredicatedData();

            this.AssertValidCopyAnalysisData();
        }

        [Conditional("DEBUG")]
        public void AssertValidCopyAnalysisData()
        {
            AssertValidCopyAnalysisData(CoreAnalysisData);
            AssertValidPredicatedAnalysisData(map => AssertValidCopyAnalysisData(map));
        }

        [Conditional("DEBUG")]
        public static void AssertValidCopyAnalysisData(CoreCopyAnalysisData map)
        {
            foreach (var kvp in map)
            {
                AssertValidCopyAnalysisEntity(kvp.Key);
                Debug.Assert(kvp.Value.AnalysisEntities.Contains(kvp.Key));
                foreach (var analysisEntity in kvp.Value.AnalysisEntities)
                {
                    AssertValidCopyAnalysisEntity(analysisEntity);
                    Debug.Assert(map[analysisEntity] == kvp.Value);
                }
            }
        }

        [Conditional("DEBUG")]
        private static void AssertValidCopyAnalysisEntity(AnalysisEntity analysisEntity)
        {
            Debug.Assert(!analysisEntity.HasUnknownInstanceLocation, "Don't track entities if do not know about it's instance location");
        }
    }
}
