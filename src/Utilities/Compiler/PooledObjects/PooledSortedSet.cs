﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

#pragma warning disable CA1000 // Do not declare static members on generic types

namespace Analyzer.Utilities.PooledObjects
{
    /// <summary>
    /// Pooled <see cref="SortedSet{T}"/>.
    /// </summary>
    /// <typeparam name="T">Type of elements in the set.</typeparam>
    internal sealed class PooledSortedSet<T> : SortedSet<T>, IDisposable
    {
        private readonly ObjectPool<PooledSortedSet<T>>? _pool;

        public PooledSortedSet(ObjectPool<PooledSortedSet<T>>? pool, IComparer<T>? comparer = null)
            : base(comparer)
        {
            _pool = pool;
        }

        public void Dispose() => Free(CancellationToken.None);

        public void Free(CancellationToken cancellationToken)
        {
            this.Clear();
            _pool?.Free(this, cancellationToken);
        }

        // global pool
        private static readonly ObjectPool<PooledSortedSet<T>> s_poolInstance = CreatePool();
        private static readonly ConcurrentDictionary<IComparer<T>, ObjectPool<PooledSortedSet<T>>> s_poolInstancesByComparer
            = new ConcurrentDictionary<IComparer<T>, ObjectPool<PooledSortedSet<T>>>();

        private static ObjectPool<PooledSortedSet<T>> CreatePool(IComparer<T>? comparer = null)
        {
            ObjectPool<PooledSortedSet<T>>? pool = null;
            pool = new ObjectPool<PooledSortedSet<T>>(
                () => new PooledSortedSet<T>(pool, comparer),
                128);
            return pool;
        }

        /// <summary>
        /// Gets a pooled instance of a <see cref="PooledSortedSet{T}"/> with an optional comparer.
        /// </summary>
        /// <param name="comparer">Comparer to use, or null for the element type's default comparer.</param>
        /// <returns>An empty <see cref="PooledSortedSet{T}"/>.</returns>
        public static PooledSortedSet<T> GetInstance(IComparer<T>? comparer = null)
        {
            var pool = comparer == null ?
                s_poolInstance :
                s_poolInstancesByComparer.GetOrAdd(comparer, c => CreatePool(c));
            var instance = pool.Allocate();
            Debug.Assert(instance.Count == 0);
            return instance;
        }

        /// <summary>
        /// Gets a pooled instance of a <see cref="PooledSortedSet{T}"/> with the given initializer and an optional comparer.
        /// </summary>
        /// <param name="initializer">Initializer for the set.</param>
        /// <param name="comparer">Comparer to use, or null for the element type's default comparer.</param>
        /// <returns>An empty <see cref="PooledSortedSet{T}"/>.</returns>
        public static PooledSortedSet<T> GetInstance(IEnumerable<T> initializer, IComparer<T>? comparer = null)
        {
            var instance = GetInstance(comparer);
            foreach (var value in initializer)
            {
                instance.Add(value);
            }

            return instance;
        }
    }
}
