using SenseNet.IO.Implementations;

namespace SenseNet.IO.CLI
{
    public enum Verb
    {
        Export,
        Import,
        Copy,
        Sync,
        Transfer
    };

    public interface IAppArguments
    {
        Verb Verb { get; }
    }

    public class ExportArguments : IAppArguments
    {
        public Verb Verb => Verb.Export;
        public RepositoryReaderArgs ReaderArgs { get; set; }
        public FsWriterArgs WriterArgs { get; set; }
    }

    public class ImportArguments : IAppArguments
    {
        public Verb Verb => Verb.Import;
        public FsReaderArgs ReaderArgs { get; set; }
        public RepositoryWriterArgs WriterArgs { get; set; }
    }

    public class CopyArguments : IAppArguments
    {
        public Verb Verb => Verb.Copy;
        public FsReaderArgs ReaderArgs { get; set; }
        public FsWriterArgs WriterArgs { get; set; }
    }

    public class SyncArguments : IAppArguments
    {
        public Verb Verb => Verb.Sync;
        public RepositoryReaderArgs ReaderArgs { get; set; }
        public RepositoryWriterArgs WriterArgs { get; set; }
    }
}