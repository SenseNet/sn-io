namespace SenseNet.IO
{
    public interface IContent
    {
        object this[string fieldName] { get; set; }

        public string Path { get; set; }
        public string ParentPath { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
    }
}
