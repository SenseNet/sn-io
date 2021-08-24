using System;
using System.Linq;
using System.Threading;
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

        public async Task TransferAsync(IProgress<(string Path, double Percent)> progress = null, CancellationToken cancel = default)
        {
            var count = 0;

            var rootName = Writer.RootName ?? ContentPath.GetName(Reader.RootPath);
            if (await Reader.ReadAsync(cancel))
            {
                if (Writer.RootName != null)
                    Rename(Reader.Content, rootName);
                await Writer.WriteAsync(ContentPath.Combine(rootName, Reader.RelativePath), Reader.Content, cancel);
                Progress(Reader.RelativePath, ref count, progress);

                while (await Reader.ReadAsync(cancel))
                {
                    await Writer.WriteAsync(ContentPath.Combine(rootName, Reader.RelativePath), Reader.Content, cancel);
                    Progress(Reader.RelativePath, ref count, progress);
                }
            }
        }

        private void Progress(string path, ref int count, IProgress<(string Path, double Percent)> progress = null)
        {
            ++count;
            var totalCount = Reader.EstimatedCount;
            if (totalCount > 0)
                progress?.Report((path, count * 100.0 / totalCount));
        }

        private void Rename(IContent content, string newName)
        {
            if (content.FieldNames.Contains("Name"))
                content["Name"] = newName;
            content.Name = newName;
        }
    }
}
