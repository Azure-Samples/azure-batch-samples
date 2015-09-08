using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.BatchExplorer.Models
{
    public sealed class ResourceFileInfo
    {
        private readonly string blobSource;
        private readonly string filePath;

        public ResourceFileInfo(string blobSource, string filePath)
        {
            this.blobSource = blobSource;
            this.filePath = filePath;
        }

        public string BlobSource
        {
            get { return blobSource; }
        }

        public string FilePath
        {
            get { return filePath; }
        }
    }
}
