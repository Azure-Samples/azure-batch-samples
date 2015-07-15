using System;

namespace Microsoft.Azure.BatchExplorer.Models
{
    public class ComputeNodeFile
    {
        /// <summary>
        /// Name of the file or directory
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Full file path
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// True if is a directory, false if file
        /// </summary>
        public bool IsDirectory { get; set; }

        /// <summary>
        /// Date file was created
        /// </summary>
        public DateTime CreationTime { get; set; }

        /// <summary>
        /// Content type
        /// </summary>
        public string ContentType { get; set; }

        /// <summary>
        /// File size
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// Last modified time
        /// </summary>
        public DateTime LastModifiedTime { get; set; }
    }
}
