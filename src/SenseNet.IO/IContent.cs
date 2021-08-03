using System.Threading.Tasks;

namespace SenseNet.IO
{
    public interface IContent
    {
        string[] FieldNames { get; }
        object this[string fieldName] { get; set; }

        public string Name { get; set; }
        public string Path { get; set; }
        public string Type { get; set; }
        public PermissionInfo Permissions { get; set; }

        Task<Attachment[]> GetAttachmentsAsync();
    }
}
