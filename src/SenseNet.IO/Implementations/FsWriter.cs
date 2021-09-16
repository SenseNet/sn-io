using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace SenseNet.IO.Implementations
{
    public class FsWriterArgs
    {
        public string Path { get; set; }
        public string Name { get; set; }
    }

    public class FsWriter : IContentWriter
    {
        public FsWriterArgs Args { get; }
        public string OutputDirectory { get; }
        public string ContainerPath => "/";
        public string RootName { get; }

        public FsWriter(IOptions<FsWriterArgs> args)
        {
            if (args == null)
                throw new ArgumentNullException(nameof(args));
            Args = args.Value;
            OutputDirectory = Args.Path;
            RootName = string.IsNullOrEmpty(Args.Name) ? null : Args.Name;
        }

        public async Task<WriterState> WriteAsync(string path, IContent content, CancellationToken cancel = default)
        {
            var name = content.Name;
            var src = ToJson(content);
            var containerPath = (ContainerPath ?? "").TrimStart('/');
            var contentPath = Path.Combine(OutputDirectory, containerPath, path) + ".Content";
            var fileDir = Path.GetDirectoryName(contentPath);
            if (fileDir == null)
                throw new NotSupportedException("The fileDir cannot be null");

            if (!IsDirectoryExists(fileDir))
                CreateDirectory(fileDir);
            var action = File.Exists(contentPath) ? WriterAction.Updated : WriterAction.Created;
            using (var writer = CreateTextWriter(contentPath, false))
                await writer.WriteAsync(src);

            foreach (var attachment in await content.GetAttachmentsAsync())
            {
                var attachmentPath = Path.Combine(fileDir, attachment.FileName);

                var inStream = attachment.Stream;
                if (inStream.Length > 0)
                    using (var outStream = CreateBinaryStream(attachmentPath, FileMode.OpenOrCreate))
                        await inStream.CopyToAsync(outStream, cancel);
            }

            return new WriterState
            {
                WriterPath = contentPath,
                Action = action,
            };
        }

        private string ToJson(IContent content)
        {
            var fields = content.FieldNames
                .Where(fieldName => content[fieldName] != null)
                .ToDictionary(fieldName => fieldName, fieldName => content[fieldName]);

            var model = new
            {
                ContentType = content.Type,
                ContentName = content.Name,
                Fields = fields,
                Permissions = content.Permissions
            };

            var writer = new StringWriter();
            JsonSerializer.Create(new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore
            }).Serialize(writer, model);

            return writer.GetStringBuilder().ToString();
        }

        /* ======================================================= TESTABILITY */

        protected virtual bool IsDirectoryExists(string fsPath)
        {
            return Directory.Exists(fsPath);
        }
        protected virtual void CreateDirectory(string fsPath)
        {
            Directory.CreateDirectory(fsPath);
        }
        protected virtual TextWriter CreateTextWriter(string fsPath, bool append)
        {
            return new StreamWriter(fsPath, append);
        }
        protected virtual Stream CreateBinaryStream(string fsPath, FileMode fileMode)
        {
            return new FileStream(fsPath, fileMode);
        }
    }
}
