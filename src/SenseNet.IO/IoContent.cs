using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SenseNet.IO
{
    public class IoContent : IContent
    {
        private readonly Dictionary<string, object> _fields = new();

        public string[] FieldNames => _fields.Keys.ToArray();

        public object this[string fieldName]
        {
            get => _fields.TryGetValue(fieldName, out var value) ? value : null;
            set => _fields[fieldName] = value;
        }

        public string Path { get; set; }
        public string Name { get; set; }
        public bool CutOff { get; set; }
        public string Type { get; set; }
        public PermissionInfo Permissions { get; set; }
        public bool IsFolder => throw new NotImplementedException("IsFolder is not implemented in IoContent.");
        public bool HasData => _fields.Any();

        public Task<Attachment[]> GetAttachmentsAsync(CancellationToken cancel)
        {
            throw new NotImplementedException();
        }
    }
}
