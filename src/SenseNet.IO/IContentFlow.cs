using System;
using System.Threading;
using System.Threading.Tasks;

namespace SenseNet.IO
{
    public interface IContentFlow
    {
        IContentReader Reader { get; }
        IContentWriter Writer { get; }

        Task TransferAsync(IProgress<TransferState> progress, CancellationToken cancel = default);
    }
}
