using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SenseNet.IO.Tests.Implementations
{
    /// <summary>
    /// A deliberately synthetic, loopback-only HTTP contract fixture. This is not a
    /// sensenet backend and does not implement or verify its naming algorithm.
    /// Explicit aliases make returned names unpredictable to the IO client.
    /// </summary>
    internal sealed class SyntheticSenseNetEndpoint : IAsyncDisposable
    {
        internal sealed class ImportRequest
        {
            public string Path { get; init; }
            public string ActualPath { get; init; }
            public JObject Data { get; init; }
            public string Action { get; init; }
        }

        internal sealed class UploadRequest
        {
            public string ParentPath { get; init; }
            public string ContentName { get; init; }
            public string ContentType { get; init; }
            public string PropertyName { get; init; }
            public byte[] Bytes { get; set; }
        }

        private readonly HttpListener _listener = new();
        private readonly Task _serverTask;
        private readonly Dictionary<string, string> _aliases;
        private readonly Dictionary<string, UploadRequest> _pendingUploads = new();
        private readonly Dictionary<string, JObject> _nodes = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<ImportRequest> _imports = new();
        private readonly List<UploadRequest> _uploads = new();
        private readonly List<string> _existenceChecks = new();
        private readonly List<string> _rejected = new();
        private readonly List<Exception> _errors = new();
        private readonly List<string> _operations = new();

        public string Url { get; }
        public string[] ReferenceFields { get; set; } = Array.Empty<string>();
        public bool IncludeResponsePath { get; set; } = true;
        public string RejectUploadProperty { get; set; }
        // Opt-in diagnostic contract: a composite Image object is accepted at creation,
        // but rejected on update. This models the reported asymmetry, not a real schema.
        public bool RejectCompositeImageOnUpdate { get; set; }
        public ImportRequest[] Imports => _imports.ToArray();
        public UploadRequest[] Uploads => _uploads.ToArray();
        public string[] ExistenceChecks => _existenceChecks.ToArray();
        public string[] RejectedRequests => _rejected.ToArray();
        public Exception[] Errors => _errors.ToArray();
        public string[] Operations => _operations.ToArray();
        public string[] Paths => _nodes.Keys.OrderBy(x => x, StringComparer.Ordinal).ToArray();
        public JObject GetNode(string path) => (JObject)_nodes[path].DeepClone();

        public SyntheticSenseNetEndpoint(IDictionary<string, string> aliases = null)
        {
            _aliases = new Dictionary<string, string>(aliases ?? new Dictionary<string, string>(),
                StringComparer.Ordinal);
            // Reserve an ephemeral loopback port, then immediately bind the HTTP listener.
            // If another process wins the small release/bind race, retry with a new port.
            for (var attempt = 0; ; attempt++)
            {
                using var reservation = new TcpListener(IPAddress.Loopback, 0);
                reservation.Start();
                var port = ((IPEndPoint)reservation.LocalEndpoint).Port;
                reservation.Stop();
                Url = $"http://127.0.0.1:{port}";
                _listener.Prefixes.Clear();
                _listener.Prefixes.Add(Url + "/");
                try
                {
                    _listener.Start();
                    break;
                }
                catch (HttpListenerException) when (attempt < 4) { }
            }

            Seed("/Root");
            Seed("/Root/Content");
            _serverTask = ServeAsync();
        }

        public void Seed(string path)
        {
            _nodes[path] = new JObject
            {
                ["Id"] = _nodes.Count + 1,
                ["Path"] = path,
                ["Name"] = ContentPath.GetName(path),
                ["Type"] = "Folder"
            };
        }

        private async Task ServeAsync()
        {
            while (_listener.IsListening)
            {
                HttpListenerContext context;
                try { context = await _listener.GetContextAsync().ConfigureAwait(false); }
                catch (HttpListenerException) when (!_listener.IsListening) { break; }
                catch (ObjectDisposedException) { break; }

                try { await HandleAsync(context).ConfigureAwait(false); }
                catch (Exception error)
                {
                    _errors.Add(error);
                    try { await ReplyAsync(context, 500, new { error = error.Message }).ConfigureAwait(false); }
                    catch (Exception) { /* The failed test may already have cancelled the client. */ }
                }
                finally { context.Response.Close(); }
            }
        }

        private async Task HandleAsync(HttpListenerContext context)
        {
            var request = context.Request;
            var uriPath = Uri.UnescapeDataString(request.Url.AbsolutePath);
            using var input = new MemoryStream();
            await request.InputStream.CopyToAsync(input).ConfigureAwait(false);
            var bytes = input.ToArray();
            var text = Encoding.UTF8.GetString(bytes);

            if (request.HttpMethod == "POST" && uriPath.EndsWith("/Import", StringComparison.Ordinal))
            {
                await ImportAsync(context, JObject.Parse(text)).ConfigureAwait(false);
                return;
            }

            if (request.HttpMethod == "POST" && uriPath.EndsWith("/Upload", StringComparison.Ordinal))
            {
                var parent = DecodeContentPath(uriPath.Substring(0, uriPath.Length - "/Upload".Length));
                if (request.QueryString["create"] == "1")
                {
                    var values = text.Split('&').Select(x => x.Split('=', 2))
                        .ToDictionary(x => WebUtility.UrlDecode(x[0]),
                            x => x.Length == 1 ? "" : WebUtility.UrlDecode(x[1]));
                    var upload = new UploadRequest
                    {
                        ParentPath = parent,
                        ContentName = values["FileName"],
                        ContentType = values["ContentType"],
                        PropertyName = values["PropertyName"]
                    };
                    if (upload.PropertyName == RejectUploadProperty)
                    {
                        await RejectAsync(context, "Synthetic attachment rejection.").ConfigureAwait(false);
                        return;
                    }
                    var createsContentType = upload.ContentType == "ContentType" && upload.PropertyName == "Binary";
                    if (!_nodes.ContainsKey(parent) ||
                        (!createsContentType && !_nodes.ContainsKey(ContentPath.Combine(parent, upload.ContentName))))
                    {
                        await RejectAsync(context, $"Upload target does not exist: {parent}/{upload.ContentName}")
                            .ConfigureAwait(false);
                        return;
                    }
                    var token = Guid.NewGuid().ToString("N");
                    _pendingUploads.Add(token, upload);
                    await ReplyTextAsync(context, 200, token).ConfigureAwait(false);
                    return;
                }

                var tokenMatch = Regex.Match(text, "name=\"?ChunkToken\"?\\r\\n\\r\\n([^\\r\\n]+)");
                if (!tokenMatch.Success || !_pendingUploads.TryGetValue(tokenMatch.Groups[1].Value, out var pending)
                    || parent != pending.ParentPath)
                {
                    await RejectAsync(context, "Invalid upload chunk token or parent.").ConfigureAwait(false);
                    return;
                }
                var fileHeader = text.IndexOf("name=\"files[]\"", StringComparison.Ordinal);
                if (fileHeader < 0)
                    fileHeader = text.IndexOf("name=files[]", StringComparison.Ordinal);
                var payloadStart = text.IndexOf("\r\n\r\n", fileHeader, StringComparison.Ordinal) + 4;
                var payloadEnd = text.IndexOf("\r\n--", payloadStart, StringComparison.Ordinal);
                if (fileHeader < 0 || payloadStart < 4 || payloadEnd < payloadStart)
                    throw new InvalidDataException("Missing multipart file payload.");
                // The fixture payloads are UTF-8, so this extraction also checks their contents.
                pending.Bytes = Encoding.UTF8.GetBytes(text.Substring(payloadStart, payloadEnd - payloadStart));
                _uploads.Add(pending);
                _operations.Add("Upload:" + pending.PropertyName);
                _pendingUploads.Remove(tokenMatch.Groups[1].Value);
                var returnedName = pending.ContentName;
                if (pending.ContentType == "ContentType" && pending.PropertyName == "Binary")
                {
                    if (_aliases.TryGetValue(pending.ContentName, out var alias))
                        returnedName = alias;
                    var createdPath = ContentPath.Combine(parent, returnedName);
                    if (!_nodes.ContainsKey(createdPath))
                        Seed(createdPath);
                    _nodes[createdPath]["Type"] = "ContentType";
                }
                await ReplyAsync(context, 200, new
                {
                    Url = ContentPath.Combine(parent, returnedName),
                    Name = returnedName,
                    Length = pending.Bytes.Length,
                    Type = pending.ContentType,
                    Id = 100
                }).ConfigureAwait(false);
                return;
            }

            if (request.HttpMethod == "GET" && uriPath.StartsWith("/OData.svc/", StringComparison.Ordinal))
            {
                var path = DecodeContentPath(uriPath);
                _existenceChecks.Add(path);
                if (_nodes.TryGetValue(path, out var node))
                    await ReplyAsync(context, 200, new JObject { ["d"] = node.DeepClone() }).ConfigureAwait(false);
                else
                    await ReplyTextAsync(context, 404, "")
                        .ConfigureAwait(false);
                return;
            }

            await RejectAsync(context, $"Unexpected HTTP request: {request.HttpMethod} {uriPath}")
                .ConfigureAwait(false);
        }

        private async Task ImportAsync(HttpListenerContext context, JObject request)
        {
            var path = request.Value<string>("path");
            var parent = ContentPath.GetParentPath(path);
            if (!_nodes.ContainsKey(parent))
            {
                await RejectAsync(context, $"Import parent does not exist: {parent}").ConfigureAwait(false);
                return;
            }

            var data = (JObject)request["data"];
            var name = data.Value<string>("ContentName");
            var actualName = _aliases.TryGetValue(name, out var alias) ? alias : name;
            var actualPath = ContentPath.Combine(parent, actualName);
            var existing = _nodes.ContainsKey(actualPath);
            var fields = (JObject)data["Fields"];
            if (RejectCompositeImageOnUpdate && existing && fields?["Image"] is JObject)
            {
                await RejectAsync(context, "Synthetic schema rejects composite Image on update.").ConfigureAwait(false);
                return;
            }
            var brokenReferences = ReferenceFields.Where(field => fields?[field] != null &&
                GetReferencePaths(fields[field]).Any(value => !_nodes.ContainsKey(value))).ToArray();
            var identities = (data["Permissions"] as JObject)?["Entries"]?.Select(x => x.Value<string>("Identity"))
                ?? Enumerable.Empty<string>();
            var retryPermissions = identities.Any(value => !_nodes.ContainsKey(value));
            var storedFields = existing && _nodes[actualPath]["Fields"] is JObject previousFields
                ? (JObject)previousFields.DeepClone() : new JObject();
            if (fields != null)
                foreach (var field in fields.Properties())
                    storedFields[field.Name] = field.Value.DeepClone();
            _nodes[actualPath] = new JObject
            {
                ["Id"] = existing ? _nodes[actualPath]["Id"] : _nodes.Count + 1,
                ["Path"] = actualPath,
                ["Name"] = actualName,
                ["Type"] = data["ContentType"].DeepClone(),
                ["Fields"] = storedFields,
                ["Permissions"] = data["Permissions"]?.DeepClone()
            };
            _imports.Add(new ImportRequest
            {
                Path = path,
                ActualPath = actualPath,
                Data = (JObject)data.DeepClone(),
                Action = existing ? "updated" : "created"
            });
            _operations.Add("Import");
            await ReplyAsync(context, 200, new
            {
                path = IncludeResponsePath ? actualPath : null,
                name = actualName,
                type = data.Value<string>("ContentType"),
                action = existing ? "updated" : "created",
                brokenReferences,
                retryPermissions,
                messages = Array.Empty<string>()
            }).ConfigureAwait(false);
        }

        private static IEnumerable<string> GetReferencePaths(JToken value) => value.Type == JTokenType.Array
            ? value.Values<string>() : new[] { value.Value<string>() };

        private static string DecodeContentPath(string uriPath)
        {
            var odata = uriPath.Substring("/OData.svc".Length);
            return Regex.Replace(odata, @"\('([^']*)'\)$", "/$1").Replace("//", "/");
        }

        private Task RejectAsync(HttpListenerContext context, string message)
        {
            _rejected.Add(message);
            return ReplyAsync(context, 400, new { error = new { message } });
        }

        private static Task ReplyAsync(HttpListenerContext context, int statusCode, object value) =>
            ReplyTextAsync(context, statusCode, JsonConvert.SerializeObject(value));

        private static async Task ReplyTextAsync(HttpListenerContext context, int statusCode, string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            _listener.Close();
            await _serverTask.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
    }
}
