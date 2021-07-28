using System;
using System.Threading;
using System.Threading.Tasks;

namespace SenseNet.IO
{
    interface IContentFlow<out TItem> where TItem : IContent
    {
        IContentReader<TItem> Reader { get; }
        IContentWriter Writer { get; }

        Task TransferAsync(IProgress<double> progress, CancellationToken cancel = default);
    }
}
