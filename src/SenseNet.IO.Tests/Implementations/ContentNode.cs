using System.Collections.Generic;
using System.Diagnostics;

namespace SenseNet.IO.Tests.Implementations
{
    [DebuggerDisplay("{Path}")]
    public class ContentNode : IContent
    {
        private readonly Dictionary<string, object> _fields = new();

        public object this[string fieldName]
        {
            get => _fields.TryGetValue(fieldName, out var value) ? value : null;
            set => _fields[fieldName] = value;
        }

        public string Path { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }

        public ContentNode Parent { get; set; }
        public List<ContentNode> Children { get; } = new List<ContentNode>();
    }
}
