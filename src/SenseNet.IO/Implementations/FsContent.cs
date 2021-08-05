using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SenseNet.IO.Implementations
{
    public class FsContent : IContent
    {
        private JObject _rawContent;
        private string[] _fieldNames;
        private Dictionary<string, object> _fields;

        public string[] FieldNames => _fieldNames;

        public object this[string fieldName]
        {
            get => _fields[fieldName];
            set => _fields[fieldName] = value;
        }

        public string Name { get; set; }

        private string _path;
        public string Path
        {
            get
            {
                if (_path != null)
                    return _path;
                if (Parent == null)
                    return Name;
                var parentPath = Parent.Path;
                if (parentPath == "")
                    return Name;
                return Parent.Path + "/" + Name;
            }
            set => _path = value;
        }

        public string Type
        {
            get
            {
                if (_metaFilePath == null)
                    return IsDirectory ? "Folder" : "File";
                return (string)this["Type"];
            }
        }

        public PermissionInfo Permissions { get; set; }

        private readonly string _defaultAttachmentPath;
        public Task<Attachment[]> GetAttachmentsAsync()
        {
            if (_defaultAttachmentPath != null)
            {
                return Task.FromResult(new[]
                {
                    new Attachment
                    {
                        FieldName = "Binary",
                        FileName = System.IO.Path.GetFileName(_defaultAttachmentPath),
                        // ReSharper disable once AssignNullToNotNullAttribute
                        Stream = CreateFileStream(_defaultAttachmentPath, FileMode.Open)
                    }
                });
            }

            if (_attachmentNames == null)
                return Task.FromResult(new Attachment[0]);

            var directory = System.IO.Path.GetDirectoryName(_metaFilePath);
            var attachments = new List<Attachment>();
            foreach (var attachmentItem in _attachmentNames)
            {
                var attachmentPath = System.IO.Path.Combine(directory, attachmentItem.Value);
                if (IsFileExists(attachmentPath))
                {
                    attachments.Add(new Attachment
                    {
                        FieldName = attachmentItem.Key,
                        FileName = attachmentItem.Value,
                        Stream = CreateFileStream(attachmentPath, FileMode.Open)
                    });
                }
            }

            return Task.FromResult(attachments.ToArray());
        }

        /// <summary>
        /// Initializes a new <see cref="FsContent"/> instance.
        /// </summary>
        /// <param name="name">Name of the content.</param>
        /// <param name="metaFilePath">*.Content file or null.</param>
        /// <param name="isDirectory">True if represents a directory.</param>
        /// <param name="parent">Parent or null if the root content.</param>
        /// <param name="defaultAttachmentPath">Path if the content is represented as a raw file (e.g. "readme.txt").</param>
        public FsContent(string name, string metaFilePath, bool isDirectory, FsContent parent, string defaultAttachmentPath = null)
        {
            Name = name;
            IsDirectory = isDirectory;
            Parent = parent;
            if (parent != null)
                parent.Children.Add(this);
            else
                _path = ""; // set explicit empty path if root
            _metaFilePath = metaFilePath;
            _defaultAttachmentPath = defaultAttachmentPath;
        }

        private readonly string _metaFilePath;
        public bool IsDirectory { get; }
        public FsContent Parent { get; set; }
        public List<FsContent> Children { get; } = new List<FsContent>();

        private static readonly string[] EmptyAttachmentNames = new string[0];

        private Dictionary<string, string> _attachmentNames;
        public string[] GetPreprocessedAttachmentNames()
        {
            if (_metaFilePath == null)
                return EmptyAttachmentNames;

            var deserialized = JsonSerializer.CreateDefault()
                .Deserialize(new JsonTextReader(CreateStreamReader(_metaFilePath)));

            var names = new Dictionary<string, string>();
            var metaFile = (JObject) deserialized;
            var fields = (JObject) metaFile["Fields"];
            foreach (var field in fields)
            {
                var token = (JToken) field.Value;
                if (token is JObject fieldObject)
                {
                    var subToken = fieldObject["Attachment"];
                    if(subToken != null && subToken.Type == JTokenType.String)
                        names.Add(field.Key, subToken.Value<string>());
                }
            }

            _attachmentNames = names;
            return _attachmentNames.Values.ToArray();
        }

        public void InitializeMetadata()
        {
            if (_metaFilePath == null)
            {
                _fields = new Dictionary<string, object>();
                _fieldNames = new string[0];
                return;
            }

            var deserialized = JsonSerializer.CreateDefault()
                .Deserialize(new JsonTextReader(CreateStreamReader(_metaFilePath)));

            var metaFile = (JObject)deserialized;
            _fields = ((JObject)metaFile["Fields"]).ToObject<Dictionary<string, object>>();
            _fieldNames = _fields.Keys.ToArray();
            var permsObject = (JObject) metaFile["Permissions"];
            if(permsObject != null)
                Permissions = permsObject.ToObject<PermissionInfo>();
        }

        /* ========================================================================== TESTABILITY */

        protected virtual bool IsFileExists(string fsPath)
        {
            return File.Exists(fsPath);
        }
        protected virtual TextReader CreateStreamReader(string metaFilePath)
        {
            return new StreamReader(metaFilePath);
        }
        protected virtual Stream CreateFileStream(string fsPath, FileMode fileMode)
        {
            return new FileStream(fsPath, fileMode);
        }
    }
}
