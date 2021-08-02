using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

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
            var src = content.ToJson();
            var contentPath = Path.Combine(_outputDirectory, ContainerPath ?? "", path) + ".Content";
            var fileDir = Path.GetDirectoryName(contentPath);
            if (fileDir == null)
                throw new NotSupportedException("The fileDir cannot be null");

            if (!Directory.Exists(fileDir))
                Directory.CreateDirectory(fileDir);
            using (var writer = new StreamWriter(contentPath, false))
                await writer.WriteAsync(src);

            foreach (var attachment in await content.GetAttachmentsAsync())
            {
                var attachmentPath = Path.Combine(fileDir, attachment.FileName);

                var inStream = attachment.Stream;
                if (inStream.Length > 0)
                    using (var outStream = new FileStream(attachmentPath, FileMode.OpenOrCreate))
                        await inStream.CopyToAsync(outStream, cancel);
            }
        }
    }
}
