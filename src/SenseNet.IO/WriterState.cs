namespace SenseNet.IO
{
    public enum WriterAction { Unknown, Created, Creating, Updated, Updating, MissingParent, Failed, Skipped, CutOff }

    public class WriterState
    {
        public string ReaderPath { get; set; }
        public string WriterPath { get; set; }
        public string[] BrokenReferences { get; set; } = new string[0];
        public bool RetryPermissions { get; set; }
        public string[] Messages { get; set; } = new string[0];
        public WriterAction Action { get; set; }

        public bool UpdateRequired => BrokenReferences.Length > 0 || RetryPermissions;
    }

    public class TransferTask
    {
        public string ReaderPath { get; set; }
        public string WriterPath { get; set; }
        public string[] BrokenReferences { get; set; } = new string[0];
        public bool RetryPermissions { get; set; }
    }
}
