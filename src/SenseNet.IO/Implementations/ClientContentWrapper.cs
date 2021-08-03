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
    public class ClientContentWrapper : IContent
    {
        private readonly Content _content;

        public ClientContentWrapper(Content content)
        {
            _content = content;
        }

        private static readonly string[] FieldBlackList = new[]
        {
            "Actions", "Children", "EffectiveAllowedChildTypes",
            "CreatedById", "ModifiedById", "OwnerId", "ParentId", "SavingState",
            "InFolder", "InTree", "__permissions"
        };
        private string[] _fieldNames;
        public string[] FieldNames => _fieldNames ?? (_fieldNames = _content.FieldNames.Except(FieldBlackList).ToArray());

        public object this[string fieldName]
        {
            get => GetValue(fieldName);
            set => _content[fieldName] = value;
        }

        private object GetValue(string fieldName)
        {
            var token = _content[fieldName] as JToken;
            if (token == null)
                return null;
            if (token.Type == JTokenType.Null)
                return null;
            if (token is JObject jObject)
            {
                if (jObject["__mediaresource"] is JObject res)
                    return new { Attachment = GetAttachmentName(fieldName) };
            }
            return token;
        }

        public string Name
        {
            get => _content.Name;
            set => _content.Name = value;
        }
        public string Path
        {
            get => _content.Path;
            set => _content.Path = value;
        }
        public string Type
        {
            get => ((JToken)_content["Type"]).Value<string>();
            set => _content["Type"] = value;
        }

        private bool _isPermissionInfoLoaded;
        private PermissionInfo _permissionInfo;
        public PermissionInfo Permissions
        {
            get
            {
                if (!_isPermissionInfoLoaded)
                {
                    var perms = this["__permissions"]?.ToString();
                    if (perms != null)
                    {
                        using(var reader = new JsonTextReader(new StringReader(perms)))
                            _permissionInfo = JsonSerializer.CreateDefault().Deserialize<PermissionInfo>(reader);
                    }

                    _isPermissionInfoLoaded = true;
                }

                return _permissionInfo;
            }
            set
            {
                _permissionInfo = value;
                _isPermissionInfoLoaded = true;
            }
        }

        public async Task<Attachment[]> GetAttachmentsAsync()
        {
            var result = new List<Attachment>();
            foreach (var fieldName in _content.FieldNames)
            {
                var field = _content[fieldName];
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
            var contentType = _content["Type"]?.ToString() ?? "";
            var attachmentName = fieldName == "Binary"
                ? contentType == "ContentType"
                    ? _content.Name + ".xml"
                    : _content.Name
                : _content.Name + "." + fieldName;

            return attachmentName;
        }

        public async Task<Stream> GetStream(string url)
        {
            var server = ClientContext.Current.Server; //UNDONE: do not use static servers
            var absoluteUrl = server.Url + url;
            Stream result = null;
            await RESTCaller.ProcessWebResponseAsync(absoluteUrl, HttpMethod.Get, ClientContext.Current.Server, async response =>
            {
                result = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            }, CancellationToken.None);

            return result;
        }
    }
}