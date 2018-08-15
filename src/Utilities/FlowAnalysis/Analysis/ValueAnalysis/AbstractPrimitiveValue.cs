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
    internal abstract partial class AbstractPrimitiveValue<TValue, TPrimitiveType>: AbstractValue<TValue>
        where TPrimitiveType : IEquatable<TPrimitiveType>
    {
        public abstract TValue UndefinedState { get; }
        public abstract TValue InvalidState { get; }
        public abstract TValue MayBeContainsNonLiteralState { get; }
        public abstract TValue DoesNotContainLiteralOrNonLiteralState { get; }
        //private static readonly TValue ContainsEmpyStringLiteralState { get; }

        protected abstract AbstractPrimitiveValue<TValue, TPrimitiveType> Create(ImmutableHashSet<TPrimitiveType> literalValues, ValueContainsNonLiteralState nonLiteralState);

        protected AbstractPrimitiveValue(ImmutableHashSet<TPrimitiveType> literalValues, ValueContainsNonLiteralState nonLiteralState)
        {
            LiteralValues = literalValues;
            NonLiteralState = nonLiteralState;
        }

        /// <summary>
        /// Indicates if this variable contains non-literal operands or not.
        /// </summary>
        public ValueContainsNonLiteralState NonLiteralState { get; }

        /// <summary>
        /// Gets a collection of the literals that could possibly make up the contents of this <see cref="Operand"/>.
        /// </summary>
        public ImmutableHashSet<TPrimitiveType> LiteralValues { get; }

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
        /// and returns a new <see cref="TValue"/> with the result.
        /// </summary>
        public override AbstractValue<TValue> Merge(AbstractValue<TValue> otherState)
        {
            if (otherState == null)
            {
                throw new ArgumentNullException(nameof(otherState));
            }

            var otherPrimitiveState = (AbstractPrimitiveValue<TValue, TPrimitiveType>)otherState;

            ImmutableHashSet<TPrimitiveType> mergedLiteralValues = LiteralValues.Union(otherPrimitiveState.LiteralValues);
            ValueContainsNonLiteralState mergedNonLiteralState = Merge(NonLiteralState, otherPrimitiveState.NonLiteralState);
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

        public AbstractPrimitiveValue<TValue, TPrimitiveType> IntersectLiteralValues(AbstractPrimitiveValue<TValue, TPrimitiveType> value2)
        {
            Debug.Assert(IsLiteralState);
            Debug.Assert(value2.IsLiteralState);

            // Merge Literals
            var mergedLiteralValues = this.LiteralValues.Intersect(value2.LiteralValues);
            return mergedLiteralValues.IsEmpty ? InvalidState : Create(mergedLiteralValues, ValueContainsNonLiteralState.No);
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
        /// Returns a string representation of the value.
        /// </summary>
        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "L({0}) NL:{1}", LiteralValues.Count, NonLiteralState.ToString()[0]);
    }
}
