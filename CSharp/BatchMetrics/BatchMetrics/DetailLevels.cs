//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.BatchMetrics
{
    using Microsoft.Azure.Batch;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    // Provides ODATADetailLevel objects for task state queries, both initial (all
    // entities) and delta (entities changed since a given time).
    internal static class DetailLevels
    {
        internal static class IdAndState
        {
            internal static readonly ODATADetailLevel AllEntities = new ODATADetailLevel(selectClause: "id,state");

            internal static ODATADetailLevel OnlyChangedAfter(DateTime time)
            {
                return new ODATADetailLevel(
                    selectClause: "id, state",
                    filterClause: string.Format("stateTransitionTime gt DateTime'{0:o}'", time)
                );
            }
        }
    }
}
