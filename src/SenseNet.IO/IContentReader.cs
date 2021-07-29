using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace SenseNet.IO
{
    /// <summary>
    /// Defines an interface for reading items from any content store.
    /// </summary>
    public interface IContentReader<out TItem> where TItem : IContent
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
        TItem Content { get; }

        /// <summary>
        /// Gets the relative repository path of the current content after calling the <see cref="ReadAsync"/> method.
        /// The path is based on the <see cref="RootPath"/>.
        /// </summary>
        string RelativePath { get; }

        /// <summary>
        /// Reads a forward-only stream of a content-subtree from any content storage.
        /// The start position is before the first content. Therefore, you must call ReadAsync to begin accessing data.
        /// Every call actualizes the <see cref="Content"/> and <see cref="RelativePath"/> properties.
        /// </summary>
        /// <param name="cancel">An optional token to monitor for cancellation requests.</param>
        /// <returns>A Task that represents the asynchronous operation.</returns>
        Task<bool> ReadAsync(CancellationToken cancel = default);
    }
}
