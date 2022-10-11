using System;

namespace SenseNet.IO.Implementations
{
    // The classes in this file represent FACTORIES for the main types of content flow:
    // IMPORT, EXPORT, COPY, SYNCHRONIZE
    // They require the appropriate configure methods to be registered as services
    // (see the Func parameters of the constructors).
    // To avoid the service locator pattern we register these functions and call them
    // when the caller requires a new flow instance. The factory methods have
    // two configuration method parameters and must return a flow instance
    // of the appropriate type.

    internal class ImportFlowFactory : IImportFlowFactory
    {
        private readonly Func<Action<FsReaderArgs>, Action<RepositoryWriterArgs>, IImportContentFlow> _factory;

        public ImportFlowFactory(Func<Action<FsReaderArgs>, Action<RepositoryWriterArgs>, IImportContentFlow> factory)
        {
            _factory = factory;
        }

        public IContentFlow Create(Action<FsReaderArgs> configureReader = null, Action<RepositoryWriterArgs> configureWriter = null)
        {
            return _factory(configureReader, configureWriter);
        }
    }

    internal class ExportFlowFactory : IExportFlowFactory
    {
        private readonly Func<Action<RepositoryReaderArgs>, Action<FsWriterArgs>, IExportContentFlow> _factory;

        public ExportFlowFactory(Func<Action<RepositoryReaderArgs>, Action<FsWriterArgs>, IExportContentFlow> factory)
        {
            _factory = factory;
        }

        public IContentFlow Create(Action<RepositoryReaderArgs> configureReader = null, Action<FsWriterArgs> configureWriter = null)
        {
            return _factory(configureReader, configureWriter);
        }
    }

    internal class CopyFlowFactory : ICopyFlowFactory
    {
        private readonly Func<Action<FsReaderArgs>, Action<FsWriterArgs>, ICopyContentFlow> _factory;

        public CopyFlowFactory(Func<Action<FsReaderArgs>, Action<FsWriterArgs>, ICopyContentFlow> factory)
        {
            _factory = factory;
        }

        public IContentFlow Create(Action<FsReaderArgs> configureReader = null, Action<FsWriterArgs> configureWriter = null)
        {
            return _factory(configureReader, configureWriter);
        }
    }

    internal class SynchronizeFlowFactory : ISynchronizeFlowFactory
    {
        private readonly Func<Action<RepositoryReaderArgs>, Action<RepositoryWriterArgs>, ISynchronizeContentFlow> _factory;

        public SynchronizeFlowFactory(Func<Action<RepositoryReaderArgs>, Action<RepositoryWriterArgs>, ISynchronizeContentFlow> factory)
        {
            _factory = factory;
        }

        public IContentFlow Create(Action<RepositoryReaderArgs> configureReader = null, Action<RepositoryWriterArgs> configureWriter = null)
        {
            return _factory(configureReader, configureWriter);
        }
    }
}
