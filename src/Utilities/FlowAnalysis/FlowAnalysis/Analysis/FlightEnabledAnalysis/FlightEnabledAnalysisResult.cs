// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics;

namespace Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.FlightEnabledAnalysis
{
    /// <summary>
    /// Analysis result from execution of <see cref="FlightEnabledAnalysis"/> on a control flow graph.
    /// </summary>
    internal sealed class FlightEnabledAnalysisResult : DataFlowAnalysisResult<FlightEnabledBlockAnalysisResult, FlightEnabledAbstractValue>
    {
        internal FlightEnabledAnalysisResult(
            DataFlowAnalysisResult<FlightEnabledBlockAnalysisResult, FlightEnabledAbstractValue> coreFlightEnabledAnalysisResult,
            ImmutableHashSet<string> enabledFlightsForInvocationsAndPropertyAccessesOpt)
            : base(coreFlightEnabledAnalysisResult)
        {
            EnabledFlightsForInvocationsAndPropertyAccessesOpt = enabledFlightsForInvocationsAndPropertyAccessesOpt;
        }

        public ImmutableHashSet<string> EnabledFlightsForInvocationsAndPropertyAccessesOpt { get; }
    }
}
