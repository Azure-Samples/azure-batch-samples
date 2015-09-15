using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.BatchExplorer.Models
{
    public class StartTaskOptions
    {
        public string CommandLine { get; set; }

        public List<ResourceFileInfo> ResourceFiles { get; set; }

        public bool RunElevated { get; set; }
    }
}
