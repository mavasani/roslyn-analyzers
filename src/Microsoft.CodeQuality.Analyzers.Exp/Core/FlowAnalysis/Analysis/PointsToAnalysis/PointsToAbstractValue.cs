// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Analyzer.Utilities;
using Microsoft.CodeAnalysis.Operations.DataFlow.NullAnalysis;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.CodeAnalysis.Operations.DataFlow.PointsToAnalysis
{
    /// <summary>
    /// Abstract PointsTo value for an <see cref="AnalysisEntity"/>/<see cref="IOperation"/> tracked by <see cref="PointsToAnalysis"/>.
    /// It contains the set of possible <see cref="AbstractLocation"/>s that the entity or the operation can point to and the <see cref="Kind"/> of the location(s).
    /// </summary>
    internal class PointsToAbstractValue: CacheBasedEquatable<PointsToAbstractValue>
    {
        public static PointsToAbstractValue Undefined = new PointsToAbstractValue(PointsToAbstractValueKind.Undefined, NullAbstractValue.MaybeNull);
        public static PointsToAbstractValue Invalid = new PointsToAbstractValue(PointsToAbstractValueKind.Invalid, NullAbstractValue.Invalid);
        public static PointsToAbstractValue Unknown = new PointsToAbstractValue(PointsToAbstractValueKind.Unknown, NullAbstractValue.MaybeNull);
        public static PointsToAbstractValue NoLocation = new PointsToAbstractValue(ImmutableHashSet.Create(AbstractLocation.NoLocation), NullAbstractValue.NotNull, ImmutableHashSet<AnalysisEntity>.Empty);
        public static PointsToAbstractValue NullLocation = new PointsToAbstractValue(ImmutableHashSet.Create(AbstractLocation.Null), NullAbstractValue.Null, ImmutableHashSet<AnalysisEntity>.Empty);

        private PointsToAbstractValue(ImmutableHashSet<AbstractLocation> locations, NullAbstractValue nullState, ImmutableHashSet<AnalysisEntity> copyEntities, PointsToAbstractValueKind kind = PointsToAbstractValueKind.Known)
        {
            Debug.Assert(!locations.IsEmpty || !copyEntities.IsEmpty);
            Debug.Assert(locations.All(location => !location.IsNull) || nullState != NullAbstractValue.NotNull);
            Debug.Assert(nullState != NullAbstractValue.NotNull || locations.Any(location => !location.IsNull));
            Debug.Assert(nullState != NullAbstractValue.Undefined);
            Debug.Assert(kind == PointsToAbstractValueKind.Known || kind == PointsToAbstractValueKind.Invalid);
            Debug.Assert((kind == PointsToAbstractValueKind.Invalid) == (nullState == NullAbstractValue.Invalid));

            Kind = kind;
            Locations = locations;
            NullState = nullState;
            CopyEntities = copyEntities;
        }

        private PointsToAbstractValue(PointsToAbstractValueKind kind, NullAbstractValue nullState)
        {
            Debug.Assert(kind != PointsToAbstractValueKind.Known);
            Debug.Assert(nullState != NullAbstractValue.Null);
            Debug.Assert((kind == PointsToAbstractValueKind.Invalid) == (nullState == NullAbstractValue.Invalid));

            Kind = kind;
            Locations = ImmutableHashSet<AbstractLocation>.Empty;
            NullState = nullState;
            CopyEntities = ImmutableHashSet<AnalysisEntity>.Empty;
        }

        public static PointsToAbstractValue Create(AbstractLocation location, bool mayBeNull)
        {
            Debug.Assert(!location.IsNull, "Use 'PointsToAbstractValue.NullLocation' singleton");
            Debug.Assert(!location.IsNoLocation, "Use 'PointsToAbstractValue.NoLocation' singleton");

            return new PointsToAbstractValue(
                locations: ImmutableHashSet.Create(location),
                nullState: mayBeNull ? NullAbstractValue.MaybeNull : NullAbstractValue.NotNull,
                copyEntities: ImmutableHashSet<AnalysisEntity>.Empty);
        }

        public static PointsToAbstractValue Create(ImmutableHashSet<AbstractLocation> locations, NullAbstractValue nullState, ImmutableHashSet<AnalysisEntity> copyEntities)
        {
            Debug.Assert(!locations.IsEmpty || !copyEntities.IsEmpty);

            if (locations.Count == 1 && copyEntities.IsEmpty)
            {
                var location = locations.Single();
                if (location.IsNull)
                {
                    return NullLocation;
                }
                if (location.IsNoLocation)
                {
                    return NoLocation;
                }
            }

            return new PointsToAbstractValue(locations, nullState, copyEntities);
        }

        public PointsToAbstractValue MakeNonNull(IOperation operation)
        {
            if (NullState == NullAbstractValue.NotNull)
            {
                return this;
            }

            if (Kind != PointsToAbstractValueKind.Known)
            {
                return Create(AbstractLocation.CreateAllocationLocation(operation, operation.Type), mayBeNull: false);
            }

            var locations = Locations.Where(location => !location.IsNull).ToImmutableHashSet();
            if (locations.Count == Locations.Count)
            {
                locations = Locations;
            }

            return new PointsToAbstractValue(locations, NullAbstractValue.NotNull, CopyEntities);
        }

        public PointsToAbstractValue MakeNull()
        {
            if (NullState == NullAbstractValue.Null)
            {
                return this;
            }

            if (Kind != PointsToAbstractValueKind.Known)
            {
                return NullLocation;
            }

            return new PointsToAbstractValue(Locations, NullAbstractValue.Null, CopyEntities);
        }

        public PointsToAbstractValue MakeMayBeNull()
        {
            Debug.Assert(NullState != NullAbstractValue.Null);
            if (NullState == NullAbstractValue.MaybeNull || ReferenceEquals(this, Unknown))
            {
                return this;
            }
            else if (Locations.IsEmpty && CopyEntities.IsEmpty)
            {
                return Unknown;
            }

            Debug.Assert(Locations.All(location => !location.IsNull));
            return new PointsToAbstractValue(Locations, NullAbstractValue.MaybeNull, CopyEntities);
        }

        public PointsToAbstractValue MakeInvalid()
        {
            if (Kind == PointsToAbstractValueKind.Invalid)
            {
                return this;
            }

            return new PointsToAbstractValue(Locations, NullAbstractValue.Invalid, CopyEntities, kind: PointsToAbstractValueKind.Invalid);
        }

        public PointsToAbstractValue WithAddedCopyEntity(AnalysisEntity addedEntity, PointsToAbstractValue defaultValue)
        {
            var newCopyEntities = CopyEntities.Add(addedEntity);
            return WithCopyEntities(newCopyEntities, defaultValue);
        }

        public PointsToAbstractValue WithRemovedCopyEntity(AnalysisEntity removedEntity, PointsToAbstractValue defaultValue)
        {
            if (Kind == PointsToAbstractValueKind.Unknown)
            {
                Debug.Assert(!CopyEntities.Contains(removedEntity));
                return this;
            }

            var newCopyEntities = CopyEntities.Remove(removedEntity);
            return WithCopyEntities(newCopyEntities, defaultValue);
        }

        public PointsToAbstractValue WithCopyEntities(ImmutableHashSet<AnalysisEntity> newCopyEntities, PointsToAbstractValue defaultValue)
        {
            if (CopyEntities.SetEquals(newCopyEntities))
            {
                return this;
            }

            if (Locations.IsEmpty && newCopyEntities.IsEmpty)
            {
                return defaultValue;
            }

            return Create(Locations, NullState, newCopyEntities);
        }

        public ImmutableHashSet<AbstractLocation> Locations { get; }
        public PointsToAbstractValueKind Kind { get; }
        public NullAbstractValue NullState { get; }
        public ImmutableHashSet<AnalysisEntity> CopyEntities { get; }

        protected override int ComputeHashCode()
        {
            int hashCode = HashUtilities.Combine(Kind.GetHashCode(),
                HashUtilities.Combine(NullState.GetHashCode(), Locations.Count.GetHashCode()));
            foreach (var location in Locations)
            {
                hashCode = HashUtilities.Combine(location.GetHashCode(), hashCode);
            }

            hashCode = HashUtilities.Combine(CopyEntities.Count.GetHashCode(), hashCode);
            foreach (var entity in CopyEntities)
            {
                hashCode = HashUtilities.Combine(entity.GetHashCode(), hashCode);
            }

            return hashCode;
        }
    }
}
