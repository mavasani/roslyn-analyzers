// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.PooledObjects;

#pragma warning disable CA1067 // Override Object.Equals(object) when implementing IEquatable<T> - CacheBasedEquatable handles equality

namespace Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.FlightEnabledAnalysis
{
    internal sealed class FlightEnabledAbstractValue : CacheBasedEquatable<FlightEnabledAbstractValue>
    {
        public static readonly FlightEnabledAbstractValue Empty = new FlightEnabledAbstractValue(ImmutableHashSet<string>.Empty, FlightEnabledAbstractValueKind.Empty);
        public static readonly FlightEnabledAbstractValue Unknown = new FlightEnabledAbstractValue(ImmutableHashSet<string>.Empty, FlightEnabledAbstractValueKind.Unknown);

        private FlightEnabledAbstractValue(ImmutableHashSet<string> enabledFlights, FlightEnabledAbstractValueKind kind)
        {
            Debug.Assert(enabledFlights != null);
            Debug.Assert((!enabledFlights.IsEmpty) == (kind == FlightEnabledAbstractValueKind.Known));
            Debug.Assert(enabledFlights.All(enabledFlight => enabledFlight != null));

            EnabledFlights = enabledFlights;
            Kind = kind;
        }

        public FlightEnabledAbstractValue(string enabledFlight)
            : this(ImmutableHashSet.Create(enabledFlight), FlightEnabledAbstractValueKind.Known)
        {
            Debug.Assert(enabledFlight != null);
        }

        public FlightEnabledAbstractValue(ImmutableHashSet<string> enabledFlights)
            : this(enabledFlights, FlightEnabledAbstractValueKind.Known)
        {
            Debug.Assert(!enabledFlights.IsEmpty);
        }

        public ImmutableHashSet<string> EnabledFlights { get; }
        public FlightEnabledAbstractValueKind Kind { get; }

        internal FlightEnabledAbstractValue WithNewEnabledFlight(string enabledFlight)
        {
            if (EnabledFlights.Contains(enabledFlight))
            {
                return this;
            }

            return new FlightEnabledAbstractValue(EnabledFlights.Add(enabledFlight), FlightEnabledAbstractValueKind.Known);
        }

        internal FlightEnabledAbstractValue WithNewDisabledFlight(string disabledFlight)
        {
            if (!EnabledFlights.Contains(disabledFlight))
            {
                return this;
            }

            var newEnableFlights = EnabledFlights.Remove(disabledFlight);
            if (newEnableFlights.IsEmpty)
            {
                return Empty;
            }

            return new FlightEnabledAbstractValue(newEnableFlights, FlightEnabledAbstractValueKind.Known);
        }

        protected override void ComputeHashCodeParts(ArrayBuilder<int> builder)
        {
            builder.Add(HashUtilities.Combine(EnabledFlights));
            builder.Add(Kind.GetHashCode());
        }
    }
}
