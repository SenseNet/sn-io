using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SenseNet.Client;
using SenseNet.Tools;

namespace SenseNet.IO.Implementations
{
    public class RepositoryWriterArgs
    {
        public string Url { get; set; }
        public string Path { get; set; }
        public string Name { get; set; }
        public RepositoryAuthenticationOptions Authentication { get; set; } = new();
        public int UploadChunkSize { get; set; }

        internal RepositoryWriterArgs Clone()
        {
            return new RepositoryWriterArgs
            {
                Url = Url,
                Path = Path,
                Name = Name,
                UploadChunkSize = UploadChunkSize,
                Authentication = Authentication?.Clone() ?? new RepositoryAuthenticationOptions()
            };
        }
    }

    public class RepositoryWriter : ISnRepositoryWriter
    {
        private readonly IRepositoryCollection _repositoryCollection;
        private readonly ILogger<RepositoryWriter> _logger;
        private IRepository _repository;

        public RepositoryWriterArgs Args { get; }
        public string Url => Args.Url;
        public string ContainerPath => Args.Path ?? "/";
        public string RootName => Args.Name;

        public RepositoryWriterArgs WriterOptions => Args;

        public RepositoryWriter(IRepositoryCollection repositoryCollection, IOptions<RepositoryWriterArgs> args, 
            ILogger<RepositoryWriter> logger)
        {
            if (args?.Value == null)
                throw new ArgumentNullException(nameof(args));
            Args = args.Value.Clone();

            _repositoryCollection = repositoryCollection;
            _logger = logger;
        }

        private bool _initialized;
        public virtual async Task InitializeAsync()
        {
            if (_initialized)
                return;
            _initialized = true;

            if (string.IsNullOrEmpty(Url))
                throw new ArgumentException("RepositoryWriter: empty URL.");

            if (Args.UploadChunkSize > 0)
                ClientContext.Current.ChunkSizeInBytes = Args.UploadChunkSize;

            _repository = await _repositoryCollection.GetRepositoryAsync("target", CancellationToken.None)
                .ConfigureAwait(false);

            // workaround while the client api sets the logger internally
            if (_repository.Server is { Logger: null })
                _repository.Server.Logger = _logger;
        }

        public virtual async Task<WriterState> WriteAsync(string path, IContent content, CancellationToken cancel = default)
        {
            await InitializeAsync();

            var repositoryPath = ContentPath.Combine(ContainerPath, path);
            if (content.Type == "ContentType")
                return await WriteContentTypeAsync(repositoryPath, content, cancel);
            return await WriteContentAsync(repositoryPath, content, cancel);
        }

        public async Task<bool> ShouldSkipSubtree(string path, CancellationToken cancel = default)
        {
            //TODO: ? ContainerPath can be "/"? See TestRepositoryWriter.IsContentExists()
            if (!path.StartsWith("/"))
                path = ContentPath.Combine(ContainerPath, path);
            return !await _repository.IsContentExistsAsync(path, cancel).ConfigureAwait(false);
        }

