using System.Collections.Generic;
using System.Linq;
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
        public string Type { get; set; }
        public PermissionInfo Permissions { get; set; }

        public Task<Attachment[]> GetAttachmentsAsync()
        {
            throw new System.NotImplementedException();
        }
    }
}
