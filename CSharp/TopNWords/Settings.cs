using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.Batch.Samples.TopNWordsSample
{
    public class Settings
    {
        public int NumberOfTasks { get; set; }
        public int PoolNodeCount { get; set; }
        public int TopWordCount { get; set; }
        public string FileName { get; set; }
        public string PoolId { get; set; }
        public string JobId { get; set; }
        public bool ShouldDeleteJob { get; set; }
        public bool ShouldDeletePool { get; set; }
        public bool ShouldDeleteContainer { get; set; }
    }
}
