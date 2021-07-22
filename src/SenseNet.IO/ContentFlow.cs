using System;
using System.Threading.Tasks;

namespace SenseNet.IO
{
    public class ContentFlow : IContentFlow
    {
        public IContentReader Reader { get; }
        public IContentWriter Writer { get; }
        public ContentFlow(IContentReader reader, IContentWriter writer)
        {
            Reader = reader;
            Writer = writer;
        }

        public Task TransferAsync(string sourcePath, string targetPath, IProgress<double> progress = null)
        {
            var count = 0;
            var totalCount = Reader.EstimatedCount;
            foreach (var content in Reader.Read(sourcePath))
            {
                Writer.Write(content.Path, content);
                ++count;
                progress?.Report(count * 100.0 / totalCount);
            }

            return Task.CompletedTask;
        }
    }
}
