// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Analyzer.Utilities;

namespace Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.ValueContentAnalysis
{
    /// <summary>
    /// Abstract string content data value for <see cref="AnalysisEntity"/>/<see cref="IOperation"/> tracked by <see cref="StringContentAnalysis"/>.
    /// </summary>
    internal partial class StringContentAbstractValue : AbstractValue<StringContentAbstractValue>
    {
        public static readonly StringContentAbstractValue UndefinedState = new StringContentAbstractValue(ImmutableHashSet<string>.Empty, ValueContainsNonLiteralState.Undefined);
        public static readonly StringContentAbstractValue InvalidState = new StringContentAbstractValue(ImmutableHashSet<string>.Empty, ValueContainsNonLiteralState.Invalid);
        public static readonly StringContentAbstractValue MayBeContainsNonLiteralState = new StringContentAbstractValue(ImmutableHashSet<string>.Empty, ValueContainsNonLiteralState.Maybe);
        public static readonly StringContentAbstractValue DoesNotContainLiteralOrNonLiteralState = new StringContentAbstractValue(ImmutableHashSet<string>.Empty, ValueContainsNonLiteralState.No);
        private static readonly StringContentAbstractValue ContainsEmpyStringLiteralState = new StringContentAbstractValue(ImmutableHashSet.Create(string.Empty), ValueContainsNonLiteralState.No);

        public static StringContentAbstractValue Create(string literal)
        {
            if (literal.Length > 0)
            {
                return new StringContentAbstractValue(ImmutableHashSet.Create(literal), ValueContainsNonLiteralState.No);
            }
            else
            {
                return ContainsEmpyStringLiteralState;
            }
        }

        private StringContentAbstractValue(ImmutableHashSet<string> literalValues, ValueContainsNonLiteralState nonLiteralState)
        {
            LiteralValues = literalValues;
            NonLiteralState = nonLiteralState;
        }

        private static StringContentAbstractValue Create(ImmutableHashSet<string> literalValues, ValueContainsNonLiteralState nonLiteralState)
        {
            if (literalValues.IsEmpty)
            {
                switch (nonLiteralState)
                {
                    case ValueContainsNonLiteralState.Undefined:
                        return UndefinedState;
                    case ValueContainsNonLiteralState.Invalid:
                        return InvalidState;
                    case ValueContainsNonLiteralState.No:
                        return DoesNotContainLiteralOrNonLiteralState;
                    default:
                        return MayBeContainsNonLiteralState;
                }
            }

            return new StringContentAbstractValue(literalValues, nonLiteralState);
        }

        /// <summary>
        /// Indicates if this string variable contains non literal string operands or not.
        /// </summary>
        public ValueContainsNonLiteralState NonLiteralState { get; }

        /// <summary>
        /// Gets a collection of the string literals that could possibly make up the contents of this string <see cref="Operand"/>.
        /// </summary>
        public ImmutableHashSet<string> LiteralValues { get; }

        protected override int ComputeHashCode()
        {
            var hashCode = HashUtilities.Combine(NonLiteralState.GetHashCode(), LiteralValues.Count.GetHashCode());
            foreach (var literal in LiteralValues.OrderBy(s => s))
            {
                hashCode = HashUtilities.Combine(hashCode, literal.GetHashCode());
            }

            return hashCode;
        }

        /// <summary>
        /// Performs the union of this state and the other state 
        /// and returns a new <see cref="StringContentAbstractValue"/> with the result.
        /// </summary>
        public StringContentAbstractValue Merge(StringContentAbstractValue otherState)
        {
            if (otherState == null)
            {
                throw new ArgumentNullException(nameof(otherState));
            }

            ImmutableHashSet<string> mergedLiteralValues = LiteralValues.Union(otherState.LiteralValues);
            ValueContainsNonLiteralState mergedNonLiteralState = Merge(NonLiteralState, otherState.NonLiteralState);
            return Create(mergedLiteralValues, mergedNonLiteralState);
        }

        private static ValueContainsNonLiteralState Merge(ValueContainsNonLiteralState value1, ValueContainsNonLiteralState value2)
        {
            // + U I M N
            // U U U M N
            // I U I M N
            // M M M M M
            // N N N M N
            if (value1 == ValueContainsNonLiteralState.Maybe || value2 == ValueContainsNonLiteralState.Maybe)
            {
                return ValueContainsNonLiteralState.Maybe;
            }
            else if (value1 == ValueContainsNonLiteralState.Invalid || value1 == ValueContainsNonLiteralState.Undefined)
            {
                return value2;
            }
            else if (value2 == ValueContainsNonLiteralState.Invalid || value2 == ValueContainsNonLiteralState.Undefined)
            {
                return value1;
            }

            Debug.Assert(value1 == ValueContainsNonLiteralState.No);
            Debug.Assert(value2 == ValueContainsNonLiteralState.No);
            return ValueContainsNonLiteralState.No;
        }

        public bool IsLiteralState => !LiteralValues.IsEmpty && NonLiteralState == ValueContainsNonLiteralState.No;

        public StringContentAbstractValue IntersectLiteralValues(StringContentAbstractValue value2)
        {
            Debug.Assert(IsLiteralState);
            Debug.Assert(value2.IsLiteralState);

            // Merge Literals
            var mergedLiteralValues = this.LiteralValues.Intersect(value2.LiteralValues);
            return mergedLiteralValues.IsEmpty ? InvalidState : new StringContentAbstractValue(mergedLiteralValues, ValueContainsNonLiteralState.No);
        }

        /// <summary>
        /// Performs the union of this state and the other state for a Binary add operation
        /// and returns a new <see cref="StringContentAbstractValue"/> with the result.
        /// </summary>
        public StringContentAbstractValue MergeBinaryAdd(StringContentAbstractValue otherState)
        {
            if (otherState == null)
            {
                throw new ArgumentNullException(nameof(otherState));
            }

            // Merge Literals
            var builder = ImmutableHashSet.CreateBuilder<string>();
            foreach (var leftLiteral in LiteralValues)
            {
                foreach (var rightLiteral in otherState.LiteralValues)
                {
                    builder.Add(leftLiteral + rightLiteral);
                }
            }

            ImmutableHashSet<string> mergedLiteralValues = builder.ToImmutable();
            ValueContainsNonLiteralState mergedNonLiteralState = Merge(NonLiteralState, otherState.NonLiteralState);

            return new StringContentAbstractValue(mergedLiteralValues, mergedNonLiteralState);
        }

        /// <summary>
        /// Returns a string representation of <see cref="StringContentsState"/>.
        /// </summary>
        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "L({0}) NL:{1}", LiteralValues.Count, NonLiteralState.ToString()[0]);
    }
}
