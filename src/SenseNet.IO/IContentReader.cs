using System.Collections.Generic;

namespace SenseNet.IO
{
    public interface IContentReader
    {
        IEnumerable<IContent> Read(string path);
    }
}
