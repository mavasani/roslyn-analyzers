// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;

namespace Microsoft.CodeAnalysis.Operations.DataFlow.StringContentAnalysis
{
    /// <summary>
    /// Abstract string content data value for <see cref="AnalysisEntity"/>/<see cref="IOperation"/> tracked by <see cref="StringContentAnalysis"/>.
    /// </summary>
    internal partial class StringContentAbstractValue : CacheBasedEquatable<StringContentAbstractValue>
    {
        public static readonly StringContentAbstractValue UndefinedState = new StringContentAbstractValue(ImmutableHashSet<object>.Empty, StringContainsNonLiteralState.Undefined);
        public static readonly StringContentAbstractValue InvalidState = new StringContentAbstractValue(ImmutableHashSet<object>.Empty, StringContainsNonLiteralState.Invalid);
        public static readonly StringContentAbstractValue MayBeContainsNonLiteralState = new StringContentAbstractValue(ImmutableHashSet<object>.Empty, StringContainsNonLiteralState.Maybe);
        public static readonly StringContentAbstractValue DoesNotContainLiteralOrNonLiteralState = new StringContentAbstractValue(ImmutableHashSet<object>.Empty, StringContainsNonLiteralState.No);
        private static readonly StringContentAbstractValue ContainsEmpyStringLiteralState = new StringContentAbstractValue(ImmutableHashSet.Create<object>(string.Empty), StringContainsNonLiteralState.No);
        private static readonly StringContentAbstractValue ContainsZeroIntergralLiteralState = new StringContentAbstractValue(ImmutableHashSet.Create<object>(0), StringContainsNonLiteralState.No);
        private static readonly StringContentAbstractValue ContainsTrueLiteralState = new StringContentAbstractValue(ImmutableHashSet.Create<object>(true), StringContainsNonLiteralState.No);
        private static readonly StringContentAbstractValue ContainsFalseLiteralState = new StringContentAbstractValue(ImmutableHashSet.Create<object>(false), StringContainsNonLiteralState.No);

        public static StringContentAbstractValue Create(object literal, ITypeSymbol type)
        {
            if (type.IsPrimitiveIntegralOrFloatType() &&
                DiagnosticHelpers.TryConvertToUInt64(literal, type.SpecialType, out ulong convertedValue) &&
                convertedValue == 0)
            {
                return ContainsZeroIntergralLiteralState;
            }

            switch (type.SpecialType)
            {
                case SpecialType.System_String:
                    if (((string)literal).Length == 0)
                    {
                        return ContainsEmpyStringLiteralState;
                    }

                    break;

                case SpecialType.System_Boolean:
                    return ((bool)literal) ? ContainsTrueLiteralState : ContainsFalseLiteralState;
            }

            return new StringContentAbstractValue(ImmutableHashSet.Create(literal), StringContainsNonLiteralState.No);
        }

        private StringContentAbstractValue(ImmutableHashSet<object> literalValues, StringContainsNonLiteralState nonLiteralState)
        {
            LiteralValues = literalValues;
            NonLiteralState = nonLiteralState;
        }

        private static StringContentAbstractValue Create(ImmutableHashSet<object> literalValues, StringContainsNonLiteralState nonLiteralState)
        {
            if (literalValues.IsEmpty)
            {
                switch (nonLiteralState)
                {
                    case StringContainsNonLiteralState.Undefined:
                        return UndefinedState;
                    case StringContainsNonLiteralState.Invalid:
                        return InvalidState;
                    case StringContainsNonLiteralState.No:
                        return DoesNotContainLiteralOrNonLiteralState;
                    default:
                        return MayBeContainsNonLiteralState;
                }
            }
            else if(literalValues.Count == 1 && nonLiteralState == StringContainsNonLiteralState.No)
            {
                switch (literalValues.Single())
                {
                    case bool boolVal:
                        return boolVal ? ContainsTrueLiteralState : ContainsFalseLiteralState;

                    case string stringVal:
                        if (stringVal.Length == 0)
                        {
                            return ContainsEmpyStringLiteralState;
                        }

                        break;

                    case int intValue:
                        if (intValue == 0)
                        {
                            return ContainsZeroIntergralLiteralState;
                        }

                        break;
                }
            }


            return new StringContentAbstractValue(literalValues, nonLiteralState);
        }

