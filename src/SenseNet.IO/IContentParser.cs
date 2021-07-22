using System.IO;

namespace SenseNet.IO
{
    public interface IContentParser
    {
        IContent ParseContent(Stream stream);
        IContent ParseContent(TextReader reader);
        IContent ParseContent(string src);
    }
}
