using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace SenseNet.IO.Implementations
{
    public class FsWriterArgs
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public bool? Flatten { get; set; }

        internal FsWriterArgs Clone()
        {
            return new FsWriterArgs
            {
                Path = Path,
                Name = Name,
                Flatten = Flatten
            };
        }
    }

    public class FsWriter : IFilesystemWriter
    {
        public FsWriterArgs Args { get; }
        public string OutputDirectory => Args.Path;
        public string ContainerPath => "/";
        public string RootName => Args.Name;
        public bool Flatten => Args.Flatten == true;

        public FsWriterArgs WriterOptions => Args;

        private readonly ILogger _logger;

        public FsWriter(IOptions<FsWriterArgs> args, ILogger<FsWriter> logger)
        {
            if (args?.Value == null)
                throw new ArgumentNullException(nameof(args));
            Args = args.Value.Clone();
            
            _logger = logger;
        }

        public virtual Task InitializeAsync()
        {
            if (OutputDirectory == null)
                throw new ArgumentException("FsWriter: Invalid target container path.");

            return Task.CompletedTask;
        }

        public async Task<WriterState> WriteAsync(string path, IContent content, CancellationToken cancel = default)
        {
            if (OutputDirectory == null)
                throw new ArgumentException("FsWriter: Invalid target container path.");

            if (Flatten)
                return await WriteFlattenedAsync(path, content, cancel);

            var containerPath = (ContainerPath ?? "").TrimStart('/');
            var contentPath = Path.Combine(OutputDirectory, containerPath, path) + ".Content";
            var fileDir = Path.GetDirectoryName(contentPath);
            if (fileDir == null)
                throw new NotSupportedException("The fileDir cannot be null");

            if (!IsDirectoryExists(fileDir))
                CreateDirectory(fileDir);
            var action = File.Exists(contentPath) ? WriterAction.Updated : WriterAction.Created;

            var src = ToJson(content);
            using (var writer = CreateTextWriter(contentPath, false))
                await writer.WriteAsync(src);
            
            try
            {
                await WriteAttachmentsAsync(fileDir, content, cancel);
            }
            catch (Exception ex)
            {
                //TODO: what can be done in case of an attachment error?
                // Should we delete the content file? Maybe only if it did not exist before?
                _logger.LogError(ex, $"Error during writing attachments of {contentPath}.");

                return new WriterState
                {
                    WriterPath = contentPath,
                    Action = WriterAction.Failed,
                };
            }

            return new WriterState
            {
                WriterPath = contentPath,
                Action = action,
            };
        }

        public Task<bool> IsContentExists(string path, CancellationToken cancel = default)
        {
            //UNDONE: NotImplementedException: FsWriter.IsContentExists
            throw new NotImplementedException();
        }

        public async Task<WriterState> WriteFlattenedAsync(string path, IContent content, CancellationToken cancel = default)
        {
            if (!IsDirectoryExists(OutputDirectory))
                CreateDirectory(OutputDirectory);

            var contentName = ContentPath.GetName(path);
            var fileName = Path.GetFileNameWithoutExtension(contentName);
            var ext = Path.GetExtension(contentName);

            var index = 0;
            string contentPath = null;
            string newContentName = null;
            do
            {
                var suffix = index == 0 ? string.Empty : $"({index})";
                newContentName = fileName + suffix + ext;
                index++;
                contentPath = Path.Combine(OutputDirectory, newContentName) + ".Content";
            } while (IsFileExists(contentPath));

            if (contentName != newContentName)
                content.Name = newContentName;

            var src = ToJson(content);
            using (var writer = CreateTextWriter(contentPath, false))
                await writer.WriteAsync(src);

            try
            {
                await WriteAttachmentsAsync(OutputDirectory, content, cancel);
            }
            catch (Exception ex)
            {
                //TODO: what can be done in case of an attachment error?
                // Should we delete the content file? Maybe only if it did not exist before?
                _logger.LogError(ex, $"Error during writing attachments of {contentPath}.");

                return new WriterState
                {
                    WriterPath = contentPath,
                    Action = WriterAction.Failed,
                };
            }

            return new WriterState
            {
                WriterPath = contentPath,
                Action = WriterAction.Created,
            };
        }

        private async Task WriteAttachmentsAsync(string metaFileDir, IContent content, CancellationToken cancel)
        {
            var attachments = await content.GetAttachmentsAsync();
            foreach (var attachment in attachments.Where(a => a.Stream != null))
            {
                var attachmentPath = Path.Combine(metaFileDir, attachment.FileName);

                var inStream = attachment.Stream;
                if (inStream.Length > 0)
                    using (var outStream = CreateBinaryStream(attachmentPath, FileMode.Create))
                        await inStream.CopyToAsync(outStream, cancel);
            }
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
        protected virtual bool IsFileExists(string fsPath)
        {
            return File.Exists(fsPath);
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
