using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.BatchExplorer.Models
{
    public sealed class ResourceFileInfo
    {
        private readonly string _blobSource;
        private readonly string _filePath;

        public ResourceFileInfo(string blobSource, string filePath)
        {
            _blobSource = blobSource;
            _filePath = filePath;
        }

        public string BlobSource
        {
            get { return _blobSource; }
        }

        public string FilePath
        {
            get { return _filePath; }
        }
    }
}
