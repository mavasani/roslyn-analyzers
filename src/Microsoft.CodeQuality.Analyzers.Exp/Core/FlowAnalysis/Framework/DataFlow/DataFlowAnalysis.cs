// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Analyzer.Utilities.Extensions;
using static Microsoft.CodeAnalysis.Operations.ControlFlowGraph;

namespace Microsoft.CodeAnalysis.Operations.DataFlow
{
    /// <summary>
    /// Subtype for all dataflow analyses on a control flow graph.
    /// It performs a worklist based approach to flow abstract data values for <see cref="AnalysisEntity"/>/<see cref="IOperation"/> across the basic blocks until a fix point is reached.
    /// </summary>
    internal abstract class DataFlowAnalysis<TAnalysisData, TAnalysisResult, TAbstractAnalysisValue>
        where TAnalysisData : class
        where TAnalysisResult : AbstractBlockAnalysisResult
    {
        private static readonly ConditionalWeakTable<IOperation, ConcurrentDictionary<DataFlowOperationVisitor<TAnalysisData, TAbstractAnalysisValue>, DataFlowAnalysisResult<TAnalysisResult, TAbstractAnalysisValue>>> s_resultCache =
            new ConditionalWeakTable<IOperation, ConcurrentDictionary<DataFlowOperationVisitor<TAnalysisData, TAbstractAnalysisValue>, DataFlowAnalysisResult<TAnalysisResult, TAbstractAnalysisValue>>>();

        protected DataFlowAnalysis(AbstractAnalysisDomain<TAnalysisData> analysisDomain, DataFlowOperationVisitor<TAnalysisData, TAbstractAnalysisValue> operationVisitor)
        {
            AnalysisDomain = analysisDomain;
            OperationVisitor = operationVisitor;
        }

        protected AbstractAnalysisDomain<TAnalysisData> AnalysisDomain { get; }
        protected DataFlowOperationVisitor<TAnalysisData, TAbstractAnalysisValue> OperationVisitor { get; }
        private Dictionary<Region, TAnalysisData> MergedInputAnalysisDataForFinallyRegions { get; set; }

        protected DataFlowAnalysisResult<TAnalysisResult, TAbstractAnalysisValue> GetOrComputeResultCore(
            ControlFlowGraph cfg,
            IOperation rootOperation,
            bool cacheResult,
            DataFlowAnalysisResult<TAnalysisResult, TAbstractAnalysisValue> seedResultOpt = null)
        {
            if (cfg == null)
            {
                throw new ArgumentNullException(nameof(cfg));
            }

            if (rootOperation == null)
            {
                throw new ArgumentNullException(nameof(rootOperation));
            }

            if (!cacheResult)
            {
                return Run(cfg, seedResultOpt: null);
            }

            var analysisResultsMap = s_resultCache.GetOrCreateValue(rootOperation);
            return analysisResultsMap.GetOrAdd(OperationVisitor, _ => Run(cfg, seedResultOpt: null));
        }

        private DataFlowAnalysisResult<TAnalysisResult, TAbstractAnalysisValue> Run(ControlFlowGraph cfg, DataFlowAnalysisResult<TAnalysisResult, TAbstractAnalysisValue> seedResultOpt)
        {
            var resultBuilder = new DataFlowAnalysisResultBuilder<TAnalysisData>();
            var uniqueSuccessors = new HashSet<BasicBlock>();
            var ordinalToBlockMap = new Dictionary<int, BasicBlock>();
            var finallyOrCatchBlockSuccessorsMap = new Dictionary<int, List<BranchWithInfo>>();
            var catchBlockInputDataMap = new Dictionary<Region, TAnalysisData>();

            // Add each basic block to the result.
            foreach (var block in cfg.Blocks)
            {
                resultBuilder.Add(block);
                ordinalToBlockMap.Add(block.Ordinal, block);
            }

            var worklist = new Queue<BasicBlock>();
            var pendingBlocksNeedingAtLeastOnePass = new HashSet<BasicBlock>(cfg.Blocks);
            var entry = cfg.GetEntry();

            // Are we computing the analysis data from scratch?
            if (seedResultOpt == null)
            {
                // Initialize the input of the initial block
                // with the default abstract value of the domain.
                UpdateInput(resultBuilder, entry, AnalysisDomain.Bottom);
            }
            else
            {
                // Initialize the input and output of every block
                // with the previously computed value.
                foreach (var block in cfg.Blocks)
                {
                    UpdateInput(resultBuilder, block, GetInputData(seedResultOpt[block]));
                }
            }

            // Add the block to the worklist.
            worklist.Enqueue(entry);

            while (worklist.Count > 0 || pendingBlocksNeedingAtLeastOnePass.Count > 0)
            {
                // Get the next block to process from the worklist.
                // If worklist is empty, get any one of the pendingBlocksNeedingAtLeastOnePass, which must be unreachable from Entry block.
                var block = worklist.Count > 0 ? worklist.Dequeue() : pendingBlocksNeedingAtLeastOnePass.ElementAt(0);

                // We process the block only if all its predecessor blocks have been processed once.
                if (HasUnprocessedPredecessorBlock(block))
                {
                    continue;
                }

                var needsAtLeastOnePass = pendingBlocksNeedingAtLeastOnePass.Remove(block);

                // Get the input data for the block.
                var input = GetInput(resultBuilder[block]);
                if (input == null)
                {
                    Debug.Assert(needsAtLeastOnePass);

                    Region enclosingTryAndCatchRegion = GetEnclosingTryAndCatchRegionIfStartsHandler(block);
                    if (enclosingTryAndCatchRegion != null)
                    {
                        Debug.Assert(enclosingTryAndCatchRegion.Kind == RegionKind.TryAndCatch);
                        Debug.Assert(block.Region.Kind == RegionKind.Catch || block.Region.Kind == RegionKind.Filter);
                        Debug.Assert(block.Region.FirstBlockOrdinal == block.Ordinal);
                        input = catchBlockInputDataMap[enclosingTryAndCatchRegion];
                    }
                    else
                    {
                        input = AnalysisDomain.Bottom;
                    }

                    UpdateInput(resultBuilder, block, input);
                }

                if (block.Region?.Kind == RegionKind.Try &&
                    block.Region?.Enclosing?.Kind == RegionKind.TryAndCatch &&
                    block.Region.Enclosing.FirstBlockOrdinal == block.Ordinal)
                {
                    MergeIntoCatchInputData(block.Region.Enclosing, input);
                }

                // Flow the new input through the block to get a new output.
                var output = Flow(OperationVisitor, block, AnalysisDomain.Clone(input));

                // Compare the previous output with the new output.
                if (!needsAtLeastOnePass)
                {
                    int compare = AnalysisDomain.Compare(GetOutput(resultBuilder[block]), output);

                    // The newly computed abstract values for each basic block
                    // must be always greater or equal than the previous value
                    // to ensure termination. 
                    Debug.Assert(compare <= 0, "The newly computed abstract value must be greater or equal than the previous one.");

                    // Is old output value >= new output value ?
                    if (compare >= 0)
                    {
                        Debug.Assert(IsValidWorklistState());
                        continue;
                    }
                }

                // The newly computed value is greater than the previous value,
                // so we need to update the current block result's
                // output values with the new ones.
                UpdateOutput(resultBuilder, block, output);

                // Since the new output value is different than the previous one, 
                // we need to propagate it to all the successor blocks of the current block.
                uniqueSuccessors.Clear();
                var successorsWithAdjustedBranches = GetSuccessorsWithAdjustedBranches(block).ToArray();
                foreach ((BranchWithInfo successorWithBranch, BranchWithInfo preadjustSuccessorWithBranch) successorWithAdjustedBranch in successorsWithAdjustedBranches)
                {
                    var newSuccessorInput = OperationVisitor.FlowBranch(block, successorWithAdjustedBranch.successorWithBranch, AnalysisDomain.Clone(output));
                    if (successorWithAdjustedBranch.preadjustSuccessorWithBranch != null)
                    {
                        UpdateFinallyAndCatchSuccessors(successorWithAdjustedBranch.preadjustSuccessorWithBranch, newSuccessorInput);
                    }

                    var successorBlockOpt = successorWithAdjustedBranch.successorWithBranch.Destination;
                    if (successorBlockOpt == null)
                    {
                        continue;
                    }

                    newSuccessorInput = OperationVisitor.OnLeavingRegions(successorWithAdjustedBranch.successorWithBranch.LeavingRegions, block, newSuccessorInput);
                    var currentSuccessorInput = GetInput(resultBuilder[successorBlockOpt]);
                    var mergedSuccessorInput = currentSuccessorInput != null ?
                        AnalysisDomain.Merge(currentSuccessorInput, newSuccessorInput) :
                        newSuccessorInput;

                    if (currentSuccessorInput != null)
                    {
                        int compare = AnalysisDomain.Compare(currentSuccessorInput, mergedSuccessorInput);

                        // The newly computed abstract values for each basic block
                        // must be always greater or equal than the previous value
                        // to ensure termination.
                        Debug.Assert(compare <= 0, "The newly computed abstract value must be greater or equal than the previous one.");

                        // Is old input value >= new input value
                        if (compare >= 0)
                        {
                            continue;
                        }
                    }

                    UpdateInput(resultBuilder, successorBlockOpt, mergedSuccessorInput);

                    if (uniqueSuccessors.Add(successorBlockOpt))
                    {
                        worklist.Enqueue(successorBlockOpt);
                    }
                }

                Debug.Assert(IsValidWorklistState());
            }

            return resultBuilder.ToResult(ToResult, OperationVisitor.GetStateMap(),
                OperationVisitor.GetPredicateValueKindMap(), OperationVisitor.GetMergedDataForUnhandledThrowOperations(),
                cfg, OperationVisitor.ValueDomain.UnknownOrMayBeValue);

            void MergeIntoCatchInputData(Region tryAndCatchRegion, TAnalysisData dataToMerge)
            {
                Debug.Assert(tryAndCatchRegion.Kind == RegionKind.TryAndCatch);

                if (!catchBlockInputDataMap.TryGetValue(tryAndCatchRegion, out var catchBlockInputData))
                {
                    catchBlockInputData = AnalysisDomain.Clone(dataToMerge);
                }
                else
                {
                    catchBlockInputData = AnalysisDomain.Merge(catchBlockInputData, dataToMerge);
                }

                catchBlockInputDataMap[tryAndCatchRegion] = catchBlockInputData;
            }

            bool IsValidWorklistState()
            {
                if (worklist.Count == 0 && pendingBlocksNeedingAtLeastOnePass.Count == 0)
                {
                    return true;
                }

                foreach (var block in worklist.Concat(pendingBlocksNeedingAtLeastOnePass))
                {
                    if (block.Predecessors.IsEmpty || !HasUnprocessedPredecessorBlock(block))
                    {
                        return true;
                    }
                }

                return false;
            }

            bool HasUnprocessedPredecessorBlock(BasicBlock block)
            {
                var predecessorsWithBranches = block.GetPredecessorsWithBranches(ordinalToBlockMap);
                return predecessorsWithBranches.Any(predecessorWithBranch =>
                    predecessorWithBranch.predecessorBlock.Ordinal < block.Ordinal &&
                    pendingBlocksNeedingAtLeastOnePass.Contains(predecessorWithBranch.predecessorBlock));
            }

            IEnumerable<(BranchWithInfo successorWithBranch, BranchWithInfo preadjustSuccessorWithBranch)> GetSuccessorsWithAdjustedBranches(BasicBlock basicBlock)
            {
                if (basicBlock.Kind != BasicBlockKind.Exit)
                {
                    if (finallyOrCatchBlockSuccessorsMap.TryGetValue(basicBlock.Ordinal, out var finallyOrCatchSuccessors))
                    {
                        Debug.Assert(basicBlock.Region.Kind == RegionKind.Finally || basicBlock.Region.Kind == RegionKind.Catch || basicBlock.Region.Kind == RegionKind.FilterAndHandler);
                        foreach (var successor in finallyOrCatchSuccessors)
                        {
                            yield return (successor, null);
                        }
                    }
                    else
                    {
                        var preadjustSuccessorWithbranch = basicBlock.GetNextBranchWithInfo();
                        var adjustedSuccessorWithBranch = AdjustBranchIfFinalizing(preadjustSuccessorWithbranch);
                        yield return (successorWithBranch: adjustedSuccessorWithBranch, preadjustSuccessorWithBranch: preadjustSuccessorWithbranch);

                        if (basicBlock.Conditional.Branch.Destination != null)
                        {
                            preadjustSuccessorWithbranch = basicBlock.GetConditionalBranchWithInfo();
                            adjustedSuccessorWithBranch = AdjustBranchIfFinalizing(preadjustSuccessorWithbranch);
                            yield return (successorWithBranch: adjustedSuccessorWithBranch, preadjustSuccessorWithBranch: preadjustSuccessorWithbranch);
                        }
                    }
                }
            }

            BranchWithInfo AdjustBranchIfFinalizing(BranchWithInfo branch)
            {
                if (branch.FinallyRegions.Length > 0)
                {
                    var firstFinally = branch.FinallyRegions[0];
                    var destination = ordinalToBlockMap[firstFinally.FirstBlockOrdinal];
                    return branch.With(destination, enteringRegions: ImmutableArray<Region>.Empty,
                        leavingRegions: ImmutableArray<Region>.Empty, finallyRegions: ImmutableArray<Region>.Empty);
                }
                else
                {
                    return branch;
                }
            }

            Region GetEnclosingTryAndCatchRegionIfStartsHandler(BasicBlock block)
            {
                if (block.Region?.FirstBlockOrdinal == block.Ordinal)
                {
                    switch (block.Region.Kind)
                    {
                        case RegionKind.Catch:
                            if (block.Region.Enclosing.Kind == RegionKind.TryAndCatch)
                            {
                                return block.Region.Enclosing;
                            }
                            break;

                        case RegionKind.Filter:
                            if (block.Region.Enclosing.Kind == RegionKind.FilterAndHandler &&
                                block.Region.Enclosing.Enclosing?.Kind == RegionKind.TryAndCatch)
                            {
                                return block.Region.Enclosing.Enclosing;
                            }
                            break;
                    }
                }

                return null;
            }

            void UpdateFinallyAndCatchSuccessors(BranchWithInfo branch, TAnalysisData branchData)
            {
                if (branch.FinallyRegions.Length > 0)
                {
                    var successor = branch.With(conditionOpt: null, valueOpt: null, jumpIfTrue: null);
                    for (var i = branch.FinallyRegions.Length - 1; i >= 0; i--)
                    {
                        Region finallyRegion = branch.FinallyRegions[i];
                        UpdateFinallyOrCatchSuccessor(finallyRegion, successor);
                        successor = new BranchWithInfo(destination: ordinalToBlockMap[finallyRegion.FirstBlockOrdinal]);
                    }
                }

                if (branch.LeavingRegions.Length > 0)
                {
                    var successor = branch.With(conditionOpt: null, valueOpt: null, jumpIfTrue: null);
                    if (branch.FinallyRegions.Length > 0)
                    {
                        var finallyRegion = branch.FinallyRegions[0];
                        successor = new BranchWithInfo(destination: ordinalToBlockMap[finallyRegion.FirstBlockOrdinal]);
                    }

                    foreach (var tryAndCatchRegion in branch.LeavingRegions.Where(region => region.Kind == RegionKind.TryAndCatch))
                    {
                        var hasHandler = false;
                        foreach (var catchRegion in tryAndCatchRegion.Regions.Where(region => region.Kind == RegionKind.Catch || region.Kind == RegionKind.FilterAndHandler))
                        {
                            UpdateFinallyOrCatchSuccessor(catchRegion, successor);
                            worklist.Enqueue(ordinalToBlockMap[catchRegion.FirstBlockOrdinal]);
                            hasHandler = true;
                        }

                        if (hasHandler)
                        {
                            MergeIntoCatchInputData(tryAndCatchRegion, branchData);
                        }
                    }
                }
            }

            void UpdateFinallyOrCatchSuccessor(Region finallyOrCatchRegion, BranchWithInfo successor)
            {
                Debug.Assert(finallyOrCatchRegion.Kind == RegionKind.Finally || finallyOrCatchRegion.Kind == RegionKind.Catch || finallyOrCatchRegion.Kind == RegionKind.FilterAndHandler);
                if (!finallyOrCatchBlockSuccessorsMap.TryGetValue(finallyOrCatchRegion.LastBlockOrdinal, out var lastBlockSuccessors))
                {
                    lastBlockSuccessors = new List<BranchWithInfo>();
                    finallyOrCatchBlockSuccessorsMap.Add(finallyOrCatchRegion.LastBlockOrdinal, lastBlockSuccessors);
                }

                lastBlockSuccessors.Add(successor);
            }
        }

        public static TAnalysisData Flow(DataFlowOperationVisitor<TAnalysisData, TAbstractAnalysisValue> operationVisitor, BasicBlock block, TAnalysisData data)
        {
            operationVisitor.OnStartBlockAnalysis(block, data);

            foreach (var statement in block.Statements)
            {
                data = operationVisitor.Flow(statement, block, data);
            }

            operationVisitor.OnEndBlockAnalysis(block);

            return data;
        }

        private static void EnqueueRange<T>(Queue<T> self, IEnumerable<T> collection)
        {
            foreach (var item in collection)
            {
                if (!self.Contains(item))
                {
                    self.Enqueue(item);
                }
            }
        }

        internal abstract TAnalysisResult ToResult(BasicBlock basicBlock, DataFlowAnalysisInfo<TAnalysisData> blockAnalysisData);
        private static TAnalysisData GetInput(DataFlowAnalysisInfo<TAnalysisData> result) => result.Input;
        private static TAnalysisData GetOutput(DataFlowAnalysisInfo<TAnalysisData> result) => result.Output;
        protected abstract TAnalysisData GetInputData(TAnalysisResult result);

        private static void UpdateInput(DataFlowAnalysisResultBuilder<TAnalysisData> builder, BasicBlock block, TAnalysisData newInput)
        {
            var currentData = builder[block];
            var newData = currentData.WithInput(newInput);
            builder.Update(block, newData);
        }

        private static void UpdateOutput(DataFlowAnalysisResultBuilder<TAnalysisData> builder, BasicBlock block, TAnalysisData newOutput)
        {
            var currentData = builder[block];
            var newData = currentData.WithOutput(newOutput);
            builder.Update(block, newData);
        }
    }
}