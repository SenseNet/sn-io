using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace SenseNet.IO
{
    /// <summary>
    /// Defines an interface for reading items from any content store.
    /// </summary>
    public interface IContentReader
    {
        /// <summary>
        /// Gets the absolute repository path of the copied subtree. Cannot be null.
        /// </summary>
        [NotNull]
        string RootPath { get; }
        /// <summary>
        /// Gets the count of contents in the whole subtree for progress computing.
        /// </summary>
        int EstimatedCount { get; }

        /// <summary>
        /// Gets the current content after calling the <see cref="ReadAsync"/> method.
        /// </summary>
        IContent Content { get; }

        /// <summary>
        /// Gets the relative repository path of the current content after calling the <see cref="ReadAsync"/> method.
        /// The path is based on the <see cref="RootPath"/>.
        /// </summary>
        string RelativePath { get; }

        /// <summary>
        /// Reads a forward-only stream of the ContentType subtree under the /Root/System/Schema/ContentTypes logical path.
        /// The start position is before the first content. Therefore, you must call ReadAsync to begin accessing data.
        /// Every call actualizes the <see cref="Content"/> and <see cref="RelativePath"/> properties.
        /// If the ContentTypes folder is out of reader's scope or there is no any ContentType in the source tree,
        /// the first call should return false.
        /// </summary>
        /// <param name="cancel">The token to monitor for cancellation requests.</param>
        /// <returns>A Task that represents the asynchronous operation and wraps a boolean value that is true
        /// if there are more items or false if there aren't.</returns>
        Task<bool> ReadContentTypesAsync(CancellationToken cancel = default);
        /// <summary>
        /// Reads a forward-only stream of the Settings subtree under the /Root/System/Settings logical path.
        /// The start position is before the first content. Therefore, you must call ReadAsync to begin accessing data.
        /// Every call actualizes the <see cref="Content"/> and <see cref="RelativePath"/> properties.
        /// If the Settings folder is out of reader's scope or there is no any Settings in the source tree,
        /// the first call should return false.
        /// </summary>
        /// <param name="cancel">The token to monitor for cancellation requests.</param>
        /// <returns>A Task that represents the asynchronous operation and wraps a boolean value that is true
        /// if there are more items or false if there aren't.</returns>
        Task<bool> ReadSettingsAsync(CancellationToken cancel = default);
        /// <summary>
        /// Reads a forward-only stream of the Aspects subtree under the /Root/System/Schema/Aspects logical path.
        /// The start position is before the first content. Therefore, you must call ReadAsync to begin accessing data.
        /// Every call actualizes the <see cref="Content"/> and <see cref="RelativePath"/> properties.
        /// If the Aspects folder is out of reader's scope or there is no any Aspect in the source tree,
        /// the first call should return false.
        /// </summary>
        /// <param name="cancel">The token to monitor for cancellation requests.</param>
        /// <returns>A Task that represents the asynchronous operation and wraps a boolean value that is true
        /// if there are more items or false if there aren't.</returns>
        Task<bool> ReadAspectsAsync(CancellationToken cancel = default);
        /// <summary>
        /// Reads a forward-only stream of a content-subtree under the requested logical path.
        /// This method need to skip all ContentType, Settings and Aspect contents.
        /// The start position is before the first content. Therefore, you must call ReadAsync to begin accessing data.
        /// Every call actualizes the <see cref="Content"/> and <see cref="RelativePath"/> properties.
        /// </summary>
        /// <param name="cancel">The token to monitor for cancellation requests.</param>
        /// <returns>A Task that represents the asynchronous operation and wraps a boolean value that is true
        /// if there are more items or false if there aren't.</returns>
        Task<bool> ReadAllAsync(CancellationToken cancel = default);

        void SetReferenceUpdateTasks(IEnumerable<TransferTask> tasks, int taskCount);

        Task<bool> ReadByReferenceUpdateTasksAsync(CancellationToken cancel);
    }
}
