using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ContentManagement
{
    internal static class FileHelper
    {
        public static string RelativePath(string fullPath, string basePath)
        {
            if (!basePath.EndsWith("\\"))
            {
                basePath += "\\";
            }

            var baseUri = new Uri(basePath);
            var fullUri = new Uri(fullPath);
            var relativeUri = baseUri.MakeRelativeUri(fullUri);

            return relativeUri.ToString();
        }

        public static IEnumerable<string> FilterFiles(string path, params string[] extensions)
        {
            return extensions
              .Select(x => "*." + x)
              .SelectMany(x => Directory.EnumerateFiles(path, x, SearchOption.AllDirectories));
        }
    }
}
