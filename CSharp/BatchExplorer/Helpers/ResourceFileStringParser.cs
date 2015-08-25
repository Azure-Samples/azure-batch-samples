using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.BatchExplorer.Helpers
{
    public static class ResourceFileStringParser
    {
        private static readonly char[] FileDelimiter = new[] { ';' };
        private static readonly string[] PartDelimiter = new[] { "=>" };

        public static ResourceFileParseResult Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return ResourceFileParseResult.Empty;
            }

            var fileStrings = text.Split(FileDelimiter, StringSplitOptions.RemoveEmptyEntries)
                                  .Select(s => s.Trim())
                                  .ToList();

            var errors = new List<string>();
            var files = new List<ResourceFileInfo>();

            foreach (var fileString in fileStrings)
            {
                var rawParse = fileString.Split(PartDelimiter, StringSplitOptions.None)
                                         .Select(s => s.Trim())
                                         .ToList();

                if (rawParse.Count == 2 && rawParse[0].Length > 0 && rawParse[1].Length > 0)
                {
                    files.Add(new ResourceFileInfo(rawParse[0], rawParse[1]));
                }
                else
                {
                    errors.Add(string.Format("Resource file '{0}' was not of the form 'url => path'", fileString));
                }
            }

            return new ResourceFileParseResult(errors, files);
        }
    }

    public sealed class ResourceFileParseResult
    {
        private readonly IReadOnlyList<string> _errors;
        private readonly IReadOnlyList<ResourceFileInfo> _files;

        public static readonly ResourceFileParseResult Empty = new ResourceFileParseResult(new List<string>(), new List<ResourceFileInfo>());

        public ResourceFileParseResult(List<string> errors, List<ResourceFileInfo> files)
        {
            _errors = errors.AsReadOnly();
            _files = files.AsReadOnly();
        }

        public bool HasErrors
        {
            get { return _errors.Any(); }
        }

        public IReadOnlyList<string> Errors
        {
            get { return _errors; }
        }

        public IReadOnlyList<ResourceFileInfo> Files
        {
            get { return _files; }
        }
    }

    public sealed class ResourceFileInfo
    {
        private readonly string _blobUrl;
        private readonly string _filePath;

        public ResourceFileInfo(string blobUrl, string filePath)
        {
            _blobUrl = blobUrl;
            _filePath = filePath;
        }

        public string BlobUrl
        {
            get { return _blobUrl; }
        }

        public string FilePath
        {
            get { return _filePath; }
        }
    }
}
