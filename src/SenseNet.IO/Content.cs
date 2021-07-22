using System.Collections.Generic;

namespace SenseNet.IO
{
    public class Content : IContent
    {
        private readonly Dictionary<string, object> _fields = new();

        public object this[string fieldName]
        {
            get => _fields.TryGetValue(fieldName, out var value) ? value : null;
            set => _fields[fieldName] = value;
        }

        public string Path { get; set; }
        public string ParentPath { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
    }
}
