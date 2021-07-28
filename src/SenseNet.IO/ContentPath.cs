using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SenseNet.IO
{
    public class ContentPath
    {
        public static string GetRelativePath(string path, string rootPath)
        {
            if (path.Equals(rootPath, StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            rootPath ??= string.Empty;

            if (string.IsNullOrEmpty(rootPath) || rootPath == "/")
                return path;

            if (path.StartsWith(rootPath + "/", StringComparison.OrdinalIgnoreCase))
                return path.Substring(rootPath.Length + 1);
            return path;
        }

        public static string GetAbsolutePath(string relativePath, string rootPath)
        {
            if (relativePath.Length > 0 && relativePath[0] == '/')
                return relativePath;

            rootPath ??= string.Empty;

            if (rootPath.EndsWith('/'))
                rootPath = rootPath.TrimEnd('/');

            return $"{rootPath}/{relativePath}";
        }

        public static string GetParentPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;
            if(path=="/")
                return string.Empty;
            var p = path.LastIndexOf('/');
            if (p < 0)
                return string.Empty;
            return path.Substring(0, p);
        }
    }
}
