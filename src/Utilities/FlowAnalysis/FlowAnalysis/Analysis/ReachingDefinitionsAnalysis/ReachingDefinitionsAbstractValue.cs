// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Globalization;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;

namespace Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.ReachingDefinitionsAnalysis
{
    /// <summary>
    /// Abstract value content data value for <see cref="AnalysisEntity"/>/<see cref="IOperation"/> tracked by <see cref="ReachingDefinitionsAnalysis"/>.
    /// </summary>
    public partial class ReachingDefinitionsAbstractValue : CacheBasedEquatable<ReachingDefinitionsAbstractValue>
    {
        public static ReachingDefinitionsAbstractValue Undefined { get; } = new ReachingDefinitionsAbstractValue(ImmutableHashSet<int>.Empty, ReachingDefinitionsAbstractValueKind.Undefined);
        public static ReachingDefinitionsAbstractValue Unknown { get; } = new ReachingDefinitionsAbstractValue(ImmutableHashSet<int>.Empty, ReachingDefinitionsAbstractValueKind.Unknown);

        private ReachingDefinitionsAbstractValue(ImmutableHashSet<int> definitions, ReachingDefinitionsAbstractValueKind kind)
        {
            Definitions = definitions;
            Kind = kind;
        }

        public ImmutableHashSet<int> Definitions { get; }

        public ReachingDefinitionsAbstractValueKind Kind { get; }

        internal static ReachingDefinitionsAbstractValue Create(int definition)
            => Create(ImmutableHashSet.Create(definition));

        internal static ReachingDefinitionsAbstractValue Create(ImmutableHashSet<int> definitions)
            => new(definitions, ReachingDefinitionsAbstractValueKind.Known);

        protected override void ComputeHashCodeParts(ref RoslynHashCode hashCode)
        {
            hashCode.Add(HashUtilities.Combine(Definitions));
            hashCode.Add(Kind.GetHashCode());
        }

        protected override bool ComputeEqualsByHashCodeParts(CacheBasedEquatable<ReachingDefinitionsAbstractValue> obj)
        {
            var other = (ReachingDefinitionsAbstractValue)obj;
            return HashUtilities.Combine(Definitions) == HashUtilities.Combine(other.Definitions)
                && Kind.GetHashCode() == other.Kind.GetHashCode();
        }

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "Defs({0}): '{1}'", Kind, string.Join(", ", Definitions.Order()));
    }
}
