namespace Microsoft.Azure.BatchExplorer.Models
{
    public class VirtualMachineConfigurationOptions
    {

        public string Offer { get; set; }
        
        public string Publisher { get; set; }
        
        public string SkuId { get; set; }
        
        public string Version { get; set; }

        public string NodeAgentSkuId { get; set; }

        public bool? EnableWindowsAutomaticUpdates { get; set;  }
    }
}
