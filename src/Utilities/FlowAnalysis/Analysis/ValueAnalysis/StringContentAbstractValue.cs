// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.ValueContentAnalysis
{
    /// <summary>
    /// Abstract string content data value for <see cref="AnalysisEntity"/>/<see cref="IOperation"/> tracked by <see cref="ValueContentAnalysis"/>.
    /// </summary>
    internal class StringContentAbstractValue : AbstractPrimitiveValue<StringContentAbstractValue, string>
    {
        public static readonly StringContentAbstractValue UndefinedState = new StringContentAbstractValue(ImmutableHashSet<string>.Empty, ValueContainsNonLiteralState.Undefined);
        public static readonly StringContentAbstractValue InvalidState = new StringContentAbstractValue(ImmutableHashSet<string>.Empty, ValueContainsNonLiteralState.Invalid);
        public static readonly StringContentAbstractValue MayBeContainsNonLiteralState = new StringContentAbstractValue(ImmutableHashSet<string>.Empty, ValueContainsNonLiteralState.Maybe);
        public static readonly StringContentAbstractValue DoesNotContainLiteralOrNonLiteralState = new StringContentAbstractValue(ImmutableHashSet<string>.Empty, ValueContainsNonLiteralState.No);
        private static readonly StringContentAbstractValue ContainsEmpyStringLiteralState = new StringContentAbstractValue(ImmutableHashSet.Create(string.Empty), ValueContainsNonLiteralState.No);

        private StringContentAbstractValue(ImmutableHashSet<string> literalValues, ValueContainsNonLiteralState nonLiteralState)
            : base(literalValues, nonLiteralState)
        {
        }

        protected override AbstractPrimitiveValue<StringContentAbstractValue, string> MayBeContainsNonLiteralPrimitiveState => MayBeContainsNonLiteralState;

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
            else if (nonLiteralState == ValueContainsNonLiteralState.No &&
                literalValues.Count == 1 &&
                literalValues.Single().Length == 0)
            {
                return ContainsEmpyStringLiteralState;
            }
            else
            {
                return new StringContentAbstractValue(literalValues, nonLiteralState);
            }
        }

        protected override AbstractPrimitiveValue<StringContentAbstractValue, string> CreateMergedOrIntersectedValue(ImmutableHashSet<string> literalValues, ValueContainsNonLiteralState nonLiteralState)
            => Create(literalValues, nonLiteralState);

        protected override bool IsSupportedBinaryOperatorForMerge(BinaryOperatorKind binaryOperatorKind)
        {
            switch (binaryOperatorKind)
            {
                case BinaryOperatorKind.Add:
                case BinaryOperatorKind.Concatenate:
                    return true;

                default:
                    return false;
            }
        }

        protected override string MergeBinaryOperator(string leftLiteral, string rightLiteral, BinaryOperatorKind binaryOperatorKind)
        {
            Debug.Assert(IsSupportedBinaryOperatorForMerge(binaryOperatorKind));
            return leftLiteral + rightLiteral;
        }
    }
}
