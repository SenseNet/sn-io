using System.Collections.Generic;

namespace SenseNet.IO
{
    public class FileSystemContentReader : IContentReader
    {
        public int EstimatedCount { get; } = -1;

        public IEnumerable<IContent> Read(string path)
        {
            throw new System.NotImplementedException();
        }
    }
}
