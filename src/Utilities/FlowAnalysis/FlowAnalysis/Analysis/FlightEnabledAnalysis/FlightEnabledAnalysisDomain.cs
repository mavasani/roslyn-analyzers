/*// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

namespace Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.FlightEnabledAnalysis
{
    using FlightEnabledAnalysisData = DictionaryAnalysisData<AnalysisEntity, FlightEnabledAbstractValue>;

    internal partial class FlightEnabledAnalysis : ForwardDataFlowAnalysis<FlightEnabledAnalysisData, FlightEnabledAnalysisContext, FlightEnabledAnalysisResult, FlightEnabledBlockAnalysisResult, FlightEnabledAbstractValue>
    {
        internal sealed class FlightEnabledAnalysisDomain : AbstractAnalysisDomain<FlightEnabledAnalysisData>
        {
            public static readonly FlightEnabledAnalysisDomain Instance = new FlightEnabledAnalysisDomain();

            private FlightEnabledAnalysisDomain() { }

            public override FlightEnabledAnalysisData Clone(FlightEnabledAnalysisData value)
               => value;

            public override int Compare(FlightEnabledAnalysisData oldValue, FlightEnabledAnalysisData newValue)
               => FlightEnabledAbstractValueDomain.Instance.Compare(oldValue.FlightEnabledValue, newValue.FlightEnabledValue);

            public override bool Equals(FlightEnabledAnalysisData value1, FlightEnabledAnalysisData value2)
                => value1 == value2;

            public override FlightEnabledAnalysisData Merge(FlightEnabledAnalysisData value1, FlightEnabledAnalysisData value2)
            {
                
            }
        }
    }
}
*/