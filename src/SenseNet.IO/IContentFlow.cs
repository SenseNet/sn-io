using System;
using System.Threading;
using System.Threading.Tasks;

namespace SenseNet.IO
{
    public interface IImportContentFlow : IContentFlow
    {
        new IFilesystemReader Reader { get; }
        new ISnRepositoryWriter Writer { get; }
    }

    public interface IExportContentFlow : IContentFlow
    {
        new ISnRepositoryReader Reader { get; }
        new IFilesystemWriter Writer { get; }
    }

    public interface ICopyContentFlow : IContentFlow
    {
        new IFilesystemReader Reader { get; }
        new IFilesystemWriter Writer { get; }
    }

    public interface ISynchronizeContentFlow : IContentFlow
    {
        new ISnRepositoryReader Reader { get; }
        new ISnRepositoryWriter Writer { get; }
    }

    /// <summary>
    /// Defines members of a content flow.
    /// </summary>
    public interface IContentFlow : ISnInitializable
    {
        /// <summary>
        /// Gets the reader.
        /// </summary>
        IContentReader Reader { get; }
        /// <summary>
        /// Gets the writer.
        /// </summary>
        IContentWriter Writer { get; }

        /// <summary>
        /// Transfers content items from the source (using the reader) to the target (using the writer).
        /// </summary>
        /// <param name="progress"></param>
        /// <param name="cancel"></param>
        /// <returns></returns>
        Task TransferAsync(IProgress<TransferState> progress, CancellationToken cancel = default);
    }
}
