using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SenseNet.IO.Implementations
{
    internal class InitialContent : IContent
    {
        public string[] FieldNames { get; } = new string[0];

        public object this[string fieldName]
        {
            get => null;
            set => throw new NotSupportedException();
        }

        public string Name { get; set; }
        public string Path { get; }
        public string Type { get; }
        public PermissionInfo Permissions { get; set; }

        public InitialContent(string path, string name, string type)
        {
            Path = path;
            Name = name;
            Type = type;
        }

        public Task<Attachment[]> GetAttachmentsAsync()
        {
            return Task.FromResult(Array.Empty<Attachment>());
        }
    }
}
