// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;

namespace Microsoft.CodeAnalysis.Operations.DataFlow.PointsToAnalysis
{
    internal sealed class PointsToAnalysisData2
    {
        public static readonly PointsToAnalysisData Empty = new PointsToAnalysisData();

        public ImmutableDictionary<SymbolWithInstance, PointsToAbstractValue>.Builder Values { get; private set; }

        public ImmutableDictionary<AbstractIndexer, IndexerKind>.Builder Indexers { get; private set; }

        private PointsToAnalysisData()
        {
        }

        private PointsToAnalysisData(IDictionary<SymbolWithInstance, PointsToAbstractValue> values, IDictionary<AbstractIndexer, IndexerKind> indexers)
        {
            if (values?.Count > 0)
            {
                Values = ImmutableDictionary.CreateBuilder<SymbolWithInstance, PointsToAbstractValue>();
                Values.AddRange(values);
            }

            if (indexers?.Count > 0)
            {
                Indexers = ImmutableDictionary.CreateBuilder<AbstractIndexer, IndexerKind>();
                Indexers.AddRange(indexers);
            }
        }

        public static PointsToAnalysisData Create(IDictionary<SymbolWithInstance, PointsToAbstractValue> values, IDictionary<AbstractIndexer, IndexerKind> indexers)
        {
            if (values?.Count == 0 && indexers?.Count == 0)
            {
                return Empty;
            }

            return new PointsToAnalysisData(values, indexers);
        }

        public void SetAbstractValue(SymbolWithInstance symbolWithInstance, PointsToAbstractValue value)
        {
            Values = Values ?? ImmutableDictionary.CreateBuilder<SymbolWithInstance, PointsToAbstractValue>();
            Values[symbolWithInstance] = value;
        }

        public PointsToAbstractValue GetAbstractValue(SymbolWithInstance symbolWithInstance)
        {
            PointsToAbstractValue value;
            if (Values != null && Values.TryGetValue(symbolWithInstance, out value))
            {
                return value;
            }

            return PointsToAbstractValue.Unknown;
        }

        public void SetAbstractValue(AbstractIndexer indexer, IndexerKind value)
        {
            Indexers = Indexers ?? ImmutableDictionary.CreateBuilder<AbstractIndexer, IndexerKind>();
            Indexers[indexer] = value;
        }

        public void UpdateAbstractValueIfExists(AbstractIndexer indexer, IndexerKind value)
        {
            if (Indexers != null && Indexers.ContainsKey(indexer))
            {
                Indexers[indexer] = value;
            }
        }

        public IndexerKind GetAbstractValue(AbstractIndexer indexer)
        {
            IndexerKind value;
            if (Indexers != null && Indexers.TryGetValue(indexer, out value))
            {
                return value;
            }

            return IndexerKind.Undefined;
        }
    }
}
