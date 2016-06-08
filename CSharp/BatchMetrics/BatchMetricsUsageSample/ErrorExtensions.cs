//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.BatchMetricsUsageSample
{
    using Microsoft.Azure.Batch.Common;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal static class ErrorExtensions
    {
        internal static bool IsBatchErrorCode(this BatchException ex, string errorCode)
        {
            return ex.RequestInformation != null
                && ex.RequestInformation.BatchError != null
                && ex.RequestInformation.BatchError.Code == errorCode;
        }
    }
}
