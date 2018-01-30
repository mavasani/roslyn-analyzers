// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis.Operations.DataFlow.PointsToAnalysis;

namespace Microsoft.CodeAnalysis.Operations.DataFlow
{
    internal abstract partial class SymbolWithLocationInfo
    {
        private sealed class NonMemberOrStaticSymbol: SymbolWithLocationInfo
        {
            private readonly ISymbol _symbol;

            public NonMemberOrStaticSymbol(ISymbol symbol, ImmutableArray<AbstractIndexer> indexers, ITypeSymbol type, SymbolWithLocationInfo parentOpt)
                : base(type, indexers, parentOpt)
            {
                Debug.Assert(symbol != null);
                _symbol = symbol;
            }

            public override ISymbol Symbol => _symbol;
            public override InstanceLocationInfo LocationInfo => null;
            public override int GetHashCode() => HashUtilities.Combine(GetBaseGetHashCode(), _symbol.GetHashCode());
            public override bool Equals(SymbolWithLocationInfo other)
            {
                return other is NonMemberOrStaticSymbol nonMemberOrStaticOther &&
                    BaseEquals(other) &&
                    _symbol.Equals(nonMemberOrStaticOther._symbol);
            }
        }
    }
}