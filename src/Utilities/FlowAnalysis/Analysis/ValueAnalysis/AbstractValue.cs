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
    internal abstract partial class AbstractValue<TValue> : CacheBasedEquatable<TValue>
    {
        /// <summary>
        /// Performs the union of this state and the other state 
        /// and returns a new <see cref="TValue"/> with the result.
        /// </summary>
        public abstract AbstractValue<TValue> Merge(AbstractValue<TValue> otherState);
}