        private async Task<WriterState> WriteContentTypeAsync(string repositoryPath, IContent content, CancellationToken cancel)
        {
            var attachments = await content.GetAttachmentsAsync();

            // "Binary" field of a ContentType need to uploaded first.
            var binary = attachments.FirstOrDefault(a => a.FieldName == "Binary");
            if (binary != null)
            {
                await using var stream = binary.Stream;
                await Content.UploadAsync("/Root/System/Schema/ContentTypes", content.Name, stream, "ContentType",
                    server: _repository.Server);
            }

            // Upload other binaries if there are.
            foreach (var attachment in attachments.Where(a => a.FieldName != "Binary"))
            {
                await using var stream = attachment.Stream;
                await Content.UploadAsync("/Root/System/Schema/ContentTypes", content.Name, stream, "ContentType",
                    attachment.FieldName, server: _repository.Server);
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
            string resultString;
            try
            {
                resultString = await RESTCaller.GetResponseStringAsync(
                    new ODataRequest(_repository.Server)
                        { IsCollectionRequest = false, Path = "/Root", ActionName = "Import" }, HttpMethod.Post, body,
                    _repository.Server);
            }
            catch (Exception e)
            {
                return new WriterState
                {
                    WriterPath = repositoryPath,
                    Action = WriterAction.Failed,
                    Messages = new[] { e.Message }
                };
            }

            if (JsonConvert.DeserializeObject(resultString) is not JObject result)
                return new WriterState{Action = WriterAction.Unknown, WriterPath = repositoryPath};

            Enum.TryParse(typeof(WriterAction), result["action"]?.Value<string>(), true, out var rawAction);
            var action = rawAction == null ? WriterAction.Unknown : (WriterAction)rawAction;
            var writerPath = result["path"]?.Value<string>();
            var retryPermissions = result["retryPermissions"]?.Value<bool>() ?? false;
            var brokenReferences = result["brokenReferences"]?.Values<string>().ToArray();
            var messages = result["messages"]?.Values<string>().ToArray();
            return new WriterState
            {
                WriterPath = writerPath,
                RetryPermissions = retryPermissions,
                BrokenReferences = brokenReferences,
                Messages = messages,
                Action = action
            };
        }
        private async Task<WriterState> WriteContentAsync(string repositoryPath, IContent content, CancellationToken cancel)
        {
            if (content.IsFolder && !content.HasData)
            {
                var existing = await _repository.IsContentExistsAsync(repositoryPath, cancel)
                    .ConfigureAwait(false);

                if (existing)
                {
                    _logger.LogTrace("Skip update of existing folder without metadata: {repositoryPath}", repositoryPath);

                    return new WriterState
                    {
                        WriterPath = repositoryPath,
                        Action = WriterAction.Skipped
                    };
                }
            }

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
                await Retrier.RetryAsync(50, 3000, async () =>
                    {
                        var request = new ODataRequest(_repository.Server)
                        {
                            IsCollectionRequest = false, Path = "/Root", ActionName = "Import"
                        };

                        resultString = await RESTCaller.GetResponseStringAsync(request, HttpMethod.Post, body, _repository.Server);
                    },
                    (i, exception) => exception.CheckRetryConditionOrThrow(i));

            }
            catch (ClientException e)
            {
                _logger.LogError(e, $"Error during importing {repositoryPath}: " +
                                    $"{e.Message}. {e.ErrorData?.ErrorCode} {e.StatusCode}");
                return new WriterState
                {
                    WriterPath = repositoryPath,
                    Action = IsParentNotFoundException(repositoryPath, e) ? WriterAction.MissingParent :  WriterAction.Failed,
                    Messages = new[] {e.Message}
                };
            }

            // Upload binaries if there are.
            var parentPath = ContentPath.GetParentPath(repositoryPath);
            foreach (var attachment in attachments.Where(a => a.Stream != null))
            {
                try
                {
                    await using var stream = attachment.Stream;
                    await Retrier.RetryAsync(50, 3000, async () =>
                        {
                            stream?.Seek(0, SeekOrigin.Begin);
                            await Content.UploadAsync(parentPath, content.Name, stream,
                                propertyName: attachment.FieldName, server: _repository.Server);
                        },
                        (i, exception) => exception.CheckRetryConditionOrThrow(i));
                }
                catch (ClientException ex)
                {
                    _logger.LogError(ex, $"Error during {attachment.FieldName} attachment upload for {repositoryPath}: " +
                                           $"{ex.Message}. {ex.ErrorData?.ErrorCode} {ex.StatusCode}");

                    return new WriterState
                    {
                        WriterPath = repositoryPath,
                        Action = WriterAction.Failed,
                        Messages = new[] { ex.Message }
                    };
                }
            }

            // Process result
            var result = JsonConvert.DeserializeObject(resultString) as JObject;
            if (result == null)
                return new WriterState { Action = WriterAction.Unknown, WriterPath = repositoryPath };

            var writerPath = result["path"]?.Value<string>();
            var retryPermissions = result["retryPermissions"]?.Value<bool>() ?? false;
            var brokenReferences = result["brokenReferences"]?.Values<string>().ToArray() ?? Array.Empty<string>();
            var messages = result["messages"]?.Values<string>().ToArray();
            var action = ParseWriterAction(result["action"]?.Value<string>(), retryPermissions || brokenReferences.Length > 0);
            return new WriterState
            {
                WriterPath = writerPath,
                RetryPermissions = retryPermissions,
                BrokenReferences = brokenReferences,
                Messages = messages,
                Action = action
            };
        }

        private bool IsParentNotFoundException(string path, Exception exception)
        {
            return exception.Message.Equals($"The server returned an error (HttpStatus: InternalServerError): " +
                                            $"Cannot create content {ContentPath.GetName(path)}, " +
                                            $"parent not found: {ContentPath.GetParentPath(path)}",
                StringComparison.OrdinalIgnoreCase);
        }

        private WriterAction ParseWriterAction(string src, bool needToUpdateReferences)
        {
            if(src==null)
                return WriterAction.Unknown;
            if (src.Equals("created", StringComparison.OrdinalIgnoreCase))
                return needToUpdateReferences ? WriterAction.Creating : WriterAction.Created;
            if (src.Equals("updated", StringComparison.OrdinalIgnoreCase))
                return needToUpdateReferences ? WriterAction.Updating : WriterAction.Updated;
            if (src.Equals("failed", StringComparison.OrdinalIgnoreCase))
                return WriterAction.Failed;
            return WriterAction.Unknown;
        }
    }
}
