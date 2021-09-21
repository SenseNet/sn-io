using System;
using System.IO;

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

            if (path.StartsWith(rootPath + "/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(rootPath + "\\", StringComparison.OrdinalIgnoreCase))
                return path.Substring(rootPath.Length + 1).Replace('\\', '/');
            return path;
        }

        public static string GetAbsolutePath(string relativePath, string rootPath)
        {
            if (relativePath.Length > 0 && relativePath[0] == '/')
                return relativePath;

            rootPath ??= string.Empty;

            if (rootPath.EndsWith('/'))
                rootPath = rootPath.TrimEnd('/');

            if (string.IsNullOrEmpty(relativePath))
                return rootPath;

            return $"{rootPath}/{relativePath}";
        }

        public static string GetParentPath(string path)
        {
            //if (string.IsNullOrEmpty(path))
            //    return string.Empty;
            //if(path=="/")
            //    return string.Empty;
            //var p = path.LastIndexOf('/');
            //if (p < 0)
            //    return string.Empty;
            //return path.Substring(0, p);

            return Path.GetDirectoryName(path)?.Replace("\\", "/");
        }

        public static string GetName(string path)
        {
            return Path.GetFileName(path);
        }

        public static string Combine(params string[] segments)
        {
            return Path.Combine(segments).Replace("\\", "/");
        }
    }
}
