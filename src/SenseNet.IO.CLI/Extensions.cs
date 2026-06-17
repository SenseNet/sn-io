using System;
using SenseNet.Client;
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
            if (args.Skip != null)
                settings.Skip = args.Skip;
        }
        public static void RewriteSettings(this FsXmlReaderArgs args, FsXmlReaderArgs settings)
        {
            // rewrite from command line args (they override config file values)
            if (args.Path != null)
                settings.Path = args.Path;
            if (args.Skip != null)
                settings.Skip = args.Skip;
            if (args.ExcludedFields != null)
                settings.ExcludedFields = args.ExcludedFields;
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
            if (args.Flatten != null)
                settings.Flatten = args.Flatten;
        }
        public static void RewriteSettings(this RepositoryReaderArgs args, RepositoryReaderArgs settings)
        {
            // workaround for "Null configuration elements deserialized as empty strings" https://github.com/dotnet/runtime/issues/36510
            if (string.IsNullOrEmpty(settings.Url))
                settings.Url = null;
            if (string.IsNullOrEmpty(settings.Path))
                settings.Path = null;
            if (string.IsNullOrEmpty(settings.Filter))
                settings.Filter = null;
            if (string.IsNullOrEmpty(settings.Expand))
                settings.Expand = null;
            if (settings.ExcludedFields != null && settings.ExcludedFields.Length == 0)
                settings.ExcludedFields = null;
            if (settings.BlockSize < 1)
                settings.BlockSize = null;

            // rewrite
            if (args.Url != null)
                settings.Url = args.Url;
            if (args.Path != null)
                settings.Path = args.Path;
            if (args.Filter != null)
                settings.Filter = args.Filter;
            if (args.Expand != null)
                settings.Expand = args.Expand;
            if (args.ExcludedFields != null)
                settings.ExcludedFields = args.ExcludedFields;
            if (args.BlockSize != null)
                settings.BlockSize = args.BlockSize;
            if (args.Authentication.Type != null)
                settings.Authentication.Type = args.Authentication.Type;
            if (args.Authentication.ApiKey != null)
                settings.Authentication.ApiKey = args.Authentication.ApiKey;
            if (args.Authentication.ClientId != null)
                settings.Authentication.ClientId = args.Authentication.ClientId;
            if (args.Authentication.ClientSecret != null)
                settings.Authentication.ClientSecret = args.Authentication.ClientSecret;
            if (args.Authentication.Username != null)
                settings.Authentication.Username = args.Authentication.Username;
            if (args.Authentication.Password != null)
                settings.Authentication.Password = args.Authentication.Password;
            if (args.Authentication.Domain != null)
                settings.Authentication.Domain = args.Authentication.Domain;
            if (args.Authentication.LoginPath != null)
                settings.Authentication.LoginPath = args.Authentication.LoginPath;
            if (args.Authentication.LoginUserNameField != null)
                settings.Authentication.LoginUserNameField = args.Authentication.LoginUserNameField;
            if (args.Authentication.LoginPasswordField != null)
                settings.Authentication.LoginPasswordField = args.Authentication.LoginPasswordField;
            if (args.Authentication.LoginDomainField != null)
                settings.Authentication.LoginDomainField = args.Authentication.LoginDomainField;
            if (args.Authentication.HeaderName != null)
                settings.Authentication.HeaderName = args.Authentication.HeaderName;
            if (args.Authentication.HeaderValue != null)
                settings.Authentication.HeaderValue = args.Authentication.HeaderValue;
            if (args.Authentication.Cookie != null)
                settings.Authentication.Cookie = args.Authentication.Cookie;
        }
        public static void RewriteSettings(this RepositoryReaderArgs args, RepositoryOptions settings)
        {
            // rewrite
            if (args.Url != null)
                settings.Url = args.Url;
            if (args.Authentication.ApiKey != null)
                settings.Authentication.ApiKey = args.Authentication.ApiKey;
            if (args.Authentication.ClientId != null)
                settings.Authentication.ClientId = args.Authentication.ClientId;
            if (args.Authentication.ClientSecret != null)
                settings.Authentication.ClientSecret = args.Authentication.ClientSecret;
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
            if (args.CreateOnly)
                settings.CreateOnly = true;
            if (args.Authentication.ApiKey != null)
                settings.Authentication.ApiKey = args.Authentication.ApiKey;
            if (args.Authentication.ClientId != null)
                settings.Authentication.ClientId = args.Authentication.ClientId;
            if (args.Authentication.ClientSecret != null)
                settings.Authentication.ClientSecret = args.Authentication.ClientSecret;
        }
        public static void RewriteSettings(this RepositoryWriterArgs args, RepositoryOptions settings)
        {
            // rewrite
            if (args.Url != null)
                settings.Url = args.Url;
            if (args.Authentication.ApiKey != null)
                settings.Authentication.ApiKey = args.Authentication.ApiKey;
            if (args.Authentication.ClientId != null)
                settings.Authentication.ClientId = args.Authentication.ClientId;
            if (args.Authentication.ClientSecret != null)
                settings.Authentication.ClientSecret = args.Authentication.ClientSecret;
        }

        public static string ParamsToDisplay(this IContentReader reader)
        {
            if (reader is FsReader fsReader)
                return ParamsToDisplay(fsReader.Args);
            if (reader is LegacyRepositoryReader legacyRepositoryReader)
                return ParamsToDisplay(legacyRepositoryReader.Args);
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
            var skip = args.Skip == null || args.Skip.Length == 0
                ? string.Empty
                : ", Skip: " + string.Join("; ", args.Skip);

            return $"Path: {args.Path}{skip}";
        }
        public static string ParamsToDisplay(this FsWriterArgs args)
        {
            var result = string.IsNullOrEmpty(args.Name) ? $"Path: {args.Path}" : $"Path: {args.Path}, Name: {args.Name}";
            if (args.Flatten == true)
                result += ", Flatten";
            return result;
        }
        public static string ParamsToDisplay(this RepositoryReaderArgs args)
        {
            var filter = args.Filter == null ? string.Empty : $", Filter: {args.Filter}";
            var expand = args.Expand == null ? string.Empty : $", Expand: {args.Expand}";
            var excludedFields = args.ExcludedFields == null || args.ExcludedFields.Length == 0
                ? string.Empty
                : $", ExcludeFields: {string.Join(",", args.ExcludedFields)}";
            return $"Url: {args.Url}, Path: {args.Path}{filter}{expand}{excludedFields}, BlockSize: {args.BlockSize}";
        }
        public static string ParamsToDisplay(this RepositoryWriterArgs args)
        {
            return $"Url: {args.Url}, " +
                   $"Path: {args.Path ?? "/"}" +
                   $"{(args.Name == null ? string.Empty : $", Name: {args.Name}")}" +
                   $"{(args.CreateOnly ? ", CreateOnly" : string.Empty)}";
        }
    }
}
