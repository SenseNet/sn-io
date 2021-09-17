using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SenseNet.Client;

namespace SenseNet.IO.Implementations
{
    public class RepositoryReaderContent : IContent
    {
        private static readonly string[] FieldBlackList = new[]
        {
            "Actions", "Children", "EffectiveAllowedChildTypes",
            "CreatedById", "ModifiedById", "OwnerId", "ParentId", "SavingState",
            "InFolder", "InTree", "__permissions"
        };

        private readonly Dictionary<string, object> _fields;

        public string[] FieldNames { get; }
        public string Name { get; set; }
        public string Path { get; }
        public string Type { get; }
        public PermissionInfo Permissions { get; set; }

        public object this[string fieldName]
        {
            get => GetValue(fieldName);
            set => _fields[fieldName] = value;
        }

        public RepositoryReaderContent(Content content)
        {
            Name = ((JToken)content["ContentName"]).Value<string>();
            Type = ((JToken)content["ContentType"]).Value<string>();

            var fieldsObject = (JObject) content["Fields"];
            _fields = fieldsObject.ToObject<Dictionary<string, object>>() ?? new Dictionary<string, object>();
            FieldNames = _fields.Keys.Except(FieldBlackList).ToArray();

            Path = (string)_fields["Path"];

            var permissionsText = ((JObject)content["Permissions"])?.ToString();
            if (permissionsText != null)
                using (var reader = new JsonTextReader(new StringReader(permissionsText)))
                    Permissions = JsonSerializer.CreateDefault().Deserialize<PermissionInfo>(reader);

        }

        private object GetValue(string fieldName)
        {
            var value = _fields[fieldName];
            if (value == null)
                return null;

            if (value is JObject jObject)
                if (jObject["__mediaresource"] is JObject)
                    return new { Attachment = GetAttachmentName(fieldName) };

            return value;
        }

        public async Task<Attachment[]> GetAttachmentsAsync()
        {
            var result = new List<Attachment>();
            foreach (var fieldName in FieldNames)
            {
                var field = _fields[fieldName];
                if (field is JObject jObject)
                {
                    if (jObject["__mediaresource"] is JObject res)
                    {
                        var uri = res["media_src"]?.Value<string>();
                        if (uri != null)
                        {
                            var contentType = res["content_type"]?.Value<string>();
                            result.Add(new Attachment
                            {
                                FileName = GetAttachmentName(fieldName),
                                FieldName = fieldName,
                                ContentType = contentType,
                                Stream = await GetStream(uri)
                            });
                        }
                    }
                }
            }

            return result.ToArray();
        }

        private string GetAttachmentName(string fieldName)
        {
            var attachmentName = fieldName == "Binary"
                ? Type == "ContentType"
                    ? Name + ".xml"
                    : Name
                : Name + "." + fieldName;

            return attachmentName;
        }

        public async Task<Stream> GetStream(string url)
        {
            var server = ClientContext.Current.Server; //UNDONE:AUTH: do not use static servers
            var absoluteUrl = server.Url + url;
            Stream result = null;
            try
            {
                await RESTCaller.ProcessWebResponseAsync(absoluteUrl, HttpMethod.Get, ClientContext.Current.Server, async response =>
                {
                    result = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                }, CancellationToken.None);
            }
            catch (Exception e)
            {
                throw new SnException(0, "RepositoryReader: cannot get attachment stream.", e);
            }

            return result;
        }
    }
}