// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.CodeAnalysis.Operations.DataFlow
{
    internal abstract partial class PredicatedAnalysisData<TKey, TValue>
    {
        private IDictionary<AnalysisEntity, (IDictionary<TKey, TValue> truePredicatedData, IDictionary<TKey, TValue> falsePredicatedData)> _lazyPredicatedData;
        
        protected PredicatedAnalysisData()
        {
            IsReachableBlockData = true;
        }

        protected PredicatedAnalysisData(PredicatedAnalysisData<TKey, TValue> fromData)
        {
            IsReachableBlockData = fromData.IsReachableBlockData;
            _lazyPredicatedData = Clone(fromData);
        }

        protected PredicatedAnalysisData(
            PredicatedAnalysisData<TKey, TValue> predicatedData1,
            PredicatedAnalysisData<TKey, TValue> predicatedData2,
            IDictionary<TKey, TValue> coreAnalysisData1,
            IDictionary<TKey, TValue> coreAnalysisData2,
            MapAbstractDomain<TKey, TValue> coreDataAnalysisDomain)
        {
            Debug.Assert(predicatedData1.IsReachableBlockData == predicatedData2.IsReachableBlockData);
            IsReachableBlockData = predicatedData1.IsReachableBlockData;

            _lazyPredicatedData = Merge(predicatedData1, predicatedData2,
                coreAnalysisData1, coreAnalysisData2, coreDataAnalysisDomain, ApplyPredicatedData);
        }

        public bool IsReachableBlockData { get; set; }
        public bool HasPredicatedData => _lazyPredicatedData != null;

        private void EnsurePredicatedData()
        {
            _lazyPredicatedData = _lazyPredicatedData ?? new Dictionary<AnalysisEntity, (IDictionary<TKey, TValue> truePredicatedData, IDictionary<TKey, TValue> falsePredicatedData)>();
        }

        protected void StartTrackingPredicatedData(AnalysisEntity predicatedEntity, IDictionary<TKey, TValue> truePredicatedData, IDictionary<TKey, TValue> falsePredicatedData)
        {
            Debug.Assert(predicatedEntity.Type.SpecialType == SpecialType.System_Boolean ||
                predicatedEntity.Type.Language == LanguageNames.VisualBasic && predicatedEntity.Type.SpecialType == SpecialType.System_Object);
            Debug.Assert(predicatedEntity.CaptureIdOpt != null, "Currently we only support predicated data tracking for flow captures");

            EnsurePredicatedData();
            _lazyPredicatedData[predicatedEntity] = (truePredicatedData, falsePredicatedData);
        }

        public void StopTrackingPredicatedData(AnalysisEntity predicatedEntity)
        {
            Debug.Assert(HasPredicatedDataForEntity(predicatedEntity));
            Debug.Assert(predicatedEntity.CaptureIdOpt != null, "Currently we only support predicated data tracking for flow captures");

            _lazyPredicatedData.Remove(predicatedEntity);
            if (_lazyPredicatedData.Count == 0)
            {
                _lazyPredicatedData = null;
            }
        }

        public bool HasPredicatedDataForEntity(AnalysisEntity predicatedEntity)
            => HasPredicatedData && _lazyPredicatedData.ContainsKey(predicatedEntity);

        public void TransferPredicatedData(AnalysisEntity fromEntity, AnalysisEntity toEntity)
        {
            Debug.Assert(HasPredicatedDataForEntity(fromEntity));
            Debug.Assert(fromEntity.CaptureIdOpt != null, "Currently we only support predicated data tracking for flow captures");
            Debug.Assert(toEntity.CaptureIdOpt != null, "Currently we only support predicated data tracking for flow captures");

            if (_lazyPredicatedData.TryGetValue(fromEntity, out var fromEntityPredicatedData))
            {
                _lazyPredicatedData[toEntity] = fromEntityPredicatedData;
            }
        }

        protected PredicateValueKind ApplyPredicatedDataForEntity(IDictionary<TKey, TValue> coreAnalysisData, AnalysisEntity predicatedEntity, bool trueData)
        {
            Debug.Assert(HasPredicatedDataForEntity(predicatedEntity));

            var predicatedDataTuple = _lazyPredicatedData[predicatedEntity];
            var predicatedDataToApply = trueData ? predicatedDataTuple.truePredicatedData : predicatedDataTuple.falsePredicatedData;
            if (predicatedDataToApply == null)
            {
                // Infeasible branch.
                return PredicateValueKind.AlwaysFalse;
            }

            ApplyPredicatedData(coreAnalysisData, predicatedDataToApply);

            // Predicate is always true if other branch predicate data is null.
            var otherBranchPredicatedData = trueData ? predicatedDataTuple.falsePredicatedData : predicatedDataTuple.truePredicatedData;
            return otherBranchPredicatedData == null ?
                PredicateValueKind.AlwaysTrue :
                PredicateValueKind.Unknown;
        }

        protected virtual void ApplyPredicatedData(IDictionary<TKey, TValue> coreAnalysisData, IDictionary<TKey, TValue> predicatedData)
        {
            Debug.Assert(coreAnalysisData != null);
            Debug.Assert(predicatedData != null);

            foreach (var kvp in predicatedData)
            {
                coreAnalysisData[kvp.Key] = kvp.Value;
            }
        }

        protected void RemoveEntriesInPredicatedData(TKey key)
        {
            Debug.Assert(HasPredicatedData);

            foreach (var kvp in _lazyPredicatedData)
            {
                if (kvp.Value.truePredicatedData != null)
                {
                    RemoveEntryInPredicatedData(key, kvp.Value.truePredicatedData);
                }

                if (kvp.Value.falsePredicatedData != null)
                {
                    RemoveEntryInPredicatedData(key, kvp.Value.falsePredicatedData);
                }
            }
        }

        protected virtual void RemoveEntryInPredicatedData(TKey key, IDictionary<TKey, TValue> predicatedData)
        {
            predicatedData.Remove(key);
        }

        private static Dictionary<AnalysisEntity, (IDictionary<TKey, TValue> truePredicatedData, IDictionary<TKey, TValue> falsePredicatedData)> Clone(PredicatedAnalysisData<TKey, TValue> fromData)
        {
            if (fromData._lazyPredicatedData == null)
            {
                return null;
            }

            var clonedMap = new Dictionary<AnalysisEntity, (IDictionary<TKey, TValue> truePredicatedData, IDictionary<TKey, TValue> falsePredicatedData)>();
            foreach (var kvp in fromData._lazyPredicatedData)
            {
                var clonedTruePredicatedData = kvp.Value.truePredicatedData == null ? null : new Dictionary<TKey, TValue>(kvp.Value.truePredicatedData);
                var clonedFalsePredicatedData = kvp.Value.falsePredicatedData == null ? null : new Dictionary<TKey, TValue>(kvp.Value.falsePredicatedData);
                clonedMap.Add(kvp.Key, (clonedTruePredicatedData, clonedFalsePredicatedData));
            }

            return clonedMap;
        }

        private static Dictionary<AnalysisEntity, (IDictionary<TKey, TValue> truePredicatedData, IDictionary<TKey, TValue> falsePredicatedData)> Merge(
            PredicatedAnalysisData<TKey, TValue> predicatedData1,
            PredicatedAnalysisData<TKey, TValue> predicatedData2,
            IDictionary<TKey, TValue> coreAnalysisData1,
            IDictionary<TKey, TValue> coreAnalysisData2,
            MapAbstractDomain<TKey, TValue> coreDataAnalysisDomain,
            Action<IDictionary<TKey, TValue>, IDictionary<TKey, TValue>> applyPredicatedData)
        {
            Debug.Assert(predicatedData1 != null);
            Debug.Assert(predicatedData2 != null);
            Debug.Assert(coreAnalysisData1 != null);
            Debug.Assert(coreAnalysisData2 != null);

            if (predicatedData1._lazyPredicatedData == null)
            {
                if (predicatedData2._lazyPredicatedData == null)
                {
                    return null;
                }

                return MergeForPredicatedDataInOneBranch(predicatedData2._lazyPredicatedData, coreAnalysisData1, coreDataAnalysisDomain);
            }
            else if (predicatedData2._lazyPredicatedData == null)
            {
                return MergeForPredicatedDataInOneBranch(predicatedData1._lazyPredicatedData, coreAnalysisData2, coreDataAnalysisDomain);
            }

            return MergeForPredicatedDataInBothBranches(predicatedData1._lazyPredicatedData, predicatedData2._lazyPredicatedData,
                coreAnalysisData1, coreAnalysisData2, coreDataAnalysisDomain, applyPredicatedData);
        }

        private static Dictionary<AnalysisEntity, (IDictionary<TKey, TValue> truePredicatedData, IDictionary<TKey, TValue> falsePredicatedData)> MergeForPredicatedDataInOneBranch(
            IDictionary<AnalysisEntity, (IDictionary<TKey, TValue> truePredicatedData, IDictionary<TKey, TValue> falsePredicatedData)> predicatedData,
            IDictionary<TKey, TValue> coreAnalysisDataForOtherBranch,
            MapAbstractDomain<TKey, TValue> coreDataAnalysisDomain)
        {
            Debug.Assert(predicatedData != null);
            Debug.Assert(coreAnalysisDataForOtherBranch != null);

            var result = new Dictionary<AnalysisEntity, (IDictionary<TKey, TValue> truePredicatedData, IDictionary<TKey, TValue> falsePredicatedData)>();
            foreach (var kvp in predicatedData)
            {
                var resultTruePredicatedData = MergeForPredicatedDataInOneBranch(kvp.Value.truePredicatedData, coreAnalysisDataForOtherBranch, coreDataAnalysisDomain);
                var resultFalsePredicatedData = MergeForPredicatedDataInOneBranch(kvp.Value.falsePredicatedData, coreAnalysisDataForOtherBranch, coreDataAnalysisDomain);
                result.Add(kvp.Key, (resultTruePredicatedData, resultFalsePredicatedData));
            }

            return result;
        }

        private static IDictionary<TKey, TValue> MergeForPredicatedDataInOneBranch(
            IDictionary<TKey, TValue> predicatedData,
            IDictionary<TKey, TValue> coreAnalysisDataForOtherBranch,
            MapAbstractDomain<TKey, TValue> coreDataAnalysisDomain)
        {
            if (predicatedData == null)
            {
                return null;
            }

            return coreDataAnalysisDomain.Merge(predicatedData, coreAnalysisDataForOtherBranch);
        }

        private static Dictionary<AnalysisEntity, (IDictionary<TKey, TValue> truePredicatedData, IDictionary<TKey, TValue> falsePredicatedData)> MergeForPredicatedDataInBothBranches(
            IDictionary<AnalysisEntity, (IDictionary<TKey, TValue> truePredicatedData, IDictionary<TKey, TValue> falsePredicatedData)> predicatedData1,
            IDictionary<AnalysisEntity, (IDictionary<TKey, TValue> truePredicatedData, IDictionary<TKey, TValue> falsePredicatedData)> predicatedData2,
            IDictionary<TKey, TValue> coreAnalysisData1,
            IDictionary<TKey, TValue> coreAnalysisData2,
            MapAbstractDomain<TKey, TValue> coreDataAnalysisDomain,
            Action<IDictionary<TKey, TValue>, IDictionary<TKey, TValue>> applyPredicatedData)
        {
            Debug.Assert(predicatedData1 != null);
            Debug.Assert(predicatedData2 != null);
            Debug.Assert(coreAnalysisData1 != null);
            Debug.Assert(coreAnalysisData2 != null);

            var result = new Dictionary<AnalysisEntity, (IDictionary<TKey, TValue> truePredicatedData, IDictionary<TKey, TValue> falsePredicatedData)>();
            foreach (var kvp in predicatedData1)
            {
                IDictionary<TKey, TValue> resultTruePredicatedData;
                IDictionary<TKey, TValue> resultFalsePredicatedData;
                if (!predicatedData2.TryGetValue(kvp.Key, out var value2))
                {
                    // Data predicated by the analysis entity present in only one branch.
                    // We should merge with the core non-predicate data in other branch.
                    resultTruePredicatedData = MergeForPredicatedDataInOneBranch(kvp.Value.truePredicatedData, coreAnalysisData2, coreDataAnalysisDomain);
                    resultFalsePredicatedData = MergeForPredicatedDataInOneBranch(kvp.Value.falsePredicatedData, coreAnalysisData2, coreDataAnalysisDomain);
                }
                else
                {
                    // Data predicated by the analysis entity present in both branches.
                    resultTruePredicatedData = Merge(kvp.Value.truePredicatedData, value2.truePredicatedData,
                        coreAnalysisData1, coreAnalysisData2, coreDataAnalysisDomain, applyPredicatedData);
                    resultFalsePredicatedData = Merge(kvp.Value.falsePredicatedData, value2.falsePredicatedData,
                        coreAnalysisData1, coreAnalysisData2, coreDataAnalysisDomain, applyPredicatedData);
                }

                result.Add(kvp.Key, (resultTruePredicatedData, resultFalsePredicatedData));
            }

            foreach (var kvp in predicatedData2)
            {
                if (!predicatedData1.TryGetValue(kvp.Key, out var value2))
                {
                    // Data predicated by the analysis entity present in only one branch.
                    // We should merge with the core non-predicate data in other branch.
                    var resultTruePredicatedData = MergeForPredicatedDataInOneBranch(kvp.Value.truePredicatedData, coreAnalysisData1, coreDataAnalysisDomain);
                    var resultFalsePredicatedData = MergeForPredicatedDataInOneBranch(kvp.Value.falsePredicatedData, coreAnalysisData1, coreDataAnalysisDomain);
                    result.Add(kvp.Key, (resultTruePredicatedData, resultFalsePredicatedData));
                }
            }

            return result;            
        }

        private static IDictionary<TKey, TValue> Merge(
            IDictionary<TKey, TValue> predicateTrueOrFalseData1,
            IDictionary<TKey, TValue> predicateTrueOrFalseData2,
            IDictionary<TKey, TValue> coreAnalysisData1,
            IDictionary<TKey, TValue> coreAnalysisData2,
            MapAbstractDomain<TKey, TValue> coreDataAnalysisDomain,
            Action<IDictionary<TKey, TValue>, IDictionary<TKey, TValue>> applyPredicatedData)
        {
            if (predicateTrueOrFalseData1 == null)
            {
                return predicateTrueOrFalseData2 != null ?
                    CloneAndApplyPredicatedData(coreAnalysisData2, predicateTrueOrFalseData2, applyPredicatedData) :
                    null;
            }
            else if (predicateTrueOrFalseData2 == null)
            {
                return CloneAndApplyPredicatedData(coreAnalysisData1, predicateTrueOrFalseData1, applyPredicatedData);
            }

            var appliedPredicatedData1 = CloneAndApplyPredicatedData(coreAnalysisData1, predicateTrueOrFalseData1, applyPredicatedData);
            var appliedPredicatedData2 = CloneAndApplyPredicatedData(coreAnalysisData2, predicateTrueOrFalseData2, applyPredicatedData);

            return coreDataAnalysisDomain.Merge(appliedPredicatedData1, appliedPredicatedData2);            
        }

        private static IDictionary<TKey, TValue> CloneAndApplyPredicatedData(
            IDictionary<TKey, TValue> coreAnalysisData,
            IDictionary<TKey, TValue> predicateTrueOrFalseData,
            Action<IDictionary<TKey, TValue>, IDictionary<TKey, TValue>> applyPredicatedData)
        {
            Debug.Assert(predicateTrueOrFalseData != null);
            Debug.Assert(coreAnalysisData != null);

            var result = new Dictionary<TKey, TValue>(coreAnalysisData);
            applyPredicatedData(result, predicateTrueOrFalseData);
            return result;
        }

        protected int BaseCompareHelper(PredicatedAnalysisData<TKey, TValue> newData)
        {
            Debug.Assert(newData != null);

            if (!IsReachableBlockData && newData.IsReachableBlockData)
            {
                return -1;
            }

            if (_lazyPredicatedData == null)
            {
                return newData._lazyPredicatedData == null ? 0 : -1;
            }
            else if (newData._lazyPredicatedData == null)
            {
                return 1;
            }

            if (ReferenceEquals(this, newData))
            {
                return 0;
            }

            // Note that predicate maps can add or remove entries based on core analysis data entries.
            // We can only determine if the predicate data is equal or not.
            return Equals(newData) ? 0 : -1;
        }

        protected bool Equals(PredicatedAnalysisData<TKey, TValue> other)
        {
            if (_lazyPredicatedData == null)
            {
                return other._lazyPredicatedData == null;
            }
            else if (other._lazyPredicatedData == null ||
                _lazyPredicatedData.Count != other._lazyPredicatedData.Count)
            {
                return false;
            }
            else
            {
                foreach (var kvp in _lazyPredicatedData)
                {
                    if (!other._lazyPredicatedData.TryGetValue(kvp.Key, out var otherValue) ||
                        !EqualsHelper(kvp.Value.truePredicatedData, otherValue.truePredicatedData) ||
                        !EqualsHelper(kvp.Value.falsePredicatedData, otherValue.falsePredicatedData))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        protected static bool EqualsHelper(IDictionary<TKey, TValue> dict1, IDictionary<TKey, TValue> dict2)
        {
            if (dict1 == null)
            {
                return dict2 == null;
            }
            else if (dict2 == null || dict1.Count != dict2.Count)
            {
                return false;
            }

            return dict1.Keys.All(key => dict2.TryGetValue(key, out TValue value2) &&
                                         EqualityComparer<TValue>.Default.Equals(dict1[key], value2));
        }

        protected void ResetPredicatedData()
        {
            _lazyPredicatedData = null;
        }

        [Conditional("DEBUG")]
        protected void AssertValidPredicatedAnalysisData(Action<IDictionary<TKey, TValue>> assertValidAnalysisData)
        {
            if (HasPredicatedData)
            {
                foreach (var kvp in _lazyPredicatedData)
                {
                    if (kvp.Value.truePredicatedData != null)
                    {
                        assertValidAnalysisData(kvp.Value.truePredicatedData);
                    }

                    if (kvp.Value.falsePredicatedData != null)
                    {
                        assertValidAnalysisData(kvp.Value.falsePredicatedData);
                    }
                }
            }
        }
    }
}
