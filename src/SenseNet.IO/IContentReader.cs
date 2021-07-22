using System.Collections.Generic;

namespace SenseNet.IO
{
    public interface IContentReader
    {
        int EstimatedCount { get; }

        IEnumerable<IContent> Read(string path);
    }
}
