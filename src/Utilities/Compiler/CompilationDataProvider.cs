// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.CodeAnalysis;

#pragma warning disable RS1012 // Start action has no registered actions.

namespace Analyzer.Utilities
{
    public sealed partial class CompilationDataProvider : IEquatable<CompilationDataProvider>
    {
        private class ReferenceCount
        {
#pragma warning disable CA1051 // Do not declare visible instance fields
            public int Count;
#pragma warning restore CA1051 // Do not declare visible instance fields
        }

        private static readonly Dictionary<Compilation, ReferenceCount> s_compilationReferenceCountMap
            = new Dictionary<Compilation, ReferenceCount>();
        private static readonly ConcurrentDictionary<Compilation, ConcurrentDictionary<object, object>> s_compilationToDataCachesMap
            = new ConcurrentDictionary<Compilation, ConcurrentDictionary<object, object>>();

        internal CompilationDataProvider(CompilationDataProviderFactory factory)
        {
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));

            VerifyState(verifyNonZeroRefCount: false);

            lock (s_compilationReferenceCountMap)
            {
                if (!s_compilationReferenceCountMap.TryGetValue(Factory.Compilation, out var referenceCount))
                {
                    referenceCount = new ReferenceCount();
                    s_compilationReferenceCountMap.Add(Factory.Compilation, referenceCount);
                }

                referenceCount.Count++;
            }
        }

        public CompilationDataProviderFactory Factory { get; }

        [Conditional("DEBUG")]
        private void VerifyState(bool verifyNonZeroRefCount)
        {
            Debug.Assert(Factory != null);

            lock (s_compilationReferenceCountMap)
            {
                if (verifyNonZeroRefCount ||
                    s_compilationToDataCachesMap.TryGetValue(Factory.Compilation, out var _))
                {
                    Debug.Assert(s_compilationReferenceCountMap.TryGetValue(Factory.Compilation, out var referenceCount));
                    Debug.Assert(referenceCount.Count > 0);
                }
            }
        }

        ~CompilationDataProvider()
        {
            if (Factory == null)
            {
                // Static .cctor
                Debug.Assert(s_compilationReferenceCountMap.Count == 0);
                Debug.Assert(s_compilationToDataCachesMap.IsEmpty);
                return;
            }

            VerifyState(verifyNonZeroRefCount: true);

            lock (s_compilationReferenceCountMap)
            {
                if (s_compilationReferenceCountMap.TryGetValue(Factory.Compilation, out var referenceCount))
                {
                    referenceCount.Count--;

                    if (referenceCount.Count <= 0)
                    {
                        s_compilationReferenceCountMap.Remove(Factory.Compilation);
                        s_compilationToDataCachesMap.TryRemove(Factory.Compilation, out _);
                    }
                }
            }
        }

        public Compilation Compilation => Factory.Compilation;

        public TCompilationData GetOrCreateValue<TCompilationData>(Func<Compilation, TCompilationData> valueFactory, object uniqueCacheId)
            where TCompilationData : class
        {
            VerifyState(verifyNonZeroRefCount: true);

            var idToCacheMap = s_compilationToDataCachesMap.GetOrAdd(Factory.Compilation, CreateDataCache);

            TCompilationData data;
            if (idToCacheMap.TryGetValue(uniqueCacheId, out object dataObj))
            {
                data = dataObj as TCompilationData;

                if (data == null)
                {
                    Debug.Fail("Multiple caches with same unique cache ID");
                    data = valueFactory(Factory.Compilation);
                }

                return data;
            }

            return (TCompilationData)idToCacheMap.GetOrAdd(uniqueCacheId, valueFactory(Factory.Compilation));
        }

        private static ConcurrentDictionary<object, object> CreateDataCache(Compilation _)
            => new ConcurrentDictionary<object, object>();

        public TCompilationData GetOrCreateValue<TCompilationData>(object uniqueCacheId)
            where TCompilationData : class, new()
        {
            return GetOrCreateValue(CreateDefaultValue, uniqueCacheId);

            // Local functions.
            static TCompilationData CreateDefaultValue(Compilation _) => new TCompilationData();
        }

        public bool Equals(CompilationDataProvider other)
            => Factory == other.Factory;

        public override bool Equals(object obj)
            => Equals(obj as CompilationDataProvider);

        public override int GetHashCode()
            => Factory.GetHashCode();
    }
}