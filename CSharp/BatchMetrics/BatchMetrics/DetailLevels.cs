namespace Microsoft.Azure.Batch.Samples.BatchMetrics
{
    using Microsoft.Azure.Batch;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal static class DetailLevels
    {
        internal static class IdAndState
        {
            internal static readonly ODATADetailLevel AllEntities = new ODATADetailLevel(selectClause: "id,state");

            internal static ODATADetailLevel OnlyChangedAfter(DateTime time)
            {
                string timeString = string.Format("DateTime'{0}'", time.ToString("o").Replace(" ", "T"));
                return new ODATADetailLevel(selectClause: "id, state", filterClause: "stateTransitionTime gt " + timeString);
            }
        }
    }
}
