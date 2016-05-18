using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.Batch.Samples.Common
{
    public enum CreatePoolResult
    {
        PoolExisted,
        CreatedNew,
        ResizedExisting,
    }
}
