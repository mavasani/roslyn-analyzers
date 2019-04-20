// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Diagnostics;

namespace Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.FlightEnabledAnalysis
{
    using FlightEnabledAnalysisData = DictionaryAnalysisData<AnalysisEntity, FlightEnabledAbstractValue>;

    internal partial class FlightEnabledAnalysis : ForwardDataFlowAnalysis<FlightEnabledAnalysisData, FlightEnabledAnalysisContext, FlightEnabledAnalysisResult, FlightEnabledBlockAnalysisResult, FlightEnabledAbstractValue>
    {
        private class FlightEnabledAbstractValueDomain : AbstractValueDomain<FlightEnabledAbstractValue>
        {
            public static FlightEnabledAbstractValueDomain Instance = new FlightEnabledAbstractValueDomain();
            private readonly SetAbstractDomain<string> _enabledFlightsDomain = SetAbstractDomain<string>.Default;

            private FlightEnabledAbstractValueDomain() { }

            public override FlightEnabledAbstractValue Bottom => FlightEnabledAbstractValue.Empty;

            public override FlightEnabledAbstractValue UnknownOrMayBeValue => FlightEnabledAbstractValue.Unknown;

            public override int Compare(FlightEnabledAbstractValue oldValue, FlightEnabledAbstractValue newValue, bool assertMonotonicity)
            {
                Debug.Assert(oldValue != null);
                Debug.Assert(newValue != null);

                if (ReferenceEquals(oldValue, newValue))
                {
                    return 0;
                }

                if (oldValue.Kind == newValue.Kind)
                {
                    return _enabledFlightsDomain.Compare(oldValue.EnabledFlights, newValue.EnabledFlights);
                }
                else if (oldValue.Kind < newValue.Kind)
                {
                    return -1;
                }
                else
                {
                    FireNonMonotonicAssertIfNeeded(assertMonotonicity);
                    return 1;
                }
            }

            public override FlightEnabledAbstractValue Merge(FlightEnabledAbstractValue value1, FlightEnabledAbstractValue value2)
            {
                if (value1 == null)
                {
                    return value2;
                }
                else if (value2 == null)
                {
                    return value1;
                }
                else if (value1.Kind == FlightEnabledAbstractValueKind.Unknown || value2.Kind == FlightEnabledAbstractValueKind.Unknown)
                {
                    return FlightEnabledAbstractValue.Unknown;
                }

                var enabledFlights = _enabledFlightsDomain.Intersect(value1.EnabledFlights, value2.EnabledFlights);
                if (enabledFlights.IsEmpty)
                {
                    return FlightEnabledAbstractValue.Empty;
                }

                return new FlightEnabledAbstractValue(enabledFlights);
            }
        }
    }
}
