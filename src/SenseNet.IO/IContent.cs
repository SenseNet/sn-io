using System.Threading;
using System.Threading.Tasks;

namespace SenseNet.IO
{
    public interface IContent
    {
        string[] FieldNames { get; }
        object this[string fieldName] { get; set; }

        public string Name { get; set; }
        public string Path { get; }
        public bool CutOff { get; set; }
        public string Type { get; }
        public PermissionInfo Permissions { get; set; }

        public bool IsFolder { get; }
        public bool HasData { get; }

        Task<Attachment[]> GetAttachmentsAsync(CancellationToken cancel);
    }
}