        /// <summary>
        /// Indicates if this string variable contains non literal string operands or not.
        /// </summary>
        public StringContainsNonLiteralState NonLiteralState { get; }

        /// <summary>
        /// Gets a collection of the string literals that could possibly make up the contents of this string <see cref="Operand"/>.
        /// </summary>
        public ImmutableHashSet<object> LiteralValues { get; }

        protected override int ComputeHashCode()
        {
            var hashCode = NonLiteralState.GetHashCode();
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

            ImmutableHashSet<object> mergedLiteralValues = LiteralValues.Union(otherState.LiteralValues);
            StringContainsNonLiteralState mergedNonLiteralState = Merge(NonLiteralState, otherState.NonLiteralState);
            return Create(mergedLiteralValues, mergedNonLiteralState);
        }

        private static StringContainsNonLiteralState Merge(StringContainsNonLiteralState value1, StringContainsNonLiteralState value2)
        {
            // + U I M N
            // U U U M N
            // I U I M N
            // M M M M M
            // N N N M N
            if (value1 == StringContainsNonLiteralState.Maybe || value2 == StringContainsNonLiteralState.Maybe)
            {
                return StringContainsNonLiteralState.Maybe;
            }
            else if (value1 == StringContainsNonLiteralState.Invalid || value1 == StringContainsNonLiteralState.Undefined)
            {
                return value2;
            }
            else if (value2 == StringContainsNonLiteralState.Invalid || value2 == StringContainsNonLiteralState.Undefined)
            {
                return value1;
            }

            Debug.Assert(value1 == StringContainsNonLiteralState.No);
            Debug.Assert(value2 == StringContainsNonLiteralState.No);
            return StringContainsNonLiteralState.No;
        }

        public bool IsLiteralState => !LiteralValues.IsEmpty && NonLiteralState == StringContainsNonLiteralState.No;

        public StringContentAbstractValue IntersectLiteralValues(StringContentAbstractValue value2)
        {
            Debug.Assert(IsLiteralState);
            Debug.Assert(value2.IsLiteralState);

            // Merge Literals
            var mergedLiteralValues = this.LiteralValues.Intersect(value2.LiteralValues);
            return mergedLiteralValues.IsEmpty ? InvalidState : new StringContentAbstractValue(mergedLiteralValues, StringContainsNonLiteralState.No);
        }

        /// <summary>
        /// Performs the union of this state and the other state for a Binary add operation
        /// and returns a new <see cref="StringContentAbstractValue"/> with the result.
        /// </summary>
        public StringContentAbstractValue MergeBinaryOperation(StringContentAbstractValue otherState, BinaryOperatorKind binaryOperatorKind, ITypeSymbol leftType, ITypeSymbol rightType)
        {
            if (otherState == null)
            {
                throw new ArgumentNullException(nameof(otherState));
            }

            // Merge Literals
            var builder = ImmutableHashSet.CreateBuilder<object>();
            foreach (var leftLiteral in LiteralValues)
            {
                foreach (var rightLiteral in otherState.LiteralValues)
                {
                    if (!TryMerge(leftLiteral, rightLiteral, binaryOperatorKind, leftType, rightType, out object result))
                    {
                        return MayBeContainsNonLiteralState;
                    }

                    builder.Add(result);
                }
            }

            ImmutableHashSet<object> mergedLiteralValues = builder.ToImmutable();
            StringContainsNonLiteralState mergedNonLiteralState = Merge(NonLiteralState, otherState.NonLiteralState);

            return new StringContentAbstractValue(mergedLiteralValues, mergedNonLiteralState);
        }

        /// <summary>
        /// Returns a string representation of <see cref="StringContentsState"/>.
        /// </summary>
        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "L({0}) NL:{1}", LiteralValues.Count, NonLiteralState.ToString()[0]);

