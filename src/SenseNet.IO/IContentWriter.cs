
namespace SenseNet.IO
{
    public interface IContentWriter
    {
        void Write(string path, IContent content);
    }
}
