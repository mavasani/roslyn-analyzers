// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

namespace Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.FlightEnabledAnalysis
{
    internal enum FlightEnabledAbstractValueKind
    {
        /// <summary>
        /// No flights enabled.
        /// </summary>
        Empty,

        /// <summary>
        /// One or more known set of flights enabled.
        /// </summary>
        Known,

        /// <summary>
        /// Unknown set of flights enabled.
        /// </summary>
        Unknown
    }
}