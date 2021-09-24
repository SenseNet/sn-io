using System;
using SenseNet.IO.Implementations;

namespace SenseNet.IO.CLI
{
    public static class Extensions
    {
        public static string GetAllMessages(this Exception e)
        {
            var msg = e.Message;
            while ((e = e.InnerException) != null)
                msg += " | " + e.Message;
            return msg;
        }
        public static void RewriteSettings(this FsReaderArgs args, FsReaderArgs settings)
        {
            // workaround for "Null configuration elements deserialized as empty strings" https://github.com/dotnet/runtime/issues/36510
            if (string.IsNullOrEmpty(settings.Path))
                settings.Path = null;

            // rewrite
            if (args.Path != null)
                settings.Path = args.Path;
        }
        public static void RewriteSettings(this FsWriterArgs args, FsWriterArgs settings)
        {
            // workaround for "Null configuration elements deserialized as empty strings" https://github.com/dotnet/runtime/issues/36510
            if (string.IsNullOrEmpty(settings.Path))
                settings.Path = null;
            if (string.IsNullOrEmpty(settings.Name))
                settings.Name = null;

            // rewrite
            if (args.Path != null)
                settings.Path = args.Path;
            if (args.Name != null)
                settings.Name = args.Name;
        }
        public static void RewriteSettings(this RepositoryReaderArgs args, RepositoryReaderArgs settings)
        {
            // workaround for "Null configuration elements deserialized as empty strings" https://github.com/dotnet/runtime/issues/36510
            if (string.IsNullOrEmpty(settings.Url))
                settings.Url = null;
            if (string.IsNullOrEmpty(settings.Path))
                settings.Path = null;
            if (settings.BlockSize < 1)
                settings.BlockSize = null;

            // rewrite
            if (args.Url != null)
                settings.Url = args.Url;
            if (args.Path != null)
                settings.Path = args.Path;
            if (args.BlockSize != null)
                settings.BlockSize = args.BlockSize;
        }
        public static void RewriteSettings(this RepositoryWriterArgs args, RepositoryWriterArgs settings)
        {
            // workaround for "Null configuration elements deserialized as empty strings" https://github.com/dotnet/runtime/issues/36510
            if (string.IsNullOrEmpty(settings.Url))
                settings.Url = null;
            if (string.IsNullOrEmpty(settings.Path))
                settings.Path = null;
            if (string.IsNullOrEmpty(settings.Name))
                settings.Name = null;

            // rewrite
            if (args.Url != null)
                settings.Url = args.Url;
            if (args.Path != null)
                settings.Path = args.Path;
            if (args.Name != null)
                settings.Name = args.Name;
        }


        public static string ParamsToDisplay(this IContentReader reader)
        {
            if (reader is FsReader fsReader)
                return ParamsToDisplay(fsReader.Args);
            if (reader is RepositoryReader repositoryReader)
                return ParamsToDisplay(repositoryReader.Args);
            return string.Empty;
        }
        public static string ParamsToDisplay(this IContentWriter writer)
        {
            if (writer is FsWriter fsWriter)
                return ParamsToDisplay(fsWriter.Args);
            if (writer is RepositoryWriter repositoryWriter)
                return ParamsToDisplay(repositoryWriter.Args);
            return string.Empty;
        }
        public static string ParamsToDisplay(this FsReaderArgs args)
        {
            return $"Path: {args.Path}";
        }
        public static string ParamsToDisplay(this FsWriterArgs args)
        {
            return string.IsNullOrEmpty(args.Name) ? $"Path: {args.Path}" : $"Path: {args.Path}, Name: {args.Name}";
        }
        public static string ParamsToDisplay(this RepositoryReaderArgs args)
        {
            return $"Url: {args.Url}, Path: {args.Path}, BlockSize: {args.BlockSize}";
        }
        public static string ParamsToDisplay(this RepositoryWriterArgs args)
        {
            return $"Url: {args.Url}, Path: {args.Path ?? "/"}{(args.Name == null ? string.Empty : $", Name: {args.Name}")}";
        }
    }
}
