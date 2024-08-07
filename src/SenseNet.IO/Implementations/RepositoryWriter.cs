﻿using System;
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
using SenseNet.Tools.Configuration;

namespace SenseNet.IO.Implementations
{
    [OptionsClass(sectionName: "repositoryWriter")]
    public class RepositoryWriterArgs
    {
        /// <summary>
        /// Repository url.
        /// </summary>
        public string Url { get; set; }
        /// <summary>
        /// Content path in the repository. Default: /
        /// </summary>
        public string Path { get; set; }
        /// <summary>
        /// Target name under the container. Default: name of the reader's root.
        /// </summary>
        public string Name { get; set; }
        public RepositoryAuthenticationOptions Authentication { get; set; } = new();
        /// <summary>
        /// Number of bytes sent to the server in one chunk during upload operations. Default: 10 MB
        /// </summary>
        public int UploadChunkSize { get; set; }
        /// <summary>
        /// True if only the creation is allowed and updates are omitted.
        /// </summary>
        public bool CreateOnly { get; set; }

        internal RepositoryWriterArgs Clone()
        {
            return new RepositoryWriterArgs
            {
                Url = Url,
                Path = Path,
                Name = Name,
                UploadChunkSize = UploadChunkSize,
                CreateOnly = CreateOnly,
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

            string skipReason;
            if(null != (skipReason = await ApplyFilters(repositoryPath, cancel)))
                return new WriterState
                {
                    Action = WriterAction.Skipped,
                    WriterPath = repositoryPath,
                    Messages = new[] { skipReason }
                };

            if (content.Type == "ContentType")
                return await WriteContentTypeAsync(repositoryPath, content, cancel);
            return await WriteContentAsync(repositoryPath, content, cancel);
        }
        private async Task<string> ApplyFilters(string repositoryPath, CancellationToken cancel)
        {
            if (Args.CreateOnly)
            {
                if(await _repository.IsContentExistsAsync(repositoryPath, cancel))
                    return "Existing content.";
            }
            return null;
        }

        public async Task<bool> ShouldSkipSubtreeAsync(string path, CancellationToken cancel = default)
        {
            //TODO: ? ContainerPath can be "/"? See TestRepositoryWriter.IsContentExists()
            if (!path.StartsWith("/"))
                path = ContentPath.Combine(ContainerPath, path);
            return !await _repository.IsContentExistsAsync(path, cancel).ConfigureAwait(false);
        }

        private async Task<WriterState> WriteContentTypeAsync(string repositoryPath, IContent content, CancellationToken cancel)
        {
            var attachments = await content.GetAttachmentsAsync(cancel);

            // "Binary" field of a ContentType need to uploaded first.
            var binary = attachments.FirstOrDefault(a => a.FieldName == "Binary");
            if (binary?.Stream != null)
            {
                await using var stream = binary.Stream;
                await _repository.UploadAsync(new UploadRequest
                {
                    ParentPath = "/Root/System/Schema/ContentTypes",
                    ContentName = content.Name,
                    ContentType = "ContentType"
                }, stream, cancel).ConfigureAwait(false);
            }
            else
            {
                // in case of content types, there is no point to continue if the binary is missing
                return new WriterState
                {
                    Action = WriterAction.Failed,
                    WriterPath = repositoryPath,
                    Messages = new[] { $"ContentType {content.Name} does not have a Binary attachment." }
                };
            }

            // Upload other binaries if there are.
            foreach (var attachment in attachments.Where(a => a.FieldName != "Binary" && a.Stream != null))
            {
                await using var stream = attachment.Stream;
                await _repository.UploadAsync(new UploadRequest
                {
                    ParentPath = "/Root/System/Schema/ContentTypes",
                    ContentName = content.Name,
                    ContentType = "ContentType",
                    PropertyName = attachment.FieldName
                }, stream, cancel).ConfigureAwait(false);
            }

            // Remove attachments from field set.
            var attachmentNames = attachments.Select(x => x.FieldName).ToArray();
            var fields = content.FieldNames
                .Where(fieldName => !attachmentNames.Contains(fieldName))
                .ToDictionary(fieldName => fieldName, fieldName => content[fieldName]);

            // Send "import" message
            var body = new
            {
                path = repositoryPath,
                data = new
                {
                    ContentType = content.Type,
                    ContentName = content.Name,
                    Fields = fields,
                    content.Permissions
                }
            };

            // {path, name, type, action, brokenReferences, retryPermissions, messages };
            string resultString;
            try
            {
                resultString = await _repository.InvokeActionAsync<string>(
                    new OperationRequest {Path= "/Root",OperationName = "Import", PostData = body}, cancel);
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

            var attachments = await content.GetAttachmentsAsync(cancel);

            // Remove attachments from field set.
            var attachmentNames = attachments.Select(x => x.FieldName).ToArray();
            var fields = content.FieldNames
                .Where(fieldName => !attachmentNames.Contains(fieldName))
                .ToDictionary(fieldName => fieldName, fieldName => content[fieldName]);

            // Send "import" message
            var body = new
            {
                path = repositoryPath,
                data = new
                {
                    ContentType = content.Type,
                    ContentName = content.Name,
                    Fields = fields,
                    content.Permissions
                }
            };

            // {path, name, type, action, brokenReferences, retryPermissions, messages };
            string resultString = null;
            try
            {
                await Retrier.RetryAsync(50, 3000, async () =>
                    {
                        resultString = await _repository.InvokeActionAsync<string>(
                            new OperationRequest {Path = "/Root", OperationName = "Import", PostData = body}, cancel);
                    },
                    (i, exception) => exception.CheckRetryConditionOrThrow(i), cancel);

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
            catch (Exception e)
            {
                _logger.LogError(e, $"Error during importing {repositoryPath}: {e.Message}.");
                return new WriterState
                {
                    WriterPath = repositoryPath,
                    Action = IsParentNotFoundException(repositoryPath, e) ? WriterAction.MissingParent : WriterAction.Failed,
                    Messages = new[] { e.Message }
                };
            }

            // Upload binaries if there are.
            var parentPath = ContentPath.GetParentPath(repositoryPath);
            foreach (var attachment in attachments)
            {
                if (attachment.Stream == null)
                {
                    _logger.LogWarning("Attachment {fileName} of field {attachmentName} for {repositoryPath} is null", 
                        attachment.FileName, attachment.FieldName, repositoryPath);
                    continue;
                }

                try
                {
                    await using var stream = attachment.Stream;
                    await _repository.UploadAsync(new UploadRequest
                    {
                        ParentPath = parentPath,
                        ContentName = content.Name,
                        PropertyName = attachment.FieldName
                    }, stream, cancel).ConfigureAwait(false);
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
                catch (Exception e)
                {
                    _logger.LogError(e, $"Error during {attachment.FieldName} attachment upload for {repositoryPath}: " +
                                        $"{e.Message}.");

                    return new WriterState
                    {
                        WriterPath = repositoryPath,
                        Action = WriterAction.Failed,
                        Messages = new[] { e.Message }
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
