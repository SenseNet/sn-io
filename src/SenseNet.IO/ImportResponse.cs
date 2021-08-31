namespace SenseNet.IO
{
    public enum ImporterAction { Unknown, Create, Update, Error }

    public class ImportResponse
    {
        public string WriterPath { get; set; }
        public string[] BrokenReferences { get; set; } = new string[0];
        public bool RetryPermissions { get; set; }
        public string[] Messages { get; set; } = new string[0];
        public ImporterAction Action { get; set; }
    }
}
