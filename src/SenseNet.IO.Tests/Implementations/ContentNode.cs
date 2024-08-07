﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SenseNet.IO.Tests.Implementations
{
    [DebuggerDisplay("{Name} ({Path})")]
    public class ContentNode : IContent
    {
        private readonly Dictionary<string, object> _fields = new();

        public string[] FieldNames => _fields.Keys.ToArray();

        public object this[string fieldName]
        {
            get => _fields.TryGetValue(fieldName, out var value) ? value : null;
            set => _fields[fieldName] = value;
        }

        public string Path { get; set; }
        public bool CutOff { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public PermissionInfo Permissions { get; set; }
        public bool IsFolder => throw new NotImplementedException();
        public bool HasData => throw new NotImplementedException();

        public Task<Attachment[]> GetAttachmentsAsync(CancellationToken cancel)
        {
            throw new System.NotImplementedException();
        }

        public ContentNode Parent { get; set; }
        public List<ContentNode> Children { get; } = new List<ContentNode>();

        public ContentNode Clone()
        {
            var contentNode = new ContentNode
            {
                Name = this.Name,
                Path = this.Path,
                Type = this.Type
            };
            foreach (var item in _fields)
                contentNode._fields.Add(item.Key, item.Value);
            return contentNode;
        }
    }
}
