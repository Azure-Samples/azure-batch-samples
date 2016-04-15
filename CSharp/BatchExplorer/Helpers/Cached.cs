namespace Microsoft.Azure.BatchExplorer.Helpers
{
    using System;
    using System.Threading.Tasks;

    public class Cached<T>
    {
        private DateTime? lastUpdateTime;
        private readonly Func<Task<T>> refreshFunc;
        private readonly TimeSpan expiryTime;
        private T data;

        public Cached(Func<Task<T>> refreshFunc, TimeSpan expiryTime)
        {
            this.refreshFunc = refreshFunc;
            this.lastUpdateTime = null; //Hasn't been updated yet
            this.expiryTime = expiryTime;
        }

        public async Task<T> GetDataAsync()
        {
            if (this.lastUpdateTime == null || this.lastUpdateTime + this.expiryTime <= DateTime.UtcNow)
            {
                this.data = await this.refreshFunc();
                this.lastUpdateTime = DateTime.UtcNow;
            }

            return this.data;
        }
    }
}
