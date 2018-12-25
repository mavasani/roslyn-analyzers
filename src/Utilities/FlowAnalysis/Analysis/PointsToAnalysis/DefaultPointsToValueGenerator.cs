﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using Analyzer.Utilities.Extensions;

namespace Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.PointsToAnalysis
{
    /// <summary>
    /// Generates and stores the default <see cref="PointsToAbstractValue"/> for <see cref="AnalysisEntity"/> instances generated for member and element reference operations.
    /// </summary>
    internal sealed class DefaultPointsToValueGenerator
    {
        private readonly ImmutableDictionary<AnalysisEntity, PointsToAbstractValue>.Builder _defaultPointsToValueMapBuilder;
        
        public DefaultPointsToValueGenerator()
        {
            _defaultPointsToValueMapBuilder = ImmutableDictionary.CreateBuilder<AnalysisEntity, PointsToAbstractValue>();
        }

        public PointsToAbstractValue GetOrCreateDefaultValue(AnalysisEntity analysisEntity)
        {
            if (!_defaultPointsToValueMapBuilder.TryGetValue(analysisEntity, out PointsToAbstractValue value))
            {
                if (analysisEntity.SymbolOpt?.Kind == SymbolKind.Local ||
                    analysisEntity.SymbolOpt is IParameterSymbol parameter && parameter.RefKind == RefKind.Out ||
                    analysisEntity.CaptureIdOpt != null)
                {
                    return PointsToAbstractValue.Undefined;
                }
                else if (!analysisEntity.Type.IsReferenceTypeOrNullableValueType())
                {
                    return PointsToAbstractValue.NoLocation;
                }
                else if (analysisEntity.HasUnknownInstanceLocation)
                {
                    return PointsToAbstractValue.Unknown;
                }

                value = PointsToAbstractValue.Create(AbstractLocation.CreateAnalysisEntityDefaultLocation(analysisEntity), mayBeNull: true);
                _defaultPointsToValueMapBuilder.Add(analysisEntity, value);
            }

            return value;
        }

        public void AddTrackedEntities(PooledHashSet<AnalysisEntity> builder) => builder.AddRange(_defaultPointsToValueMapBuilder.Keys);
        public bool IsTrackedEntity(AnalysisEntity analysisEntity) => _defaultPointsToValueMapBuilder.ContainsKey(analysisEntity);
    }
}