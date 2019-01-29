// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading;
using Analyzer.Utilities.Extensions;

#pragma warning disable CA1067 // Override Object.Equals(object) when implementing IEquatable<T>
#pragma warning disable CA1710 // Rename DictionaryAnalysisData to end in 'Dictionary'

namespace Microsoft.CodeAnalysis.FlowAnalysis.DataFlow
{
    internal sealed class DictionaryAnalysisData<TKey, TValue> : AbstractAnalysisData, IDictionary<TKey, TValue>
        where TKey: IEquatable<TKey>
    {
        private PooledDictionary<TKey, TValue> _coreAnalysisData;
        private ArrayBuilder<(TKey Key, long UpdateId)> _entryUpdateIds;
        private static long s_id = 0;

        public DictionaryAnalysisData()
        {
            _coreAnalysisData = PooledDictionary<TKey, TValue>.GetInstance();
            _entryUpdateIds = ArrayBuilder<(TKey Key, long UpdateId)>.GetInstance();
        }

        public DictionaryAnalysisData(ImmutableDictionary<TKey, TValue> initializer)
        {
            _coreAnalysisData = PooledDictionary<TKey, TValue>.GetInstance(initializer);

            _entryUpdateIds = ArrayBuilder<(TKey Key, long UpdateId)>.GetInstance(initializer.Count);
            var updateId = GetUpdateId();
            foreach (var key in initializer.Keys)
            {
                _entryUpdateIds.Add((key, updateId));
            }
        }

        public DictionaryAnalysisData(DictionaryAnalysisData<TKey, TValue> initializer)
        {
            Debug.Assert(!initializer.IsDisposed);

            _coreAnalysisData = PooledDictionary<TKey, TValue>.GetInstance(initializer._coreAnalysisData);
            _entryUpdateIds = ArrayBuilder<(TKey Key, long UpdateId)>.GetInstance(initializer._entryUpdateIds);
        }

        private static long GetUpdateId()
            => Interlocked.Increment(ref s_id);
        private bool HasValidUpdateIds()
            => _entryUpdateIds.Count == 0 || _entryUpdateIds[0].UpdateId <= _entryUpdateIds[_entryUpdateIds.Count - 1].UpdateId;

        public ImmutableDictionary<TKey, TValue> ToImmutableDictionary()
        {
            Debug.Assert(!IsDisposed);
            return _coreAnalysisData.ToImmutableDictionary();
        }

        public PooledHashSet<TKey> GetKeysToMerge(DictionaryAnalysisData<TKey, TValue> other)
        {
            Debug.Assert(!IsDisposed);
            Debug.Assert(other != null);
            Debug.Assert(!other.IsDisposed);

            var keysToMerge = PooledHashSet<TKey>.GetInstance();
            if (!HasValidUpdateIds() ||
                !other.HasValidUpdateIds())
            {
                keysToMerge.AddRange(_coreAnalysisData.Keys);
                keysToMerge.AddRange(other._coreAnalysisData.Keys);
                return keysToMerge;
            }

            var defaultEntry = (Key: default(TKey), UpdateId: long.MinValue);
            int i = _entryUpdateIds.Count - 1;
            int j = other._entryUpdateIds.Count - 1;
            while (i >= 0 || j >= 0)
            {
                var myEntry = i >= 0 ? _entryUpdateIds[i] : defaultEntry;
                var otherEntry = j >= 0 ? other._entryUpdateIds[j]: defaultEntry;
                if (myEntry.UpdateId > otherEntry.UpdateId)
                {
                    keysToMerge.Add(myEntry.Key);
                    i--;
                }
                else if (otherEntry.UpdateId > myEntry.UpdateId)
                {
                    keysToMerge.Add(otherEntry.Key);
                    j--;
                }
                else
                {
                    Debug.Assert(i == j);
#if DEBUG
                    for (i = i - 1; i >= 0; i--)
                    {
                        myEntry = _entryUpdateIds[i];
                        otherEntry = other._entryUpdateIds[i];
                        Debug.Assert(myEntry.Key.Equals(otherEntry.Key));
                        Debug.Assert(myEntry.UpdateId == otherEntry.UpdateId);
                    }
#endif
                    break;
                }
            }

            return keysToMerge;
        }

        public TValue this[TKey key]
        {
            get
            {
                Debug.Assert(!IsDisposed);
                return _coreAnalysisData[key];
            }
            set
            {
                Debug.Assert(!IsDisposed);
                _coreAnalysisData[key] = value;
                _entryUpdateIds.Add((key, GetUpdateId()));
            }
        }

        public ICollection<TKey> Keys
        {
            get
            {
                Debug.Assert(!IsDisposed);
                return _coreAnalysisData.Keys;
            }
        }

        public ICollection<TValue> Values
        {
            get
            {
                // "Values" might be accessed during dispose.
                //Debug.Assert(!IsDisposed);
                return _coreAnalysisData.Values;
            }
        }

        public int Count
        {
            get
            {
                Debug.Assert(!IsDisposed);
                return _coreAnalysisData.Count;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                Debug.Assert(!IsDisposed);
                return ((IDictionary<TKey, TValue>)_coreAnalysisData).IsReadOnly;
            }
        }

        public void Add(TKey key, TValue value)
        {
            Debug.Assert(!IsDisposed);
            _coreAnalysisData.Add(key, value);
            _entryUpdateIds.Add((key, GetUpdateId()));
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            Debug.Assert(!IsDisposed);
            _coreAnalysisData.Add(item.Key, item.Value);
            _entryUpdateIds.Add((item.Key, GetUpdateId()));
        }

        public void Clear()
        {
            Debug.Assert(!IsDisposed);
            _coreAnalysisData.Clear();
            _entryUpdateIds.Clear();
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            Debug.Assert(!IsDisposed);
            return ((IDictionary<TKey, TValue>)_coreAnalysisData).Contains(item);
        }

        public bool ContainsKey(TKey key)
        {
            Debug.Assert(!IsDisposed);
            return _coreAnalysisData.ContainsKey(key);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            Debug.Assert(!IsDisposed);
            ((IDictionary<TKey, TValue>)_coreAnalysisData).CopyTo(array, arrayIndex);
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            Debug.Assert(!IsDisposed);
            return _coreAnalysisData.GetEnumerator();
        }

        public bool Remove(TKey key)
        {
            Debug.Assert(!IsDisposed);
            return _coreAnalysisData.Remove(key);
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            Debug.Assert(!IsDisposed);
            return Remove(item.Key);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            Debug.Assert(!IsDisposed);
            return _coreAnalysisData.TryGetValue(key, out value);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            Debug.Assert(!IsDisposed);
            return _coreAnalysisData.GetEnumerator();
        }

        protected override void Dispose(bool disposing)
        {
            if (IsDisposed)
            {
                return;
            }

            if (disposing)
            {
                _coreAnalysisData.Free();
                _coreAnalysisData = null;

                _entryUpdateIds.Free();
                _entryUpdateIds = null;
            }

            base.Dispose(disposing);
        }
    }
}
