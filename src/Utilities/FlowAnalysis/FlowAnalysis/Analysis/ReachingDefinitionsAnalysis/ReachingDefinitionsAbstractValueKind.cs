// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

namespace Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.ReachingDefinitionsAnalysis
{
    /// <summary>
    /// Kind for the <see cref="ReachingDefinitionsAbstractValue"/>.
    /// </summary>
    public enum ReachingDefinitionsAbstractValueKind
    {
        /// <summary>
        /// Undefined value.
        /// </summary>
        Undefined,

        /// <summary>
        /// One or more known reaching definitions.
        /// </summary>
        Known,

        /// <summary>
        /// Unknown reaching definitions.
        /// </summary>
        Unknown,
    }
}
