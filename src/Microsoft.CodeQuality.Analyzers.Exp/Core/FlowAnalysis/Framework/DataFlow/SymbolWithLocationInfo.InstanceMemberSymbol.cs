// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis.Operations.DataFlow.PointsToAnalysis;

namespace Microsoft.CodeAnalysis.Operations.DataFlow
{
    internal abstract partial class SymbolWithLocationInfo
    {
        private sealed class InstanceMemberSymbol : SymbolWithLocationInfo
        {
            private readonly ISymbol _symbol;
            private readonly InstanceLocationInfo _instanceWithLocation;

            public InstanceMemberSymbol(ISymbol symbol, ImmutableArray<AbstractIndexer> indexers, InstanceLocationInfo instanceWithLocation, ITypeSymbol type, SymbolWithLocationInfo parentOpt)
                : base(type, indexers, parentOpt)
            {
                Debug.Assert(symbol != null);
                Debug.Assert(symbol.Kind != SymbolKind.Parameter);
                Debug.Assert(symbol.Kind != SymbolKind.Local);
                Debug.Assert(instanceWithLocation != null);
                Debug.Assert(instanceWithLocation.InstanceType.OriginalDefinition.Equals(symbol.ContainingType.OriginalDefinition));
                
                _symbol = symbol;
                _instanceWithLocation = instanceWithLocation;
            }

            public override ISymbol Symbol => _symbol;
            public override InstanceLocationInfo LocationInfo => _instanceWithLocation;

            public override int GetHashCode()
            {
                return HashUtilities.Combine(GetBaseGetHashCode(),
                    HashUtilities.Combine(_symbol.GetHashCode(), _instanceWithLocation.GetHashCode()));
            }

            public override bool Equals(SymbolWithLocationInfo other)
            {
                return other is InstanceMemberSymbol instanceMemberOther &&
                    BaseEquals(other) &&
                    _symbol.Equals(instanceMemberOther._symbol) &&
                    _instanceWithLocation.Equals(instanceMemberOther._instanceWithLocation);
            }
        }
    }
}