namespace SenseNet.IO
{
    public class TransferProgress
    {
        public int CurrentCount { get; set; }
        public int TotalCount { get; set; }
        public double Percent => TotalCount == 0 ? 0 : CurrentCount * 100.0 / TotalCount;
        public TransferState State { get; set; }
    }
}
