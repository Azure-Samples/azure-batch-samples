namespace Microsoft.Azure.Batch.Samples.BatchMetrics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal static class EnumHelpers
    {
        internal static int GetMaxValue(Type enumType)
        {
            return Enum.GetValues(enumType).Cast<int>().Max();
        }
    }
}
