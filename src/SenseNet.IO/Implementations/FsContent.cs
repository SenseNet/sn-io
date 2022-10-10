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
        public string Path { get; set; }

        private string _type;
        public string Type
        {
            get
            {
                if (_type != null)
                    return _type;
                if (_metaFilePath == null)
                    return IsDirectory ? "Folder" : "File";
                return (string)this["Type"];
            }
            private set => _type = value;
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
                attachments.Add(new Attachment
                {
                    FieldName = attachmentItem.Key,
                    FileName = attachmentItem.Value,
                    Stream = IsFileExists(attachmentPath) ? CreateFileStream(attachmentPath, FileMode.Open) : null
                });
            }

            return Task.FromResult(attachments.ToArray());
        }

        /// <summary>
        /// Initializes a new <see cref="FsContent"/> instance.
        /// </summary>
        /// <param name="name">Name of the content.</param>
        /// <param name="metaFilePath">*.Content file or null.</param>
        /// <param name="isDirectory">True if represents a directory.</param>
        /// <param name="relativePath">Relative repository path. The reader's root content path need to be String.Empty.</param>
        /// <param name="defaultAttachmentPath">Path if the content is represented as a raw file (e.g. "readme.txt").</param>
        public FsContent(string name, string relativePath, string metaFilePath, bool isDirectory, string defaultAttachmentPath = null)
        {
            Name = name;
            IsDirectory = isDirectory;
            Path = relativePath;
            _metaFilePath = metaFilePath;
            _defaultAttachmentPath = defaultAttachmentPath;
        }

        private readonly string _metaFilePath;
        public bool IsDirectory { get; }

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

        public void InitializeMetadata(string[] fieldNames = null, bool? withoutPermissions = false)
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
            if (metaFile == null)
                throw new Exception("Cannot parse the metafile: " + _metaFilePath);
            _type = metaFile["ContentType"]?.Value<string>();
            if(_type == null)
                throw new Exception("Cannot parse the \"ContentType\" property: " + _metaFilePath);

            var name = metaFile["ContentName"]?.Value<string>();
            if (name != null)
                this.Name = name;

            var jFields = (JObject)metaFile["Fields"];
            if (jFields != null)
            {
                var fields = jFields.ToObject<Dictionary<string, object>>() ?? new Dictionary<string, object>();
                if (fieldNames != null)
                    foreach (var fieldName in fields.Keys.Except(fieldNames))
                        fields.Remove(fieldName);

                _fields = fields;
                _fieldNames = _fields?.Keys.ToArray();
            }
            else
            {
                _fields = new Dictionary<string, object>();
                _fieldNames = Array.Empty<string>();
            }


            if (withoutPermissions != true)
            {
                var permsObject = (JObject) metaFile["Permissions"];
                if(permsObject != null)
                    Permissions = permsObject.ToObject<PermissionInfo>();
            }
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
            // Open the file while allowing others to read it too. 
            return new FileStream(fsPath, fileMode, FileAccess.Read);
        }
    }
}
