using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SenseNet.IO
{
    public interface IContentReader<out TItem> where TItem : IContent
    {
        int EstimatedCount { get; }

        TItem Content { get; }

        Task<bool> ReadAsync(CancellationToken cancel = default);
    }
}
