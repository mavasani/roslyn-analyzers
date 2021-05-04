// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

namespace Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.ReachingDefinitionsAnalysis
{
    using System.Diagnostics;
    using ReachingDefinitionsAnalysisData = DictionaryAnalysisData<AnalysisEntity, ReachingDefinitionsAbstractValue>;
    using ReachingDefinitionsAnalysisResult = DataFlowAnalysisResult<ReachingDefinitionsBlockAnalysisResult, ReachingDefinitionsAbstractValue>;

    public partial class ReachingDefinitionsAnalysis : ForwardDataFlowAnalysis<ReachingDefinitionsAnalysisData, ReachingDefinitionsAnalysisContext, ReachingDefinitionsAnalysisResult, ReachingDefinitionsBlockAnalysisResult, ReachingDefinitionsAbstractValue>
    {
        /// <summary>
        /// Abstract value domain for <see cref="ReachingDefinitionsAnalysis"/> to merge and compare <see cref="ReachingDefinitionsAbstractValue"/> values.
        /// </summary>
        private sealed class ReachingDefinitionsAbstractValueDomain : AbstractValueDomain<ReachingDefinitionsAbstractValue>
        {
            public static ReachingDefinitionsAbstractValueDomain Default = new();
            private readonly SetAbstractDomain<int> _definitionsSetDomain = SetAbstractDomain<int>.Default;

            private ReachingDefinitionsAbstractValueDomain() { }

            public override ReachingDefinitionsAbstractValue Bottom => ReachingDefinitionsAbstractValue.Undefined;

            public override ReachingDefinitionsAbstractValue UnknownOrMayBeValue => ReachingDefinitionsAbstractValue.Unknown;

            public override int Compare(ReachingDefinitionsAbstractValue oldValue, ReachingDefinitionsAbstractValue newValue, bool assertMonotonicity)
            {
                if (ReferenceEquals(oldValue, newValue))
                {
                    return 0;
                }

                if (oldValue.Kind == newValue.Kind)
                {
                    return _definitionsSetDomain.Compare(oldValue.Definitions, newValue.Definitions);
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

            public override ReachingDefinitionsAbstractValue Merge(ReachingDefinitionsAbstractValue value1, ReachingDefinitionsAbstractValue value2)
            {
                if (value1 == null)
                {
                    return value2;
                }
                else if (value2 == null)
                {
                    return value1;
                }
                else if (value1.Kind == ReachingDefinitionsAbstractValueKind.Undefined)
                {
                    return value2;
                }
                else if (value2.Kind == ReachingDefinitionsAbstractValueKind.Undefined)
                {
                    return value1;
                }
                else if (value1.Kind == ReachingDefinitionsAbstractValueKind.Unknown || value2.Kind == ReachingDefinitionsAbstractValueKind.Unknown)
                {
                    return ReachingDefinitionsAbstractValue.Unknown;
                }

                var mergedDefinitions = _definitionsSetDomain.Merge(value1.Definitions, value2.Definitions);
                if (mergedDefinitions.Count == value1.Definitions.Count)
                {
                    Debug.Assert(_definitionsSetDomain.Equals(mergedDefinitions, value1.Definitions));
                    return value1;
                }
                else if (mergedDefinitions.Count == value2.Definitions.Count)
                {
                    Debug.Assert(_definitionsSetDomain.Equals(mergedDefinitions, value2.Definitions));
                    return value2;
                }

                return ReachingDefinitionsAbstractValue.Create(mergedDefinitions);
            }
        }
    }
}
