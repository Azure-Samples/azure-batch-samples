namespace Microsoft.Azure.Batch.Samples.BatchMetrics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    internal static class TaskHelpers
    {
        public static async Task CancellableDelay(TimeSpan delay, CancellationToken ct)
        {
            try
            {
                await Task.Delay(delay);
            }
            catch (TaskCanceledException)
            {
            }
        }
    }
}