        private static bool TryMerge(object value1, object value2, BinaryOperatorKind binaryOperatorKind, ITypeSymbol type1, ITypeSymbol type2, out object result)
        {
            result = null;

            switch (type1.SpecialType)
            {
                case SpecialType.System_String:
                    return type2.SpecialType == SpecialType.System_String &&
                        TryMerge((string)value1, (string)value2, binaryOperatorKind, out result);

                case SpecialType.System_Boolean:
                    return type2.SpecialType == SpecialType.System_Boolean &&
                        TryMerge((bool)value1, (bool)value2, binaryOperatorKind, out result);

                case SpecialType.System_Byte:
                case SpecialType.System_Int16:
                case SpecialType.System_Int32:
                case SpecialType.System_Int64:
                case SpecialType.System_UInt16:
                case SpecialType.System_UInt32:
                case SpecialType.System_SByte:
                    switch (type2.SpecialType)
                    {
                        case SpecialType.System_UInt64:
                            return TryMerge((ulong)value1, (ulong)value2, binaryOperatorKind, out result);

                        case SpecialType.System_Byte:
                        case SpecialType.System_Int16:
                        case SpecialType.System_Int32:
                        case SpecialType.System_Int64:
                        case SpecialType.System_UInt16:
                        case SpecialType.System_UInt32:
                        case SpecialType.System_SByte:
                            return TryMerge((long)value1, (long)value2, binaryOperatorKind, out result);
                    }

                    break;

                case SpecialType.System_UInt64:
                    switch (type2.SpecialType)
                    {
                        case SpecialType.System_Byte:
                        case SpecialType.System_Int16:
                        case SpecialType.System_Int32:
                        case SpecialType.System_Int64:
                        case SpecialType.System_UInt16:
                        case SpecialType.System_UInt32:
                        case SpecialType.System_UInt64:
                        case SpecialType.System_SByte:
                            return TryMerge((ulong)value1, (ulong)value2, binaryOperatorKind, out result);
                    }

                    break;

                case SpecialType.System_Double:
                case SpecialType.System_Single:
                    switch (type2.SpecialType)
                    {
                        case SpecialType.System_Single:
                        case SpecialType.System_Double:
                            return TryMerge((double)value1, (double)value2, binaryOperatorKind, out result);
                    }

                    break;
            }

            return false;
        }

        private static bool TryMerge(string value1, string value2, BinaryOperatorKind binaryOperatorKind, out object result)
        {
            if (value1 != null && value2 != null)
            {
                switch (binaryOperatorKind)
                {
                    case BinaryOperatorKind.Add:
                    case BinaryOperatorKind.Concatenate:
                        result = value1 + value2;
                        return true;
                }
            }

            result = null;
            return false;
        }

        private static bool TryMerge(bool value1, bool value2, BinaryOperatorKind binaryOperatorKind, out object result)
        {
            switch (binaryOperatorKind)
            {
                case BinaryOperatorKind.And:
                case BinaryOperatorKind.ConditionalAnd:
                    result = value1 && value2;
                    return true;

                case BinaryOperatorKind.Or:
                case BinaryOperatorKind.ConditionalOr:
                    result = value1 || value2;
                    return true;

                case BinaryOperatorKind.Equals:
                    result = value1 == value2;
                    return true;

                case BinaryOperatorKind.NotEquals:
                    result = value1 != value2;
                    return true;
            }

            result = null;
            return false;
        }

        private static bool TryMerge(long value1, long value2, BinaryOperatorKind binaryOperatorKind, out object result)
        {
            switch (binaryOperatorKind)
            {
                case BinaryOperatorKind.Add:
                    result = value1 + value2;
                    return true;

                case BinaryOperatorKind.Subtract:
                    result = value1 - value2;
                    return true;

                case BinaryOperatorKind.Multiply:
                    result = value1 * value2;
                    return true;

                case BinaryOperatorKind.Divide:
                    if (value2 != 0)
                    {
                        result = value1 / value2;
                        return true;
                    }

                    break;

                case BinaryOperatorKind.And:
                    result = value1 & value2;
                    return true;

                case BinaryOperatorKind.Or:
                    result = value1 | value2;
                    return true;

                case BinaryOperatorKind.Remainder:
                    result = value1 % value2;
                    return true;

                case BinaryOperatorKind.Power:
                    result = Math.Pow(value1, value2);
                    return true;

                case BinaryOperatorKind.LeftShift:
                    var intValue2 = (int)value2;
                    if (intValue2 == value2)
                    {
                        result = value1 << intValue2;
                        return true;
                    }

                    break;

                case BinaryOperatorKind.RightShift:
                    intValue2 = (int)value2;
                    if (intValue2 == value2)
                    {
                        result = value1 >> intValue2;
                        return true;
                    }

                    break;

                case BinaryOperatorKind.ExclusiveOr:
                    result = value1 ^ value2;
                    return true;

                case BinaryOperatorKind.Equals:
                    result = value1 == value2;
                    return true;

                case BinaryOperatorKind.NotEquals:
                    result = value1 != value2;
                    return true;

                case BinaryOperatorKind.LessThan:
                    result = value1 < value2;
                    return true;

                case BinaryOperatorKind.LessThanOrEqual:
                    result = value1 <= value2;
                    return true;

                case BinaryOperatorKind.GreaterThan:
                    result = value1 > value2;
                    return true;

                case BinaryOperatorKind.GreaterThanOrEqual:
                    result = value1 >= value2;
                    return true;
            }

            result = null;
            return false;
        }

