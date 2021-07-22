using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
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
