using System;
using System.Threading;
using System.Threading.Tasks;

namespace SenseNet.IO
{
    public class ContentFlow<TItem> : IContentFlow<TItem> where TItem : IContent
    {
        public IContentReader<TItem> Reader { get; }
        public IContentWriter Writer { get; }
        public ContentFlow(IContentReader<TItem> reader, IContentWriter writer)
        {
            Reader = reader;
            Writer = writer;
        }

        public async Task TransferAsync(IProgress<double> progress, CancellationToken cancel = default)
        {
            var count = 0;
            var totalCount = Reader.EstimatedCount;

            var rootName = Writer.RootName ?? ContentPath.GetName(Reader.RootPath);
            while (await Reader.ReadAsync(cancel))
            {
                await Writer.WriteAsync(ContentPath.Combine(rootName, Reader.RelativePath), Reader.Content, cancel);
                ++count;
                progress?.Report(count * 100.0 / totalCount);
            }

        }
    }
}
