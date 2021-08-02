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

        public object this[string fieldName]
        {
            get => _content[fieldName];
            set => _content[fieldName] = value;
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

        public T GetField<T>(string name)
        {
            var raw = this[name];
            if (raw is JValue jValue)
            {
                if (jValue.Value == null)
                    return default;
                return jValue.ToObject<T>();
            }

            //UNDONE: other types are not implemented
            return default;
        }

        private static readonly string[] FieldBlackList = new[]
        {
            "Actions", "Children", "EffectiveAllowedChildTypes",
            "CreatedById", "ModifiedById", "OwnerId", "ParentId", "SavingState",
            "InFolder", "InTree", "__permissions"
        };

        public string ToJson()
        {
            var fields = _content.FieldNames
                .Except(FieldBlackList)
                .Where(f => !IsNull(_content[f]))
                .ToDictionary(key => key, GetFieldValue);

            var model = new
            {
                ContentType = Type,
                ContentName = Name,
                Fields = fields,
                Permissions = Permissions
            };

            var writer = new StringWriter();
            JsonSerializer.Create(new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore
            }).Serialize(writer, model);

            return writer.GetStringBuilder().ToString();
        }

        private object GetFieldValue(string fieldName)
        {
            var fieldValue = _content[fieldName];
            if (fieldValue is JObject jObject)
            {
                if (jObject["__mediaresource"] is JObject res)
                    fieldValue = new {Attachment = GetAttachmentName(fieldName)};
            }
            return fieldValue;
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

        private bool IsNull(object value)
        {
            if (value == null)
                return true;
            if(value is JValue jValue)
                if (jValue.Value == null)
                    return true;
            return false;
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

        /*
        public async Task Download(string url)
        {
            string ctd = null;
            await RESTCaller.ProcessWebResponseAsync(url, HttpMethod.Get, ClientContext.Current.Server, async response =>
            {
                if (response == null)
                    return;
                using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var reader = new StreamReader(stream))
                    ctd = reader.ReadToEnd();
            }, CancellationToken.None);
        }
        */
    }
}