// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.CopyAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.PointsToAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.ValueContentAnalysis;

#pragma warning disable CA1067 // Override Object.Equals(object) when implementing IEquatable<T>

namespace Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.FlightEnabledAnalysis
{
    using CopyAnalysisResult = DataFlowAnalysisResult<CopyBlockAnalysisResult, CopyAbstractValue>;
    using InterproceduralFlightEnabledAnalysisData = InterproceduralAnalysisData<DictionaryAnalysisData<AnalysisEntity, FlightEnabledAbstractValue>, FlightEnabledAnalysisContext, FlightEnabledAbstractValue>;
    using FlightEnabledAnalysisData = DictionaryAnalysisData<AnalysisEntity, FlightEnabledAbstractValue>;
    using ValueContentAnalysisResult = DataFlowAnalysisResult<ValueContentBlockAnalysisResult, ValueContentAbstractValue>;

    /// <summary>
    /// Analysis context for execution of <see cref="FlightEnabledAnalysis"/> on a control flow graph.
    /// </summary>
    internal sealed class FlightEnabledAnalysisContext : AbstractDataFlowAnalysisContext<FlightEnabledAnalysisData, FlightEnabledAnalysisContext, FlightEnabledAnalysisResult, FlightEnabledAbstractValue>
    {
        private FlightEnabledAnalysisContext(
            AbstractValueDomain<FlightEnabledAbstractValue> valueDomain,
            WellKnownTypeProvider wellKnownTypeProvider,
            ControlFlowGraph controlFlowGraph,
            ISymbol owningSymbol,
            InterproceduralAnalysisConfiguration interproceduralAnalysisConfig,
            bool pessimisticAnalysis,
            PointsToAnalysisResult pointsToAnalysisResultOpt,
            ValueContentAnalysisResult valueContentAnalysisResult,
            Func<FlightEnabledAnalysisContext, FlightEnabledAnalysisResult> getOrComputeAnalysisResult,
            ControlFlowGraph parentControlFlowGraphOpt,
            InterproceduralFlightEnabledAnalysisData interproceduralAnalysisDataOpt,
            InterproceduralAnalysisPredicate interproceduralAnalysisPredicateOpt)
            : base(valueDomain, wellKnownTypeProvider, controlFlowGraph,
                  owningSymbol, interproceduralAnalysisConfig, pessimisticAnalysis,
                  predicateAnalysis: false,
                  exceptionPathsAnalysis: false,
                  copyAnalysisResultOpt: null,
                  pointsToAnalysisResultOpt,
                  getOrComputeAnalysisResult,
                  parentControlFlowGraphOpt,
                  interproceduralAnalysisDataOpt,
                  interproceduralAnalysisPredicateOpt)
        {
            Debug.Assert(valueContentAnalysisResult != null);
            this.ValueContentAnalysisResult = valueContentAnalysisResult;
        }

        internal static FlightEnabledAnalysisContext Create(
            AbstractValueDomain<FlightEnabledAbstractValue> valueDomain,
            WellKnownTypeProvider wellKnownTypeProvider,
            ControlFlowGraph controlFlowGraph,
            ISymbol owningSymbol,
            InterproceduralAnalysisConfiguration interproceduralAnalysisConfig,
            InterproceduralAnalysisPredicate interproceduralAnalysisPredicateOpt,
            bool pessimisticAnalysis,
            PointsToAnalysisResult pointsToAnalysisResultOpt,
            ValueContentAnalysisResult valueContentAnalysisResult,
            Func<FlightEnabledAnalysisContext, FlightEnabledAnalysisResult> getOrComputeAnalysisResult)
        {
            return new FlightEnabledAnalysisContext(
                valueDomain, wellKnownTypeProvider, controlFlowGraph,
                owningSymbol, interproceduralAnalysisConfig, pessimisticAnalysis,
                pointsToAnalysisResultOpt, valueContentAnalysisResult, getOrComputeAnalysisResult,
                parentControlFlowGraphOpt: null, interproceduralAnalysisDataOpt: null,
                interproceduralAnalysisPredicateOpt);
        }

        public override FlightEnabledAnalysisContext ForkForInterproceduralAnalysis(
            IMethodSymbol invokedMethod,
            ControlFlowGraph invokedControlFlowGraph,
            IOperation operation,
            PointsToAnalysisResult pointsToAnalysisResultOpt,
            CopyAnalysisResult copyAnalysisResultOpt,
            InterproceduralFlightEnabledAnalysisData interproceduralAnalysisData)
        {
            Debug.Assert(pointsToAnalysisResultOpt != null);
            Debug.Assert(copyAnalysisResultOpt == null);

            return new FlightEnabledAnalysisContext(ValueDomain, WellKnownTypeProvider, invokedControlFlowGraph, invokedMethod, InterproceduralAnalysisConfiguration, PessimisticAnalysis,
                pointsToAnalysisResultOpt, this.ValueContentAnalysisResult, GetOrComputeAnalysisResult, ControlFlowGraph, interproceduralAnalysisData, InterproceduralAnalysisPredicateOpt);
        }

        protected override void ComputeHashCodePartsSpecific(ArrayBuilder<int> builder)
        {
            builder.Add(ValueContentAnalysisResult.GetHashCode());
        }

        public ValueContentAnalysisResult ValueContentAnalysisResult { get; }
    }
}
