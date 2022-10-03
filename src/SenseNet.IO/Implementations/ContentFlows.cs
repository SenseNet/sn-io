using Microsoft.Extensions.Logging;

// ReSharper disable SuggestBaseTypeForParameterInConstructor

namespace SenseNet.IO.Implementations
{
    // The classes in this file represent the main types of content flows:
    // IMPORT, EXPORT, COPY, SYNCHRONIZE
    // They are necessary because they require different types of readers and writers (see the constructors).
    // The caller will not see these internal classes directly. They can either load these services
    // by their interfaces (e.g. IImportContentFlow) or by creating them through the appropriate
    // factory service (e.g. IImportFlowFactory).

    internal class ImportContentFlow : SemanticContentFlow, IImportContentFlow
    {
        public new IFilesystemReader Reader => (IFilesystemReader)base.Reader;

        public ImportContentFlow(IFilesystemReader reader, ISnRepositoryWriter writer, ILogger<ImportContentFlow> logger) : 
            base(reader, writer, logger)
        {
        }
    }

    internal class ExportContentFlow : SimpleContentFlow, IExportContentFlow
    {
        public new ISnRepositoryReader Reader => (ISnRepositoryReader)base.Reader;
        public new IFilesystemWriter Writer => (IFilesystemWriter)base.Writer;

        public ExportContentFlow(ISnRepositoryReader reader, IFilesystemWriter writer, ILogger<ExportContentFlow> logger) :
            base(reader, writer, logger)
        {
        }
    }

    internal class CopyContentFlow : SimpleContentFlow, ICopyContentFlow
    {
        public new IFilesystemReader Reader => (IFilesystemReader)base.Reader;
        public new IFilesystemWriter Writer => (IFilesystemWriter)base.Writer;

        public CopyContentFlow(IFilesystemReader reader, IFilesystemWriter writer, ILogger<CopyContentFlow> logger) :
            base(reader, writer, logger)
        {
        }
    }

    internal class SynchronizeContentFlow : SemanticContentFlow, ISynchronizeContentFlow
    {
        public new ISnRepositoryReader Reader => (ISnRepositoryReader)base.Reader;

        public SynchronizeContentFlow(ISnRepositoryReader reader, ISnRepositoryWriter writer,
            ILogger<SynchronizeContentFlow> logger) : base(reader, writer, logger)
        {
        }
    }
}