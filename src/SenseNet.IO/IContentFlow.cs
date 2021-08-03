using System;
using System.Threading;
using System.Threading.Tasks;

namespace SenseNet.IO
{
    interface IContentFlow
    {
        IContentReader Reader { get; }
        IContentWriter Writer { get; }

        Task TransferAsync(IProgress<double> progress, CancellationToken cancel = default);
    }
}
