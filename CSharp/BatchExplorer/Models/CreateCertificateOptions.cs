using Microsoft.Azure.Batch.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.BatchExplorer.Models
{
    public class CreateCertificateOptions
    {
        public string FilePath { get; set; }

        public string Password { get; set; }

        public CertificateFormat CertificateFormat { get; set; }
    }
}
