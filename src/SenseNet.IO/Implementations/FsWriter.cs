﻿using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SenseNet.IO.Implementations
{
    public class FsWriter : IContentWriter
    {
        private readonly string _outputDirectory;
        public string ContainerPath { get; }
        public string RootName { get; }

        public FsWriter(string outputDirectory, string containerPath = null, string rootName = null)
        {
            _outputDirectory = outputDirectory;
            ContainerPath = containerPath;
            RootName = rootName;
        }

        public async Task WriteAsync(string path, IContent content, CancellationToken cancel = default)
        {
            var name = content.Name;
            var src = ToJson(content);
            var containerPath = (ContainerPath ?? "").TrimStart('/');
            var contentPath = Path.Combine(_outputDirectory, containerPath, path) + ".Content";
            var fileDir = Path.GetDirectoryName(contentPath);
            if (fileDir == null)
                throw new NotSupportedException("The fileDir cannot be null");

            if (!IsDirectoryExists(fileDir))
                CreateDirectory(fileDir);
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