//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.Common
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public enum CreatePoolResult
    {
        PoolExisted,
        CreatedNew,
        ResizedExisting,
    }
}
