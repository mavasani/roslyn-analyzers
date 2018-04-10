// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.CodeAnalysis.Operations.DataFlow.PointsToAnalysis
{
    using PointsToAnalysisData = IDictionary<AnalysisEntity, PointsToAbstractValue>;

    internal partial class PointsToAnalysis : ForwardDataFlowAnalysis<PointsToAnalysisData, PointsToBlockAnalysisResult, PointsToAbstractValue>
    {
        /// <summary>
        /// Abstract value domain for <see cref="PointsToAnalysis"/> to merge and compare <see cref="PointsToAbstractValue"/> values.
        /// </summary>
        private class PointsToAbstractValueDomain : AbstractValueDomain<PointsToAbstractValue>
        {
            public static PointsToAbstractValueDomain Default = new PointsToAbstractValueDomain();
            private readonly SetAbstractDomain<AbstractLocation> _locationsDomain = new SetAbstractDomain<AbstractLocation>();
            private readonly SetAbstractDomain<AnalysisEntity> _entitiesDomain = new SetAbstractDomain<AnalysisEntity>();

            private PointsToAbstractValueDomain() { }

            public override PointsToAbstractValue Bottom => PointsToAbstractValue.Undefined;

            public override PointsToAbstractValue UnknownOrMayBeValue => PointsToAbstractValue.Unknown;

            public override int Compare(PointsToAbstractValue oldValue, PointsToAbstractValue newValue)
            {
                Debug.Assert(oldValue != null);
                Debug.Assert(newValue != null);

                if (ReferenceEquals(oldValue, newValue))
                {
                    return 0;
                }

                if (oldValue.Kind == newValue.Kind)
                {
                    int locationsCompareResult = _locationsDomain.Compare(oldValue.Locations, newValue.Locations);
                    int nullCompareResult = NullAnalysis.NullAbstractValueDomain.Default.Compare(oldValue.NullState, newValue.NullState);
                    int copyCompareResult = oldValue.Kind == PointsToAbstractValueKind.Invalid ?
                        0 :
                        _entitiesDomain.Compare(oldValue.CopyEntities, newValue.CopyEntities) * -1;
                    if (locationsCompareResult > 0 || nullCompareResult > 0 || copyCompareResult > 0)
                    {
                        Debug.Fail("Compare");
                        return 1;
                    }
                    else if (locationsCompareResult < 0 || nullCompareResult < 0 || copyCompareResult != 0)
                    {
                        return -1;
                    }
                    else
                    {
                        return 0;
                    }
                }
                else if (oldValue.Kind < newValue.Kind)
                {
                    Debug.Assert(NullAnalysis.NullAbstractValueDomain.Default.Compare(oldValue.NullState, newValue.NullState) <= 0);
                    Debug.Assert(oldValue.Kind == PointsToAbstractValueKind.Invalid || _entitiesDomain.Compare(oldValue.CopyEntities, newValue.CopyEntities) >= 0);
                    return -1;
                }
                else
                {
                    Debug.Fail("Compare");
                    return 1;
                }
            }

            public override PointsToAbstractValue Merge(PointsToAbstractValue value1, PointsToAbstractValue value2)
            {
                Debug.Assert(value1 != null);
                Debug.Assert(value2 != null);

                if (ReferenceEquals(value1, value2))
                {
                    return value1;
                }
                else if (value1.Kind == PointsToAbstractValueKind.Invalid)
                {
                    var mergedCopyValues = _entitiesDomain.Intersect(value1.CopyEntities, value2.CopyEntities);
                    return value2.WithCopyEntities(mergedCopyValues);
                }
                else if (value2.Kind == PointsToAbstractValueKind.Invalid)
                {
                    var mergedCopyValues = _entitiesDomain.Intersect(value1.CopyEntities, value2.CopyEntities);
                    return value1.WithCopyEntities(mergedCopyValues);
                }
                else if (value1.Kind == PointsToAbstractValueKind.Undefined ||
                    value2.Kind == PointsToAbstractValueKind.Undefined ||
                    value1.Kind == PointsToAbstractValueKind.Unknown ||
                    value2.Kind == PointsToAbstractValueKind.Unknown)
                {
                    return PointsToAbstractValue.Unknown;
                }

                var mergedLocations = _locationsDomain.Merge(value1.Locations, value2.Locations);
                var mergedNullState = NullAnalysis.NullAbstractValueDomain.Default.Merge(value1.NullState, value2.NullState);
                var mergedCopyEntities = _entitiesDomain.Intersect(value1.CopyEntities, value2.CopyEntities);
                var mergedKind = (PointsToAbstractValueKind)Math.Max((int)value1.Kind, (int)value2.Kind);
                var result = PointsToAbstractValue.Create(mergedLocations, mergedNullState, mergedCopyEntities, mergedKind);
                Debug.Assert(Compare(value1, result) <= 0);
                Debug.Assert(Compare(value2, result) <= 0);
                return result;
            }
        }
    }
}
