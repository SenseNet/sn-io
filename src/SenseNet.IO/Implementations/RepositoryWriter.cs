using System;
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
    public class RepositoryWriter : IContentWriter
    {
        private readonly string _url;
        public string ContainerPath { get; }
        public string RootName { get; }

        public RepositoryWriter(string url, string containerPath = null, string rootName = null)
        {
            _url = url;
            ContainerPath = containerPath;
            RootName = rootName;
            Initialize();
        }

        private void Initialize()
        {
            ClientContext.Current.AddServer(new ServerContext
            {
                Url = _url,
                Username = "builtin\\admin",
                Password = "admin"
            });

        }

        public async Task<ImportResponse> WriteAsync(string path, IContent content, CancellationToken cancel = default)
        {
            var repositoryPath = ContentPath.Combine(ContainerPath ?? "/", path);
            if (content.Type == "ContentType")
                return await WriteContentTypeAsync(repositoryPath, content, cancel);
            return await WriteContentAsync(repositoryPath, content, cancel);
        }

        private async Task<ImportResponse> WriteContentTypeAsync(string repositoryPath, IContent content, CancellationToken cancel)
        {
            Content uploaded;
            var attachments = await content.GetAttachmentsAsync();

            // "Binary" field of a ContentType need to uploaded first.
            var binary = attachments.FirstOrDefault(a => a.FieldName == "Binary");
            if (binary != null)
            {
                using (var stream = binary.Stream)
                    uploaded = await Content.UploadAsync("/Root/System/Schema/ContentTypes", content.Name, stream, "ContentType");
            }

            // Upload other binaries if there are.
            foreach (var attachment in attachments)
            {
                if (attachment.FieldName != "Binary")
                {
                    using (var stream = attachment.Stream)
                        uploaded = await Content.UploadAsync("/Root/System/Schema/ContentTypes", content.Name, stream, "ContentType", attachment.FieldName);
                }
            }

            // Remove attachments from field set.
            var attachmentNames = attachments.Select(x => x.FieldName).ToArray();
            var fields = content.FieldNames
                .Where(fieldName => !attachmentNames.Contains(fieldName))
                .ToDictionary(fieldName => fieldName, fieldName => content[fieldName]);

            // Send "import" message
            var body = "models=" + JsonConvert.SerializeObject(new[]
                {
                    new
                    {
                        path = repositoryPath,
                        data = new
                        {
                            ContentType = content.Type,
                            ContentName = content.Name,
                            Fields = fields,
                            content.Permissions
                        }
                    }
                }
            );

            // {path, name, type, action, brokenReferences, retryPermissions, messages };
            string resultString = null;
            try
            {
                resultString = await RESTCaller.GetResponseStringAsync(
                    new ODataRequest {IsCollectionRequest = false, Path = "/Root", ActionName = "Import"}, HttpMethod.Post, body);
            }
            catch (Exception e)
            {
                return new ImportResponse
                {
                    WriterPath = repositoryPath,
                    Action = ImporterAction.Error,
                    Messages = new[] { e.Message }
                };
            }

            var result = JsonConvert.DeserializeObject(resultString) as JObject;
            if (result == null)
                return new ImportResponse{Action = ImporterAction.Unknown, WriterPath = repositoryPath};

            Enum.TryParse(typeof(ImporterAction), result["action"]?.Value<string>(), true, out var rawAction);
            var action = rawAction == null ? ImporterAction.Unknown : (ImporterAction)rawAction;
            var writerPath = result["path"]?.Value<string>();
            var retryPermissions = result["retryPermissions"]?.Value<bool>() ?? false;
            var brokenReferences = result["brokenReferences"]?.Values<string>().ToArray();
            var messages = result["messages"]?.Values<string>().ToArray();
            return new ImportResponse
            {
                WriterPath = writerPath,
                RetryPermissions = retryPermissions,
                BrokenReferences = brokenReferences,
                Messages = messages,
                Action = action
            };
        }
        private async Task<ImportResponse> WriteContentAsync(string repositoryPath, IContent content, CancellationToken cancel)
        {
            Content uploaded;
            var attachments = await content.GetAttachmentsAsync();

            // Remove attachments from field set.
            var attachmentNames = attachments.Select(x => x.FieldName).ToArray();
            var fields = content.FieldNames
                .Where(fieldName => !attachmentNames.Contains(fieldName))
                .ToDictionary(fieldName => fieldName, fieldName => content[fieldName]);

            // Send "import" message
            var body = "models=" + JsonConvert.SerializeObject(new[]
                {
                    new
                    {
                        path = repositoryPath,
                        data = new
                        {
                            ContentType = content.Type,
                            ContentName = content.Name,
                            Fields = fields,
                            content.Permissions
                        }
                    }
                }
            );

            // {path, name, type, action, brokenReferences, retryPermissions, messages };
            string resultString = null;
            try
            {
                resultString = await RESTCaller.GetResponseStringAsync(
                    new ODataRequest { IsCollectionRequest = false, Path = "/Root", ActionName = "Import" }, HttpMethod.Post, body);
            }
            catch (Exception e)
            {
                return new ImportResponse
                {
                    WriterPath = repositoryPath,
                    Action = ImporterAction.Error,
                    Messages = new[] {e.Message}
                };
            }

            // Upload binaries if there are.
            foreach (var attachment in attachments)
            {
                var parentPath = ContentPath.GetParentPath(repositoryPath);
                using (var stream = attachment.Stream)
                    uploaded = await Content.UploadAsync(parentPath, content.Name, stream, propertyName: attachment.FieldName);
            }

            // Process result
            var result = JsonConvert.DeserializeObject(resultString) as JObject;
            if (result == null)
                return new ImportResponse { Action = ImporterAction.Unknown, WriterPath = repositoryPath };

            Enum.TryParse(typeof(ImporterAction), result["action"]?.Value<string>(), true, out var rawAction);
            var action = rawAction == null ? ImporterAction.Unknown : (ImporterAction) rawAction;
            var writerPath = result["path"]?.Value<string>();
            var retryPermissions = result["retryPermissions"]?.Value<bool>() ?? false;
            var brokenReferences = result["brokenReferences"]?.Values<string>().ToArray();
            var messages = result["messages"]?.Values<string>().ToArray();
            return new ImportResponse
            {
                WriterPath = writerPath,
                RetryPermissions = retryPermissions,
                BrokenReferences = brokenReferences,
                Messages = messages,
                Action = action
            };
        }


        private async Task ___x()
        {
            var uploadRootPath = "/Root/UploadTests";
            var uploadFolder = await Content.LoadAsync(uploadRootPath).ConfigureAwait(false);
            if (uploadFolder == null)
            {
                uploadFolder = Content.CreateNew("/Root", "SystemFolder", "UploadTests");
                await uploadFolder.SaveAsync().ConfigureAwait(false);
            }

            Content uploaded;
            string fileText;

            // Upload a file
            using (var fileStream = new FileStream(@"D:\dev\Examples\MyFile.txt", FileMode.Open))
                uploaded = await Content.UploadAsync("/Root/UploadTests", "MyFile.txt", fileStream, "File", "Binary");


            // Update a CTD
            using (var reader = new StreamReader(@"D:\dev\Examples\DomainsCtd.xml"))
                fileText = reader.ReadToEnd();
            uploaded = await Content.UploadTextAsync("/Root/System/Schema/ContentTypes", "Domains",
                fileText, CancellationToken.None, "ContentType");

            using (var stream = new FileStream(@"D:\dev\Examples\DomainsCtd.xml", FileMode.Open))
                uploaded = await Content.UploadAsync("/Root/System/Schema/ContentTypes", "Domains", stream, "ContentType");

            // Update a Settings file
            uploaded = await Content.UploadTextAsync("/Root/System/Settings", "MyCustom.settings",
                "{Key:'Value'}", CancellationToken.None, "Settings");


        }
    }
}
