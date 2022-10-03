using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using SenseNet.IO.Implementations;

namespace SenseNet.IO
{
    /// <summary>
    /// Defines members of a filesystem reader.
    /// </summary>
    public interface IFilesystemReader : IContentReader
    {
        /// <summary>
        /// Filesystem reader options.
        /// </summary>
        FsReaderArgs ReaderOptions { get; }
    }

    /// <summary>
    /// Defines members of a repository reader.
    /// </summary>
    public interface ISnRepositoryReader : IContentReader
    {
        /// <summary>
        /// Repository reader options.
        /// </summary>
        RepositoryReaderArgs ReaderOptions { get; }
    }

    /// <summary>
    /// Defines an interface for reading items from any content store.
    /// </summary>
    public interface IContentReader : ISnInitializable
    {
        /// <summary>
        /// Gets the name of the copied subtree. Cannot be null.
        /// </summary>
        [NotNull]
        string RootName { get; }
        /// <summary>
        /// Gets the count of contents in the whole subtree for progress computing.
        /// </summary>
        int EstimatedCount { get; }

        /// <summary>
        /// Gets the current content after calling any reader method
        /// (see <see cref="ReadSubTreeAsync"/>, <see cref="ReadAllAsync"/> and <see cref="ReadByReferenceUpdateTasksAsync"/>).
        /// </summary>
        IContent Content { get; }

        /// <summary>
        /// Gets the relative repository path of the current content after calling any reader method
        /// (see <see cref="ReadSubTreeAsync"/>, <see cref="ReadAllAsync"/> and <see cref="ReadByReferenceUpdateTasksAsync"/>).
        /// The path is based on the copied subtree so the relative path of the copied subtree's root is empty string.
        /// The separator should be slash ("/").
        /// </summary>
        string RelativePath { get; }

        Task<bool> ReadSubTreeAsync(string relativePath, CancellationToken cancel = default);

        /// <summary>
        /// Reads a forward-only stream of a content-subtree under the requested logical path stored in RootPath property.
        /// This method need to skip all ContentType, Settings and Aspect contents. The root paths of skipped subtrees comes
        /// from the <paramref name="contentsWithoutChildren"/>. These subtree roots need to be read without their children.
        /// The start position is before the first content. Therefore, you must call ReadAsync to begin accessing data.
        /// Every call actualizes the <see cref="Content"/> and <see cref="RelativePath"/> properties.
        /// </summary>
        /// <param name="contentsWithoutChildren">An array that contains relative paths of contents that should be read
        /// but their children should be skipped. The parameter is required but can be empty.</param>
        /// <param name="cancel">The token to monitor for cancellation requests.</param>
        /// <returns>A Task that represents the asynchronous operation and wraps a boolean value that is true
        /// if there are more items or false if there aren't.</returns>
        Task<bool> ReadAllAsync(string[] contentsWithoutChildren, CancellationToken cancel = default);

        void SetReferenceUpdateTasks(IEnumerable<TransferTask> tasks, int taskCount);

        Task<bool> ReadByReferenceUpdateTasksAsync(CancellationToken cancel);
    }
}
