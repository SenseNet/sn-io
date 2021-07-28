using System.Threading;
using System.Threading.Tasks;

namespace SenseNet.IO
{
    public interface IContentWriter
    {
        Task WriteAsync(string relativePath, IContent content, CancellationToken cancel = default);
    }
}
