using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace SenseNet.IO
{
    /// <summary>
    /// Defines an interface for writing items to any content store.
    /// </summary>
    public interface IContentWriter
    {
        /// <summary>
        /// Gets the absolute path of the target container. Cannot be null.
        /// This value is the base path if the "path" parameter of the <see cref="WriteAsync"/> method is relative.
        /// </summary>
        [NotNull]
        string ContainerPath { get; }
        /// <summary>
        /// Gets the name of the root content. If the value is null, it will be the name of the source root.
        /// This value is the pat of the absolute path if the "path" parameter of the <see cref="WriteAsync"/> method is relative.
        /// </summary>
        string RootName { get; }

        /// <summary>
        /// Writes a content to the given path. The path is always a repository path.
        /// The path can be absolute (/Root...) or relative to the <see cref="ContainerPath"/>.
        /// </summary>
        /// <param name="path">Absolute or relative repository path.</param>
        /// <param name="content">The content that will be written.</param>
        /// <param name="cancel">An optional token to monitor for cancellation requests.</param>
        /// <returns>A Task that represents the asynchronous operation and wraps an <see cref="WriterState"/> instance.</returns>
        Task<WriterState> WriteAsync(string path, IContent content, CancellationToken cancel = default);
    }
}
