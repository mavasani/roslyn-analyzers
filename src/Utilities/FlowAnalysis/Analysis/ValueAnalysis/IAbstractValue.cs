// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.


namespace Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.ValueContentAnalysis
{
    /// <summary>
    /// Abstract data value for <see cref="AnalysisEntity"/>/<see cref="IOperation"/> tracked by <see cref="ValueContentAnalysis"/>.
    /// </summary>
    internal interface IAbstractValue
    {
        /// <summary>
        /// Performs the union of this state and the other state 
        /// and returns a new <see cref="TValue"/> with the result.
        /// </summary>
        IAbstractValue Merge(IAbstractValue otherState);

        IAbstractValue MayBeOrUnknownValue { get; }
    }
}
