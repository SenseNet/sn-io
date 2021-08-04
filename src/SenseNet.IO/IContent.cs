﻿using System.Threading.Tasks;

namespace SenseNet.IO
{
    public interface IContent
    {
        string[] FieldNames { get; }
        object this[string fieldName] { get; set; }

        public string Name { get; }
        public string Path { get; }
        public string Type { get; }
        public PermissionInfo Permissions { get; set; }

        Task<Attachment[]> GetAttachmentsAsync();
    }
}
