//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.BatchMetrics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal static class EnumHelpers
    {
        // We use the underlying values of enums (specifically the TaskState enum)
        // to index into arrays; to do this requires an array whose size is big enough
        // to accommodate the maximum value of the enum type. This method gets that
        // maximum value.
        internal static int GetMaxValue(Type enumType)
        {
            return Enum.GetValues(enumType).Cast<int>().Max();
        }
    }
}
