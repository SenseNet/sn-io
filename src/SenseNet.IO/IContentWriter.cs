using System.Threading;
using System.Threading.Tasks;

namespace SenseNet.IO
{
    public interface IContentWriter
    {
        /// <summary>
        /// Gets the absolute path of the target container.
        /// </summary>
        string ContainerPath { get; }
        /// <summary>
        /// Gets the name of the root content. If the value is null, it will be the name of the source root.
        /// </summary>
        string RootName { get; }

        Task WriteAsync(string relativeParentPath, IContent content, CancellationToken cancel = default);
    }
}
