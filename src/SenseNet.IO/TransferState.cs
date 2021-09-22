namespace SenseNet.IO
{
    public class TransferState
    {
        public string CurrentBatchAction { get; set; }
        public int ErrorCount { get; set; }
        public bool UpdatingReferences { get; set; }
        public int CurrentCount { get; set; }
        public int ContentCount { get; set; }
        public int UpdateTaskCount { get; set; }
        public double Percent
        {
            get
            {
                var total = ContentCount + UpdateTaskCount;
                return total == 0 ? 0 : CurrentCount * 100.0 / total;
            }
        }
        public WriterState State { get; set; }
    }
}
