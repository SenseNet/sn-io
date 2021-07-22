using System;
using System.Threading.Tasks;

namespace SenseNet.IO
{
    interface IContentFlow
    {
        IContentReader Reader { get; }
        IContentWriter Writer { get; }

        Task TransferAsync(string sourcePath, string targetPath, IProgress<double> progress = null);
    }
}
