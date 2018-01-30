// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.CodeAnalysis.Operations.DataFlow.PointsToAnalysis
{
    internal partial class PointsToAnalysis : ForwardDataFlowAnalysis<PointsToAnalysisData, PointsToBlockAnalysisResult, PointsToAbstractValue>
    {
        /// <summary>
        /// Abstract value domain for <see cref="PointsToAnalysis"/> to merge and compare <see cref="IndexerKind"/> values.
        /// </summary>
        private class IndexerAbstractValueDomain : AbstractDomain<IndexerKind>
        {
            public static IndexerAbstractValueDomain Default = new IndexerAbstractValueDomain();

            private IndexerAbstractValueDomain()
            {
            }

            public override IndexerKind Bottom => IndexerKind.Undefined;

            public override int Compare(IndexerKind value1, IndexerKind value2)
            {
                return Comparer<IndexerKind>.Default.Compare(value1, value2);
            }

            public override IndexerKind Merge(IndexerKind value1, IndexerKind value2)
            {
                IndexerKind result;

                if (value1 == IndexerKind.MaybeIndex ||
                    value2 == IndexerKind.MaybeIndex)
                {
                    result = IndexerKind.MaybeIndex;
                }
                else if (value1 == IndexerKind.Undefined)
                {
                    result = value2;
                }
                else if (value2 == IndexerKind.Undefined)
                {
                    result = value1;
                }
                else
                {
                    result = value1;
                }

                return result;
            }
        }
    }
}
