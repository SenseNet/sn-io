using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SenseNet.IO.Implementations
{
    [DebuggerDisplay("{Path}")]
    public class LegacyRepositoryContent : IContent
    {
        private static readonly HashSet<string> FieldBlackList = new(StringComparer.Ordinal)
        {
            "Actions",
            "AllFieldSettingContents",
            "AvailableContentTypeFields",
            "Children",
            "EffectiveAllowedChildTypes",
            "Icon",
            "InFolder",
            "InTree",
            "IsRateable",
            "NodeType",
            "Rate",
            "RateAvg",
            "RateCount",
            "RateStr",
            "SavingState",
            "TypeIs",
            "CreatedById",
            "ModifiedById",
            "OwnerId",
            "ParentId",
            "__metadata",
            "__permissions"
        };

        private readonly Dictionary<string, object> _fields;
        private readonly Func<string, CancellationToken, Task<Stream>> _streamLoader;

        public string[] FieldNames { get; }
        public string Name { get; set; }
        public string Path { get; }
        public bool CutOff { get; set; }
        public string Type { get; }
        public PermissionInfo Permissions { get; set; }

        public bool IsFolder => true;
        public bool HasData => _fields?.Any() ?? false;

        public object this[string fieldName]
        {
            get => GetValue(fieldName);
            set => _fields[fieldName] = value;
        }

        public LegacyRepositoryContent(JObject content, Func<string, CancellationToken, Task<Stream>> streamLoader)
        {
            _streamLoader = streamLoader ?? throw new ArgumentNullException(nameof(streamLoader));

            Name = content["ContentName"]?.Value<string>() ?? content["Name"]?.Value<string>();
            Type = content["ContentType"]?.Value<string>() ?? content["Type"]?.Value<string>();

            _fields = CreateFields(content);
            FieldNames = _fields.Keys.Where(IsExportedField).ToArray();

            Path = _fields.TryGetValue("Path", out var path) ? path?.ToString() : null;

            var permissionsText = (content["Permissions"] as JObject)?.ToString();
            if (permissionsText != null)
                using (var reader = new JsonTextReader(new StringReader(permissionsText)))
                    Permissions = JsonSerializer.CreateDefault().Deserialize<PermissionInfo>(reader);
        }

        private static Dictionary<string, object> CreateFields(JObject content)
        {
            if (content["Fields"] is JObject fieldsObject)
                return fieldsObject.ToObject<Dictionary<string, object>>() ?? new Dictionary<string, object>();

            var fields = content.ToObject<Dictionary<string, object>>() ?? new Dictionary<string, object>();
            fields.Remove("ContentType");
            fields.Remove("ContentName");
            fields.Remove("Permissions");
            return fields;
        }

        private static bool IsExportedField(string fieldName)
        {
            return !FieldBlackList.Contains(fieldName);
        }

        private object GetValue(string fieldName)
        {
            var value = _fields[fieldName];
            if (value == null)
                return null;

            if (value is JObject jObject && jObject["__mediaresource"] is JObject)
                return new { Attachment = GetAttachmentName(fieldName) };

            if (TryGetReferencePath(value, out var referencePath))
                return referencePath;

            if (TryGetReferencePaths(value, out var referencePaths))
                return referencePaths;

            if (value is JObject deferredObject && deferredObject["__deferred"] is JObject)
                return null;

            return value;
        }

        private static bool TryGetReferencePath(object value, out string path)
        {
            path = null;

            if (value is not JObject jObject)
                return false;

            path = jObject["Path"]?.Value<string>();
            return path != null;
        }

        private static bool TryGetReferencePaths(object value, out string[] paths)
        {
            paths = null;

            JArray array = value as JArray;
            if (array == null && value is JObject jObject)
                array = jObject["results"] as JArray;

            if (array == null || array.Count == 0)
                return false;

            var result = new List<string>();
            foreach (var item in array)
            {
                if (item is not JObject itemObject)
                    return false;

                var path = itemObject["Path"]?.Value<string>();
                if (path == null)
                    return false;

                result.Add(path);
            }

            paths = result.ToArray();
            return true;
        }

        public async Task<Attachment[]> GetAttachmentsAsync(CancellationToken cancel = default)
        {
            var result = new List<Attachment>();
            foreach (var fieldName in FieldNames)
            {
                if (_fields[fieldName] is not JObject jObject)
                    continue;
                if (jObject["__mediaresource"] is not JObject resource)
                    continue;

                var uri = resource["media_src"]?.Value<string>();
                if (uri == null)
                    continue;

                result.Add(new Attachment
                {
                    FileName = GetAttachmentName(fieldName),
                    FieldName = fieldName,
                    ContentType = resource["content_type"]?.Value<string>(),
                    Stream = await _streamLoader(uri, cancel).ConfigureAwait(false)
                });
            }

            return result.ToArray();
        }

        private string GetAttachmentName(string fieldName)
        {
            if (fieldName == "Binary")
                return Type == "ContentType" ? Name + ".xml" : Name;

            return Name + "." + fieldName;
        }
    }
}
