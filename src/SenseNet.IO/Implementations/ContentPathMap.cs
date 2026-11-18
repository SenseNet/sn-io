using System;
using System.Collections.Generic;

namespace SenseNet.IO.Implementations
{
    // Import-scoped aliases learned from server responses. Never generates names.
    internal class ContentPathMap
    {
        private readonly Dictionary<string, string> _paths = new(StringComparer.OrdinalIgnoreCase);

        public void Record(string requestedPath, string actualPath)
        {
            if (!string.IsNullOrEmpty(actualPath))
                _paths[requestedPath] = actualPath;
        }

        public bool TryGetExact(string path, out string actualPath) => _paths.TryGetValue(path, out actualPath);

        public string Resolve(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith('/'))
                return path;

            // Search complete ancestors, longest first; /News must not match /Newsletter.
            var prefix = path;
            while (!string.IsNullOrEmpty(prefix))
            {
                if (_paths.TryGetValue(prefix, out var actualPath))
                    return actualPath + path.Substring(prefix.Length);
                var separator = prefix.LastIndexOf('/');
                if (separator <= 0)
                    break;
                prefix = prefix.Substring(0, separator);
            }
            return path;
        }
    }
}
