using System;
using System.Threading;
using System.Threading.Tasks;

namespace SenseNet.IO
{
    public class ContentFlow<TItem> : IContentFlow<TItem> where TItem : IContent
    {
        public IContentReader Reader { get; }
        public IContentWriter Writer { get; }
        public ContentFlow(IContentReader reader, IContentWriter writer)
        {
            Reader = reader;
            Writer = writer;
        }

        public async Task TransferAsync(IProgress<double> progress, CancellationToken cancel = default)
        {
            var count = 0;

            var rootName = Writer.RootName ?? ContentPath.GetName(Reader.RootPath);
            while (await Reader.ReadAsync(cancel))
            {
                await Writer.WriteAsync(ContentPath.Combine(rootName, Reader.RelativePath), Reader.Content, cancel);
                ++count;

                var totalCount = Reader.EstimatedCount;
                if(totalCount > 0)
                    progress?.Report(count * 100.0 / totalCount);
            }

        }
    }
}
