using System;
using SenseNet.IO.Implementations;

namespace SenseNet.IO
{
    /// <summary>
    /// Defines members for a factory for creating import content flows on-the-fly.
    /// </summary>
    public interface IImportFlowFactory
    {
        /// <summary>
        /// Creates an import content flow.
        /// </summary>
        /// <param name="configureReader">Configuration method for the reader.</param>
        /// <param name="configureWriter">Configuration method for the writer.</param>
        /// <returns>A new <see cref="IContentFlow"/> instance.</returns>
        IContentFlow Create(Action<FsReaderArgs> configureReader = null, Action<RepositoryWriterArgs> configureWriter = null);
    }

    /// <summary>
    /// Defines members for a factory for creating export content flows on-the-fly.
    /// </summary>
    public interface IExportFlowFactory
    {
        /// <summary>
        /// Creates an export content flow.
        /// </summary>
        /// <param name="configureReader">Configuration method for the reader.</param>
        /// <param name="configureWriter">Configuration method for the writer.</param>
        /// <returns>A new <see cref="IContentFlow"/> instance.</returns>
        IContentFlow Create(Action<RepositoryReaderArgs> configureReader = null, Action<FsWriterArgs> configureWriter = null);
    }

    /// <summary>
    /// Defines members for a factory for creating copy content flows on-the-fly.
    /// </summary>
    public interface ICopyFlowFactory
    {
        /// <summary>
        /// Creates a copy content flow.
        /// </summary>
        /// <param name="configureReader">Configuration method for the reader.</param>
        /// <param name="configureWriter">Configuration method for the writer.</param>
        /// <returns>A new <see cref="IContentFlow"/> instance.</returns>
        IContentFlow Create(Action<FsReaderArgs> configureReader = null, Action<FsWriterArgs> configureWriter = null);
    }

    /// <summary>
    /// Defines members for a factory for creating synchronizer content flows on-the-fly.
    /// </summary>
    public interface ISynchronizeFlowFactory
    {
        /// <summary>
        /// Creates a synchronizer content flow.
        /// </summary>
        /// <param name="configureReader">Configuration method for the reader.</param>
        /// <param name="configureWriter">Configuration method for the writer.</param>
        /// <returns>A new <see cref="IContentFlow"/> instance.</returns>
        IContentFlow Create(Action<RepositoryReaderArgs> configureReader = null, Action<RepositoryWriterArgs> configureWriter = null);
    }
}