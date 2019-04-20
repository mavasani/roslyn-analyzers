/*// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

namespace Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.FlightEnabledAnalysis
{
    internal sealed class FlightEnabledAnalysisData : AbstractAnalysisData
    {
        public static readonly FlightEnabledAnalysisData Empty = new FlightEnabledAnalysisData(FlightEnabledAbstractValue.Empty);
        public static readonly FlightEnabledAnalysisData Unknown = new FlightEnabledAnalysisData(FlightEnabledAbstractValue.Unknown);

        public FlightEnabledAnalysisData(FlightEnabledAbstractValue flightEnabledValue)
        {
            FlightEnabledValue = flightEnabledValue;
        }

        public FlightEnabledAbstractValue FlightEnabledValue { get; }
    }
}
*/