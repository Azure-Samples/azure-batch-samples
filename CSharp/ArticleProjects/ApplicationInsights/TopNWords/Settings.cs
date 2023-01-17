using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.Batch.Samples.TopNWordsSample
{
    public class Settings
    {
        public int PoolNodeCount { get; set; }
        public int TopWordCount { get; set; }
        public string DocumentsRootPath { get; set; }
        public string PoolId { get; set; }
        public string JobId { get; set; }
        public bool ShouldDeleteJob { get; set; }
        public bool ShouldDeletePool { get; set; }
        public bool ShouldDeleteContainer { get; set; }
        public string PoolNodeVirtualMachineSize { get; set; }
        public string ImagePublisher { get; set; }
        public string ImageOffer { get; set; }
        public string ImageSku { get; set; }
        public string ImageVersion { get; set; }
        public string NodeAgentSkuId { get; set; }           
    }
}
