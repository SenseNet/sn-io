using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace SenseNet.IO.Implementations
{
    /// <summary>
    /// Represents a content read from an old SenseNet 6 XML export format.
    /// </summary>
    public class FsXmlContent : IContent
    {
        private string[] _fieldNames;
        private Dictionary<string, object> _fields;

        public string[] FieldNames => _fieldNames ?? Array.Empty<string>();

        public object this[string fieldName]
        {
            get => _fields.TryGetValue(fieldName, out var value) ? value : null;
            set => _fields[fieldName] = value;
        }

        public string Name { get; set; }
        public string Path { get; set; }
        public bool CutOff { get; set; }

        private string _type;
        public string Type
        {
            get
            {
                if (_type != null)
                    return _type;
                return IsDirectory ? "Folder" : "File";
            }
        }

        public PermissionInfo Permissions { get; set; }
        public bool IsFolder => IsDirectory;
        public bool HasData => _metaFilePath != null;

        public bool IsDirectory { get; }

        private readonly string _metaFilePath;
        private readonly string _defaultAttachmentPath;
        private Dictionary<string, string> _attachmentNames;
        private readonly ILogger _logger;

        public FsXmlContent(string name, string relativePath, string metaFilePath, bool isDirectory, bool cutOff, ILogger logger, string defaultAttachmentPath = null)
        {
            Name = name;
            IsDirectory = isDirectory;
            Path = relativePath;
            _metaFilePath = metaFilePath;
            CutOff = cutOff;
            _logger = logger;
            _defaultAttachmentPath = defaultAttachmentPath;
        }

        public Task<Attachment[]> GetAttachmentsAsync(CancellationToken cancel)
        {
            if (_defaultAttachmentPath != null)
            {
                return Task.FromResult(new[]
                {
                    new Attachment
                    {
                        FieldName = "Binary",
                        FileName = System.IO.Path.GetFileName(_defaultAttachmentPath),
                        Stream = File.Exists(_defaultAttachmentPath) ? new FileStream(_defaultAttachmentPath, FileMode.Open, FileAccess.Read) : null
                    }
                });
            }

            if (_attachmentNames == null || _attachmentNames.Count == 0)
                return Task.FromResult(Array.Empty<Attachment>());

            var directory = System.IO.Path.GetDirectoryName(_metaFilePath);
            var attachments = new List<Attachment>();
            foreach (var attachmentItem in _attachmentNames)
            {
                var attachmentPath = System.IO.Path.Combine(directory!, attachmentItem.Value);
                // Binary field keeps the file extension, other fields (like ImageData) do not
                var fileName = attachmentItem.Key.Equals("Binary", StringComparison.OrdinalIgnoreCase)
                    ? attachmentItem.Value
                    : System.IO.Path.GetFileNameWithoutExtension(attachmentItem.Value);
                attachments.Add(new Attachment
                {
                    FieldName = attachmentItem.Key,
                    FileName = fileName,
                    Stream = File.Exists(attachmentPath) ? new FileStream(attachmentPath, FileMode.Open, FileAccess.Read) : null
                });
            }

            return Task.FromResult(attachments.ToArray());
        }

        /// <summary>
        /// Returns attachment file names referenced in the XML metadata file.
        /// </summary>
        public string[] GetPreprocessedAttachmentNames()
        {
            if (_metaFilePath == null)
                return Array.Empty<string>();

            try
            {
                var doc = XDocument.Load(_metaFilePath);
                var root = doc.Root;
                if (root == null)
                    return Array.Empty<string>();

                var names = new Dictionary<string, string>();
                var fieldsElement = root.Element("Fields");
                if (fieldsElement != null)
                {
                    foreach (var field in fieldsElement.Elements())
                    {
                        var attachment = field.Attribute("attachment")?.Value;
                        if (!string.IsNullOrEmpty(attachment))
                        {
                            names[field.Name.LocalName] = attachment;
                        }
                    }
                }

                _attachmentNames = names;
                return _attachmentNames.Values.ToArray();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Cannot preprocess attachments from XML: {Path}", _metaFilePath);
                _attachmentNames = new Dictionary<string, string>();
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Parses the XML metadata file and initializes fields, type, permissions.
        /// </summary>
        /// <param name="excludedFields">Optional list of field names to exclude from reading.</param>
        /// <param name="withoutPermissions">If true, permissions are not parsed.</param>
        public void InitializeMetadata(string[] excludedFields = null, bool? withoutPermissions = false)
        {
            if (_metaFilePath == null)
            {
                _fields = new Dictionary<string, object>();
                _fieldNames = Array.Empty<string>();
                return;
            }

            try
            {
                var doc = XDocument.Load(_metaFilePath);
                var root = doc.Root;
                if (root == null)
                    throw new Exception("Empty XML metafile: " + _metaFilePath);

                _type = root.Element("ContentType")?.Value;
                if (_type == null)
                    throw new Exception("Cannot parse the \"ContentType\" element: " + _metaFilePath);

                var contentName = root.Element("ContentName")?.Value;
                if (!string.IsNullOrEmpty(contentName))
                    Name = contentName;

                _fields = new Dictionary<string, object>();
                var fieldsElement = root.Element("Fields");
                if (fieldsElement != null)
                {
                    foreach (var field in fieldsElement.Elements())
                    {
                        var fieldName = field.Name.LocalName;

                        // Skip excluded fields
                        if (excludedFields != null && excludedFields.Contains(fieldName, StringComparer.OrdinalIgnoreCase))
                            continue;

                        // Handle attachment-only fields (they have no text value, only an attachment attribute)
                        var attachment = field.Attribute("attachment")?.Value;
                        if (!string.IsNullOrEmpty(attachment) && string.IsNullOrEmpty(field.Value))
                        {
                            // Store attachment-only field as object with Attachment property
                            // Binary field keeps the file extension, other fields (like ImageData) do not
                            var attachmentName = fieldName.Equals("Binary", StringComparison.OrdinalIgnoreCase)
                                ? attachment
                                : System.IO.Path.GetFileNameWithoutExtension(attachment);
                            _fields[fieldName] = new { Attachment = attachmentName };
                            continue;
                        }

                        // If field has child elements, store as a complex structure
                        if (field.HasElements)
                        {
                            // For multi-value fields like References, store as string array
                            var values = field.Elements().Select(e => e.Value).ToArray();
                            if (values.Length > 0)
                                _fields[fieldName] = values;
                        }
                        else
                        {
                            var value = field.Value;
                            if (!string.IsNullOrEmpty(value))
                            {
                                // Convert space-separated values to arrays for specific fields
                                if (ShouldConvertToArray(fieldName))
                                {
                                    _fields[fieldName] = value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                }
                                else
                                {
                                    _fields[fieldName] = value;
                                }
                            }
                        }
                    }
                }

                _fieldNames = _fields.Keys.ToArray();

                if (withoutPermissions != true)
                {
                    var permsElement = root.Element("Permissions");
                    if (permsElement != null)
                    {
                        var parsed = ParsePermissions(permsElement);
                        if (parsed.Entries.Length > 0)
                            Permissions = parsed;
                    }
                }
            }
            catch (Exception ex) when (ex.Message.StartsWith("Cannot parse") || ex.Message.StartsWith("Empty XML"))
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new Exception("Cannot parse the XML metafile: " + _metaFilePath, ex);
            }
        }

        /// <summary>
        /// Determines if a field should be converted from space-separated string to an array.
        /// </summary>
        private static bool ShouldConvertToArray(string fieldName)
        {
            // Fields that are stored as space-separated strings in SenseNet 6 XML
            // but should be arrays in modern JSON format
            return fieldName.Equals("AllowedChildTypes", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Parses the XML Permissions element into a PermissionInfo object.
        /// </summary>
        private static PermissionInfo ParsePermissions(XElement permsElement)
        {
            // <Clear /> means permissions are not inherited (IsInherited = false)
            var isInherited = permsElement.Element("Clear") == null && permsElement.Element("Break") == null;

            var entries = new List<AceInfo>();
            foreach (var identity in permsElement.Elements("Identity"))
            {
                var path = identity.Attribute("path")?.Value;
                if (string.IsNullOrEmpty(path))
                    continue;

                var propagation = identity.Attribute("propagation")?.Value;
                var localOnly = string.Equals(propagation, "LocalOnly", StringComparison.OrdinalIgnoreCase);

                var permissions = new Dictionary<string, string>();
                foreach (var perm in identity.Elements())
                {
                    var permValue = perm.Value?.Trim();
                    if (string.Equals(permValue, "Allowed", StringComparison.OrdinalIgnoreCase))
                        permissions[perm.Name.LocalName] = "allow";
                    else if (string.Equals(permValue, "Denied", StringComparison.OrdinalIgnoreCase))
                        permissions[perm.Name.LocalName] = "deny";
                }

                if (permissions.Count > 0)
                    entries.Add(new AceInfo { Identity = path, LocalOnly = localOnly, Permissions = permissions });
            }

            return new PermissionInfo
            {
                IsInherited = isInherited,
                Entries = entries.ToArray()
            };
        }
    }
}