        private static bool TryMerge(ulong value1, ulong value2, BinaryOperatorKind binaryOperatorKind, out object result)
        {
            switch (binaryOperatorKind)
            {
                case BinaryOperatorKind.Add:
                    result = value1 + value2;
                    return true;

                case BinaryOperatorKind.Subtract:
                    result = value1 - value2;
                    return true;

                case BinaryOperatorKind.Multiply:
                    result = value1 * value2;
                    return true;

                case BinaryOperatorKind.Divide:
                    if (value2 != 0)
                    {
                        result = value1 / value2;
                        return true;
                    }

                    break;

                case BinaryOperatorKind.And:
                    result = value1 & value2;
                    return true;

                case BinaryOperatorKind.Or:
                    result = value1 | value2;
                    return true;

                case BinaryOperatorKind.Remainder:
                    result = value1 % value2;
                    return true;

                case BinaryOperatorKind.Power:
                    result = Math.Pow(value1, value2);
                    return true;

                case BinaryOperatorKind.LeftShift:
                    if ((uint)value2 == value2)
                    {
                        result = value1 << (int)value2;
                        return true;
                    }

                    break;

                case BinaryOperatorKind.RightShift:
                    if ((uint)value2 == value2)
                    {
                        result = value1 >> (int)value2;
                        return true;
                    }

                    break;

                case BinaryOperatorKind.ExclusiveOr:
                    result = value1 ^ value2;
                    return true;

                case BinaryOperatorKind.Equals:
                    result = value1 == value2;
                    return true;

                case BinaryOperatorKind.NotEquals:
                    result = value1 != value2;
                    return true;

                case BinaryOperatorKind.LessThan:
                    result = value1 < value2;
                    return true;

                case BinaryOperatorKind.LessThanOrEqual:
                    result = value1 <= value2;
                    return true;

                case BinaryOperatorKind.GreaterThan:
                    result = value1 > value2;
                    return true;

                case BinaryOperatorKind.GreaterThanOrEqual:
                    result = value1 >= value2;
                    return true;
            }

            result = null;
            return false;
        }

        private static bool TryMerge(double value1, double value2, BinaryOperatorKind binaryOperatorKind, out object result)
        {
            switch (binaryOperatorKind)
            {
                case BinaryOperatorKind.Add:
                    result = value1 + value2;
                    return true;

                case BinaryOperatorKind.Subtract:
                    result = value1 - value2;
                    return true;

                case BinaryOperatorKind.Multiply:
                    result = value1 * value2;
                    return true;

                case BinaryOperatorKind.Divide:
                    if (value2 != 0)
                    {
                        result = value1 / value2;
                        return true;
                    }

                    break;

                case BinaryOperatorKind.Remainder:
                    result = value1 % value2;
                    return true;

                case BinaryOperatorKind.Power:
                    result = Math.Pow(value1, value2);
                    return true;

                case BinaryOperatorKind.Equals:
                    result = value1 == value2;
                    return true;

                case BinaryOperatorKind.NotEquals:
                    result = value1 != value2;
                    return true;

                case BinaryOperatorKind.LessThan:
                    result = value1 < value2;
                    return true;

                case BinaryOperatorKind.LessThanOrEqual:
                    result = value1 <= value2;
                    return true;

                case BinaryOperatorKind.GreaterThan:
                    result = value1 > value2;
                    return true;

                case BinaryOperatorKind.GreaterThanOrEqual:
                    result = value1 >= value2;
                    return true;
            }

            result = null;
            return false;
        }
    }
}
