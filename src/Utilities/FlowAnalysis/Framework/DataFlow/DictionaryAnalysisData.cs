﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.CodeAnalysis.FlowAnalysis.DataFlow
{
    internal sealed class DictionaryAnalysisData<TKey, TValue> : AbstractAnalysisData, IDictionary<TKey, TValue>
    {
        private PooledDictionary<TKey, TValue> _coreAnalysisData;
        private bool _sealed;

        public DictionaryAnalysisData()
        {
            _coreAnalysisData = PooledDictionary<TKey, TValue>.GetInstance();
        }

        public DictionaryAnalysisData(IDictionary<TKey, TValue> initializer)
        {
            _coreAnalysisData = PooledDictionary<TKey, TValue>.GetInstance(initializer);
        }

        public IDictionary<TKey, TValue> Seal()
        {
            Debug.Assert(!IsDisposed);
            Debug.Assert(!_sealed);
            _sealed = true;
            return this;
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
                Debug.Assert(!_sealed);
                _coreAnalysisData[key] = value;
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
            Debug.Assert(!_sealed);
            _coreAnalysisData.Add(key, value);
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            Debug.Assert(!IsDisposed);
            Debug.Assert(!_sealed);
            _coreAnalysisData.Add(item.Key, item.Value);
        }

        public void Clear()
        {
            Debug.Assert(!IsDisposed);
            Debug.Assert(!_sealed);
            _coreAnalysisData.Clear();
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
            Debug.Assert(!_sealed);
            return _coreAnalysisData.Remove(key);
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            Debug.Assert(!IsDisposed);
            Debug.Assert(!_sealed);
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
            }

            base.Dispose(disposing);
        }
    }
}