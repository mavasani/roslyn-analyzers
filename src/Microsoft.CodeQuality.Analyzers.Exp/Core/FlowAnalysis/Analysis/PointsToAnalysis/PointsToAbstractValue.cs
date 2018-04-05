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
        public static PointsToAbstractValue NoLocation = new PointsToAbstractValue(PointsToAbstractValueKind.NoLocation, NullAbstractValue.NotNull);
        public static PointsToAbstractValue Unknown = new PointsToAbstractValue(PointsToAbstractValueKind.Unknown, NullAbstractValue.MaybeNull);
        public static PointsToAbstractValue Invalid = new PointsToAbstractValue(PointsToAbstractValueKind.Invalid, NullAbstractValue.Invalid);
        public static PointsToAbstractValue NullLocation = new PointsToAbstractValue(ImmutableHashSet.Create(AbstractLocation.Null), NullAbstractValue.Null);

        private PointsToAbstractValue(ImmutableHashSet<AbstractLocation> locations, NullAbstractValue nullState)
        {
            Debug.Assert(!locations.IsEmpty);
            Debug.Assert(locations.All(location => !location.IsNull) || nullState != NullAbstractValue.NotNull);
            Debug.Assert(nullState != NullAbstractValue.Null || locations.Single().IsNull);
            
            Locations = locations;
            Kind = PointsToAbstractValueKind.Known;
            NullState = nullState;
        }

        private PointsToAbstractValue(PointsToAbstractValueKind kind, NullAbstractValue nullState)
        {
            Debug.Assert(kind != PointsToAbstractValueKind.Known);
            Debug.Assert(nullState != NullAbstractValue.Null);

            Locations = ImmutableHashSet<AbstractLocation>.Empty;
            Kind = kind;
            NullState = nullState;
        }

        private static NullAbstractValue ComputeNullState(ImmutableHashSet<AbstractLocation> locations)
        {
            switch (locations.Count)
            {
                case 0:
                    throw new InvalidProgramException();

                case 1:
                    return locations.Single().IsNull ? NullAbstractValue.Null : NullAbstractValue.NotNull;

                default:
                    return locations.Any(location => location.IsNull) ? NullAbstractValue.MaybeNull : NullAbstractValue.NotNull;
            }
        }

        public static PointsToAbstractValue Create(AbstractLocation location, bool mayBeNull)
        {
            Debug.Assert(!location.IsNull, "Use 'PointsToAbstractValue.NullLocation' singleton");

            return mayBeNull ?
                new PointsToAbstractValue(ImmutableHashSet.Create(location, AbstractLocation.Null), NullAbstractValue.MaybeNull) :
                new PointsToAbstractValue(ImmutableHashSet.Create(location), NullAbstractValue.NotNull);
        }

        public static PointsToAbstractValue Create(ImmutableHashSet<AbstractLocation> locations)
        {
            Debug.Assert(!locations.IsEmpty);

            NullAbstractValue nullState = ComputeNullState(locations);
            if (nullState == NullAbstractValue.Null)
            {
                return NullLocation;
            }

            return new PointsToAbstractValue(locations, nullState);
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
            return new PointsToAbstractValue(locations, NullAbstractValue.NotNull);
        }

        public PointsToAbstractValue MakeMayBeNull()
        {
            Debug.Assert(NullState != NullAbstractValue.Null);
            if (NullState == NullAbstractValue.MaybeNull)
            {
                return this;
            }

            Debug.Assert(Locations.All(location => !location.IsNull));
            return new PointsToAbstractValue(Locations.Add(AbstractLocation.Null), NullAbstractValue.MaybeNull);
        }

        public ImmutableHashSet<AbstractLocation> Locations { get; }
        public PointsToAbstractValueKind Kind { get; }
        public NullAbstractValue NullState { get; }

        protected override int ComputeHashCode()
        {
            int hashCode = HashUtilities.Combine(Kind.GetHashCode(),
                HashUtilities.Combine(NullState.GetHashCode(), Locations.Count.GetHashCode()));
            foreach (var location in Locations)
            {
                hashCode = HashUtilities.Combine(location.GetHashCode(), hashCode);
            }

            return hashCode;
        }
    }
}
