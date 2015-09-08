using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.BatchExplorer.Models;

namespace Microsoft.Azure.BatchExplorer.Helpers
{
    public static class ResourceFileStringParser
    {
        private static readonly string[] FileDelimiter = new[] { ";", Environment.NewLine };
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
                    errors.Add(string.Format("Resource file '{0}' was not of the form 'blobSource => filePath'", fileString));
                }
            }

            return new ResourceFileParseResult(errors, files);
        }
    }

    public sealed class ResourceFileParseResult
    {
        private readonly IReadOnlyList<string> errors;
        private readonly IReadOnlyList<ResourceFileInfo> files;

        public static readonly ResourceFileParseResult Empty = new ResourceFileParseResult(new List<string>(), new List<ResourceFileInfo>());

        public ResourceFileParseResult(List<string> errors, List<ResourceFileInfo> files)
        {
            this.errors = errors.AsReadOnly();
            this.files = files.AsReadOnly();
        }

        public bool HasErrors
        {
            get { return errors.Any(); }
        }

        public IReadOnlyList<string> Errors
        {
            get { return errors; }
        }

        public IReadOnlyList<ResourceFileInfo> Files
        {
            get { return files; }
        }
    }
}
