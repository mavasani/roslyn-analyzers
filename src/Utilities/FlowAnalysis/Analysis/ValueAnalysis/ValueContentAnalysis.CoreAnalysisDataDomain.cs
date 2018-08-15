// StringContentright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

namespace Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.ValueContentAnalysis
{
    internal partial class ValueContentAnalysis : ForwardDataFlowAnalysis<ValueContentAnalysisData, ValueContentBlockAnalysisResult, IAbstractValue>
    {
        /// <summary>
        /// An abstract analysis domain implementation for core analysis data tracked by <see cref="ValueContentAnalysis"/>.
        /// </summary>
        private abstract class CoreAnalysisDataDomain : AnalysisEntityMapAbstractDomain<IAbstractValue>
        {
            public static readonly CoreAnalysisDataDomain Instance = new CoreAnalysisDataDomain(ValueContentAbstractValueDomain.Default);

            protected CoreAnalysisDataDomain(AbstractValueDomain<IAbstractValue> valueDomain) : base(valueDomain)
            {
            }

            protected override IAbstractValue GetDefaultValue(AnalysisEntity analysisEntity) => IAbstractValue;
            protected override bool CanSkipNewEntry(AnalysisEntity analysisEntity, IAbstractValue value) => value.NonLiteralState == ValueContainsNonLiteralState.Maybe;
        }
    }
}