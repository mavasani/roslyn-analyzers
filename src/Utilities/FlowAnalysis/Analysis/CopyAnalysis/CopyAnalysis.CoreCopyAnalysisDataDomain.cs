// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Diagnostics;

namespace Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.CopyAnalysis
{
    using CoreCopyAnalysisData = DictionaryAnalysisData<AnalysisEntity, CopyAbstractValue>;
    using CopyAnalysisResult = DataFlowAnalysisResult<CopyBlockAnalysisResult, CopyAbstractValue>;

    internal partial class CopyAnalysis : ForwardDataFlowAnalysis<CopyAnalysisData, CopyAnalysisContext, CopyAnalysisResult, CopyBlockAnalysisResult, CopyAbstractValue>
    {
        /// <summary>
        /// An abstract analysis domain implementation for <see cref="CoreCopyAnalysisData"/>.
        /// </summary>
        private sealed class CoreCopyAnalysisDataDomain : MapAbstractDomain<AnalysisEntity, CopyAbstractValue>
        {
            Func<AnalysisEntity, CopyAbstractValue> _getDefaultCopyValue;

            public CoreCopyAnalysisDataDomain(AbstractValueDomain<CopyAbstractValue> valueDomain, Func<AnalysisEntity, CopyAbstractValue> getDefaultCopyValue)
                : base(valueDomain)
            {
                _getDefaultCopyValue = getDefaultCopyValue;
            }

            public override CoreCopyAnalysisData Merge(CoreCopyAnalysisData map1, CoreCopyAnalysisData map2)
            {
                Debug.Assert(map1 != null);
                Debug.Assert(map2 != null);
                CopyAnalysisData.AssertValidCopyAnalysisData(map1);
                CopyAnalysisData.AssertValidCopyAnalysisData(map2);

                var result = new DictionaryAnalysisData<AnalysisEntity, CopyAbstractValue>(map1);

                var keysToMerge = map1.GetKeysToMerge(map2);
                try
                {
                    foreach (var key in keysToMerge)
                    {
                        // If the key exists in both maps, use the merged value.
                        // Otherwise, use the default value.
                        CopyAbstractValue mergedValue;
                        var map1HasValue = map1.TryGetValue(key, out var value1);
                        var map2HasValue = map2.TryGetValue(key, out var value2);
                        if (map1HasValue && map2HasValue)
                        {
                            mergedValue = ValueDomain.Merge(value1, value2);
                        }
                        else if (map1HasValue || map2HasValue)
                        {
                            mergedValue = _getDefaultCopyValue(key);
                        }
                        else
                        {
                            continue;
                        }

                        result[key] = mergedValue;
                    }

                    // Update original entries from map1 where copy values differ in map2.
                    foreach (var kvp in map1)
                    {
                        var key = kvp.Key;
                        if (keysToMerge.Contains(key))
                        {
                            continue;
                        }

                        if (!map2.TryGetValue(key, out var value2))
                        {
                            result[key] = _getDefaultCopyValue(key);
                        }
                        else if (!value2.Equals(kvp.Value))
                        {
                            result.Remove(key);
                        }
                    }

                    CopyAnalysisData.AssertValidCopyAnalysisData(result);
                    return result;
                }
                finally
                {
                    keysToMerge.Free();
                }
            }
        }
    }
}