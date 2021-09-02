namespace SenseNet.IO
{
    public class TransferState
    {
        public string CurrentBatchAction { get; set; }
        public int ErrorCount { get; set; }
        public bool UpdatingReferences { get; set; }
        public int CurrentCount { get; set; }
        public int TotalCount { get; set; }
        public double Percent => TotalCount == 0 ? 0 : CurrentCount * 100.0 / TotalCount;
        public WriterState State { get; set; }
    }
}
