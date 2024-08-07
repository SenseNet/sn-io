﻿using System;
using System.Threading;
using System.Threading.Tasks;

namespace SenseNet.IO.Implementations
{
    /// <summary>
    /// Technical class for building initial structures.
    /// </summary>
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
        public bool CutOff { get; set; }
        public string Type { get; }
        public PermissionInfo Permissions { get; set; }
        public bool IsFolder => true; // always a folder
        public bool HasData => false; // never contains data

        public InitialContent(string path, string name, string type)
        {
            Path = path;
            Name = name;
            Type = type;
        }

        public Task<Attachment[]> GetAttachmentsAsync(CancellationToken cancel)
        {
            return Task.FromResult(Array.Empty<Attachment>());
        }
    }
}
