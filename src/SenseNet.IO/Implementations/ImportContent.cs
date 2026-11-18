using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace SenseNet.IO.Implementations
{
    // A request-only view: keep reader metadata and attachment filenames unchanged.
    internal class ImportContent : IContent
    {
        private readonly IContent _source;
        private readonly ContentPathMap _paths;
        private readonly HashSet<string> _referenceFields;

        public ImportContent(IContent source, string name, ContentPathMap paths,
            IEnumerable<string> referenceFields = null, bool remapPermissions = false, bool referenceUpdate = false)
        {
            _source = source;
            Name = name;
            _paths = paths;
            IsReferenceUpdate = referenceUpdate || source is ImportContent { IsReferenceUpdate: true };
            _referenceFields = new HashSet<string>(referenceFields ?? Array.Empty<string>(), StringComparer.Ordinal);
            Permissions = remapPermissions && source.Permissions != null
                ? new PermissionInfo
                {
                    IsInherited = source.Permissions.IsInherited,
                    Entries = source.Permissions.Entries?.Select(entry => new AceInfo
                    {
                        Identity = paths.Resolve(entry.Identity),
                        LocalOnly = entry.LocalOnly,
                        Permissions = entry.Permissions
                    }).ToArray()
                }
                : source.Permissions;
        }

        public string[] FieldNames => _source.FieldNames;
        public object this[string fieldName]
        {
            get => fieldName == "Name" ? Name : _referenceFields.Contains(fieldName)
                ? RemapReference(_source[fieldName]) : _source[fieldName];
            set => throw new NotSupportedException("Import request metadata is read-only.");
        }

        private object RemapReference(object value)
        {
            // The server identifies reference fields in brokenReferences. Only their scalar
            // path values (or lists of them) are remapped; IDs and structured objects stay intact.
            return value switch
            {
                string path => _paths.Resolve(path),
                JValue { Type: JTokenType.String } token => new JValue(_paths.Resolve(token.Value<string>())),
                JArray array => new JArray(array.Select(item => RemapReference(item))),
                IEnumerable<string> paths => paths.Select(_paths.Resolve).ToArray(),
                _ => value
            };
        }

        public string Name { get; set; }
        internal bool IsReferenceUpdate { get; }
        public string Path => _source.Path;
        public bool CutOff { get => _source.CutOff; set => _source.CutOff = value; }
        public string Type => _source.Type;
        public PermissionInfo Permissions { get; set; }
        public bool IsFolder => _source.IsFolder;
        public bool HasData => _source.HasData;
        public Task<Attachment[]> GetAttachmentsAsync(CancellationToken cancel) => _source.GetAttachmentsAsync(cancel);
    }
}
