using System;
using System.Threading;
using System.Threading.Tasks;

namespace SenseNet.IO
{
    public interface IContentFlow
    {
        IContentReader Reader { get; }
        IContentWriter Writer { get; }

        void WriteLogHead(string head); //UNDONE:LOG: Remove after the final logger integration.
        Task TransferAsync(IProgress<TransferState> progress, CancellationToken cancel = default);
    }
}
