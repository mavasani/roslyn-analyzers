// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;

namespace Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.ReachingDefinitionsAnalysis
{
    using ReachingDefinitionsAnalysisData = DictionaryAnalysisData<AnalysisEntity, ReachingDefinitionsAbstractValue>;
    using ReachingDefinitionsAnalysisResult = DataFlowAnalysisResult<ReachingDefinitionsBlockAnalysisResult, ReachingDefinitionsAbstractValue>;

    public partial class ReachingDefinitionsAnalysis : ForwardDataFlowAnalysis<ReachingDefinitionsAnalysisData, ReachingDefinitionsAnalysisContext, ReachingDefinitionsAnalysisResult, ReachingDefinitionsBlockAnalysisResult, ReachingDefinitionsAbstractValue>
    {
        /// <summary>
        /// Operation visitor to flow the data values across a given statement in a basic block.
        /// </summary>
        private sealed class ReachingDefinitionsDataFlowOperationVisitor : AnalysisEntityDataFlowOperationVisitor<ReachingDefinitionsAnalysisData, ReachingDefinitionsAnalysisContext, ReachingDefinitionsAnalysisResult, ReachingDefinitionsAbstractValue>
        {
            private int _currentDefinitionCount;
            private readonly Dictionary<IOperation, int> _operationToDefinitionMap;

            public ReachingDefinitionsDataFlowOperationVisitor(ReachingDefinitionsAnalysisContext analysisContext)
                : base(analysisContext)
            {
                _operationToDefinitionMap = new Dictionary<IOperation, int>();
            }

            protected sealed override bool SupportsPredicateAnalysis => false;

            protected override void ResetAbstractValue(AnalysisEntity analysisEntity)
                => SetAbstractValue(analysisEntity, ValueDomain.UnknownOrMayBeValue);

            protected override void SetAbstractValue(AnalysisEntity analysisEntity, ReachingDefinitionsAbstractValue value)
                => SetAbstractValue(CurrentAnalysisData, analysisEntity, value);

            private static void SetAbstractValue(ReachingDefinitionsAnalysisData analysisData, AnalysisEntity analysisEntity, ReachingDefinitionsAbstractValue value)
            {
                // PERF: Avoid creating an entry if the value is the default unknown value.
                if (value == ReachingDefinitionsAbstractValue.Unknown &&
                    !analysisData.ContainsKey(analysisEntity))
                {
                    return;
                }

                analysisData[analysisEntity] = value;
            }

            protected override bool HasAbstractValue(AnalysisEntity analysisEntity)
                => CurrentAnalysisData.ContainsKey(analysisEntity);

            protected override void StopTrackingEntity(AnalysisEntity analysisEntity, ReachingDefinitionsAnalysisData analysisData)
                => analysisData.Remove(analysisEntity);

            protected override ReachingDefinitionsAbstractValue GetAbstractValue(AnalysisEntity analysisEntity)
                => CurrentAnalysisData.TryGetValue(analysisEntity, out var value) ? value : ValueDomain.UnknownOrMayBeValue;

            protected override ReachingDefinitionsAbstractValue GetAbstractDefaultValue(ITypeSymbol type)
                => ReachingDefinitionsAbstractValue.Undefined;

            protected override bool HasAnyAbstractValue(ReachingDefinitionsAnalysisData data)
                => data.Count > 0;

            protected override void ResetCurrentAnalysisData()
            {
                foreach (var key in CurrentAnalysisData.Keys.ToImmutableArray())
                {
                    CurrentAnalysisData[key] = ValueDomain.UnknownOrMayBeValue;
                }
            }

            protected override void AddTrackedEntities(ReachingDefinitionsAnalysisData analysisData, HashSet<AnalysisEntity> builder, bool forInterproceduralAnalysis = false)
                => builder.AddRange(analysisData.Keys);

            protected override ReachingDefinitionsAnalysisData MergeAnalysisData(ReachingDefinitionsAnalysisData value1, ReachingDefinitionsAnalysisData value2)
                => ReachingDefinitionsAnalysisDomainInstance.Merge(value1, value2);
            protected override void UpdateValuesForAnalysisData(ReachingDefinitionsAnalysisData targetAnalysisData)
                => UpdateValuesForAnalysisData(targetAnalysisData, CurrentAnalysisData);
            protected override ReachingDefinitionsAnalysisData GetClonedAnalysisData(ReachingDefinitionsAnalysisData analysisData)
                => new(analysisData);
            public override ReachingDefinitionsAnalysisData GetEmptyAnalysisData()
                => new();
            protected override ReachingDefinitionsAnalysisData GetExitBlockOutputData(ReachingDefinitionsAnalysisResult analysisResult)
                => new(analysisResult.ExitBlockOutput.Data);
            protected override void ApplyMissingCurrentAnalysisDataForUnhandledExceptionData(ReachingDefinitionsAnalysisData dataAtException, ThrownExceptionInfo throwBranchWithExceptionType)
                => ApplyMissingCurrentAnalysisDataForUnhandledExceptionData(dataAtException, CurrentAnalysisData, throwBranchWithExceptionType);
            protected override bool Equals(ReachingDefinitionsAnalysisData value1, ReachingDefinitionsAnalysisData value2)
                => value1.Equals(value2);
            protected override void ApplyInterproceduralAnalysisResultCore(ReachingDefinitionsAnalysisData resultData)
                => ApplyInterproceduralAnalysisResultHelper(resultData);
            protected override ReachingDefinitionsAnalysisData GetTrimmedCurrentAnalysisData(IEnumerable<AnalysisEntity> withEntities)
                => GetTrimmedCurrentAnalysisDataHelper(withEntities, CurrentAnalysisData, SetAbstractValue);

            #region Visitor methods

            protected override void SetAbstractValueForAssignment(AnalysisEntity targetAnalysisEntity, IOperation? assignedValueOperation, ReachingDefinitionsAbstractValue assignedValue)
            {
                if (assignedValueOperation != null)
                {
                    if (!_operationToDefinitionMap.TryGetValue(assignedValueOperation, out var definition))
                    {
                        definition = _currentDefinitionCount;
                        _operationToDefinitionMap[assignedValueOperation] = definition;
                        _currentDefinitionCount++;
                    }

                    SetAbstractValue(targetAnalysisEntity, ReachingDefinitionsAbstractValue.Create(definition));
                }
            }

            #endregion
        }
    }
}
