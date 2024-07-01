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
using SenseNet.Client;

namespace SenseNet.IO.Implementations
{
    [DebuggerDisplay("{Path}")]
    public class RepositoryReaderContent : IContent
    {
        private static readonly string[] FieldBlackList = new[]
        {
            "Actions", "Children", "EffectiveAllowedChildTypes",
            "CreatedById", "ModifiedById", "OwnerId", "ParentId", "SavingState",
            "InFolder", "InTree", "__permissions"
        };

        private readonly Dictionary<string, object> _fields;
        private readonly ServerContext _server;
        private readonly IRepository _repository;

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

        public RepositoryReaderContent(Content content)
        {
            _server = content.Server;
            _repository = content.Repository;

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

        public async Task<Attachment[]> GetAttachmentsAsync(CancellationToken cancel = default)
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
                                Stream = await GetStream(uri, cancel)
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

        public async Task<Stream> GetStream(string url, CancellationToken cancel)
        {
//var absoluteUrl = _server.Url + url;
            MemoryStream result = null;
            try
            {
                //TODO: return a stream that can handle huge files by downloading segments
                // later, instead of storing the whole file in memory.
//await RESTCaller.ProcessWebResponseAsync(absoluteUrl, HttpMethod.Get, _server, response =>
//{
//    result = new MemoryStream();
//    response.Content.ReadAsStream().CopyTo(result);
//    result.Seek(0, SeekOrigin.Begin);
//}, CancellationToken.None);
                await _repository.ProcessWebResponseAsync(url, HttpMethod.Get, null, null, 
                    async (response, cancel) =>
                {
                    result = new MemoryStream();
                    var stream = await response.Content.ReadAsStreamAsync(cancel);
                    await stream.CopyToAsync(result, cancel);
                    result.Seek(0, SeekOrigin.Begin);
                }, cancel);
            }
            catch (Exception e)
            {
                throw new SnException(0, "RepositoryReader: cannot get attachment stream.", e);
            }

            return result;
        }
    }
}