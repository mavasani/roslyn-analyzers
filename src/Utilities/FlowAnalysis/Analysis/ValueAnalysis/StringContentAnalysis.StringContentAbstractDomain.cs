// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.ValueContentAnalysis
{
    internal partial class ValueContentAnalysis : ForwardDataFlowAnalysis<ValueContentAnalysisData, ValueContentBlockAnalysisResult, StringContentAbstractValue>
    {
        /// <summary>
        /// Abstract value domain for <see cref="ValueContentAnalysis"/> to merge and compare <see cref="IAbstractValue"/> values.
        /// </summary>
        private sealed class ValueContentAbstractValueDomain : AbstractValueDomain<IAbstractValue>
        {
            public static ValueContentAbstractValueDomain Default = new ValueContentAbstractValueDomain();

            private ValueContentAbstractValueDomain() { }

            public override IAbstractValue Bottom => StringContentAbstractValue.UndefinedState;

            public override IAbstractValue UnknownOrMayBeValue => StringContentAbstractValue.MayBeContainsNonLiteralState;

            public override int Compare(StringContentAbstractValue oldValue, StringContentAbstractValue newValue)
            {
                Debug.Assert(oldValue != null);
                Debug.Assert(newValue != null);

                if (ReferenceEquals(oldValue, newValue))
                {
                    return 0;
                }

                if (oldValue.NonLiteralState == newValue.NonLiteralState)
                {
                    if (oldValue.IsLiteralState)
                    {
                        if (oldValue.LiteralValues.SetEquals(newValue.LiteralValues))
                        {
                            return 0;
                        }
                        else if (oldValue.LiteralValues.IsSubsetOf(newValue.LiteralValues))
                        {
                            return -1;
                        }
                        else
                        {
                            Debug.Fail("Non-monotonic Merge function");
                            return 1;
                        }
                    }
                    else
                    {
                        return 0;
                    }
                }
                else if (oldValue.NonLiteralState < newValue.NonLiteralState)
                {
                    return -1;
                }
                else
                {
                    Debug.Fail("Non-monotonic Merge function");
                    return 1;
                }
            }

            public override StringContentAbstractValue Merge(StringContentAbstractValue value1, StringContentAbstractValue value2)
            {
                return value1.Merge(value2);
            }
        }
    }
}
