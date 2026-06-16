using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace SenseNet.IO.Implementations
{
    /// <summary>
    /// Reads an old ASP.NET-hosted sensenet repository through the legacy OData API.
    /// This reader deliberately bypasses the modern SenseNet.Client authentication pipeline.
    /// </summary>
    public class LegacyRepositoryReader : ISnRepositoryReader, IDisposable
    {
        public RepositoryReaderArgs Args { get; }
        public RepositoryReaderArgs ReaderOptions => Args;

        private readonly ILogger<LegacyRepositoryReader> _logger;
        private readonly HttpClient _httpClient;
        private readonly CookieContainer _cookies;
        private readonly bool _disposeHandler;
        private int _blockSize;
        private int _blockIndex;

        public string Url => Args.Url;
        public string RootName => ContentPath.GetName(Args.Path);
        public string RepositoryRootPath => Args.Path;
        public string Filter { get; private set; }
        public int EstimatedCount { get; private set; }
        public IContent Content { get; private set; }
        public string RelativePath { get; private set; }

        public LegacyRepositoryReader(IOptions<RepositoryReaderArgs> args, ILogger<LegacyRepositoryReader> logger)
            : this(args, logger, null)
        {
        }

        protected LegacyRepositoryReader(IOptions<RepositoryReaderArgs> args, ILogger<LegacyRepositoryReader> logger,
            HttpMessageHandler messageHandler)
        {
            if (args?.Value == null)
                throw new ArgumentNullException(nameof(args));

            Args = args.Value.Clone();
            Args.Path ??= "/Root";
            Args.BlockSize ??= 10;

            _blockSize = Args.BlockSize.Value;
            _logger = logger;

            if (messageHandler == null)
            {
                _cookies = new CookieContainer();
                messageHandler = new HttpClientHandler
                {
                    CookieContainer = _cookies,
                    UseCookies = true
                };
                _disposeHandler = true;
            }

            _httpClient = new HttpClient(messageHandler, _disposeHandler);
        }

        private bool _initialized;
        public virtual async Task InitializeAsync()
        {
            if (_initialized)
                return;
            _initialized = true;

            if (string.IsNullOrEmpty(Url))
                throw new ArgumentException("LegacyRepositoryReader: empty URL.");

            Filter = InitializeFilter(Args.Filter);
            _blockSize = Args.BlockSize ?? 10;

            await Task.CompletedTask.ConfigureAwait(false);
        }

        private bool _connectionInitialized;
        private async Task InitializeConnectionAsync(CancellationToken cancel)
        {
            await InitializeAsync().ConfigureAwait(false);

            if (_connectionInitialized)
                return;

            _connectionInitialized = true;

            await AuthenticateAsync(cancel).ConfigureAwait(false);
            EstimatedCount = await GetCountAsync(cancel).ConfigureAwait(false);
        }

        private async Task AuthenticateAsync(CancellationToken cancel)
        {
            var auth = Args.Authentication;
            if (auth == null)
                return;

            AddConfiguredHeader(auth);
            AddConfiguredCookie(auth);
            AddBasicAuthentication(auth);

            if (!UsesFormsLogin(auth))
                return;

            var loginPath = string.IsNullOrWhiteSpace(auth.LoginPath) ? "/sn-login" : auth.LoginPath;
            var form = new Dictionary<string, string>
            {
                [string.IsNullOrWhiteSpace(auth.LoginUserNameField) ? "UserName" : auth.LoginUserNameField] = auth.Username,
                [string.IsNullOrWhiteSpace(auth.LoginPasswordField) ? "Password" : auth.LoginPasswordField] = auth.Password
            };

            if (!string.IsNullOrEmpty(auth.Domain))
                form[string.IsNullOrWhiteSpace(auth.LoginDomainField) ? "Domain" : auth.LoginDomainField] = auth.Domain;

            var loginUri = BuildUri(loginPath);
            var response = await _httpClient.PostAsync(loginUri, new FormUrlEncodedContent(form), cancel)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var body = await ReadErrorBodyAsync(response, cancel).ConfigureAwait(false);
                throw new InvalidOperationException(
                    $"LegacyRepositoryReader: login POST failed: {(int)response.StatusCode} {response.ReasonPhrase}. Url: {loginUri}.{body}");
            }
        }

        private void AddConfiguredHeader(RepositoryAuthenticationOptions auth)
        {
            if (string.IsNullOrWhiteSpace(auth.HeaderName) || auth.HeaderValue == null)
                return;

            _httpClient.DefaultRequestHeaders.Remove(auth.HeaderName);
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(auth.HeaderName, auth.HeaderValue);
        }

        private void AddConfiguredCookie(RepositoryAuthenticationOptions auth)
        {
            if (string.IsNullOrWhiteSpace(auth.Cookie))
                return;

            if (_cookies == null)
            {
                _httpClient.DefaultRequestHeaders.Remove("Cookie");
                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", auth.Cookie);
                return;
            }

            foreach (var cookieText in auth.Cookie.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = cookieText.Split('=', 2);
                if (parts.Length == 2)
                    _cookies.Add(BuildUri("/"), new Cookie(parts[0].Trim(), parts[1].Trim()));
            }
        }

        private void AddBasicAuthentication(RepositoryAuthenticationOptions auth)
        {
            if (!string.Equals(auth.Type, "basic", StringComparison.OrdinalIgnoreCase))
                return;
            if (string.IsNullOrEmpty(auth.Username) || auth.Password == null)
                return;

            var userName = string.IsNullOrEmpty(auth.Domain) ? auth.Username : auth.Domain + "\\" + auth.Username;
            var bytes = System.Text.Encoding.UTF8.GetBytes(userName + ":" + auth.Password);
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", Convert.ToBase64String(bytes));
        }

        private static bool UsesFormsLogin(RepositoryAuthenticationOptions auth)
        {
            if (string.IsNullOrEmpty(auth.Username) || auth.Password == null)
                return false;

            return string.IsNullOrEmpty(auth.Type) ||
                   auth.Type.Equals("legacy", StringComparison.OrdinalIgnoreCase) ||
                   auth.Type.Equals("legacyforms", StringComparison.OrdinalIgnoreCase) ||
                   auth.Type.Equals("forms", StringComparison.OrdinalIgnoreCase);
        }

        private class TreeState
        {
            public string AbsolutePath;
            public IContent[] CurrentBlock;
            public int BlockIndex;
            public int CurrentBlockIndex;
        }

        private readonly Dictionary<string, TreeState> _treeStates = new();
        public async Task<bool> ReadSubTreeAsync(string relativePath, CancellationToken cancel = default)
        {
            await InitializeConnectionAsync(cancel).ConfigureAwait(false);

            if (!_treeStates.TryGetValue(relativePath, out var treeState))
            {
                treeState = new TreeState
                {
                    AbsolutePath = ContentPath.Combine(RepositoryRootPath, relativePath),
                };
                _treeStates.Add(relativePath, treeState);
            }

            do
            {
                if (treeState.CurrentBlock == null || treeState.CurrentBlockIndex >= treeState.CurrentBlock.Length)
                {
                    treeState.CurrentBlock = await QueryBlockAsync(treeState.AbsolutePath, Array.Empty<string>(),
                        treeState.CurrentBlock?[^1].Path, _blockSize, treeState.BlockIndex * _blockSize, cancel)
                        .ConfigureAwait(false);
                    treeState.BlockIndex++;
                    treeState.CurrentBlockIndex = 0;
                    if (treeState.CurrentBlock == null || treeState.CurrentBlock.Length == 0)
                        return false;
                }

                Content = treeState.CurrentBlock[treeState.CurrentBlockIndex++];
                RelativePath = ContentPath.GetRelativePath(Content.Path, RepositoryRootPath);
            } while (SkipContent(Content));

            return true;
        }

        private IContent[] _currentBlock;
        private int _currentBlockIndex;
        public async Task<bool> ReadAllAsync(string[] contentsWithoutChildren, CancellationToken cancel = default)
        {
            await InitializeConnectionAsync(cancel).ConfigureAwait(false);

            do
            {
                if (_cutoffChanged)
                {
                    _currentBlock = await QueryBlockAsync(RepositoryRootPath, contentsWithoutChildren,
                        _currentBlock?[^1].Path, _blockSize, (_blockIndex - 1) * _blockSize, cancel)
                        .ConfigureAwait(false);
                    _currentBlockIndex--;
                    _cutoffChanged = false;
                }
                else if (_currentBlock == null || _currentBlockIndex >= _currentBlock.Length)
                {
                    _currentBlock = await QueryBlockAsync(RepositoryRootPath, contentsWithoutChildren,
                        _currentBlock?[^1].Path, _blockSize, _blockIndex * _blockSize, cancel)
                        .ConfigureAwait(false);
                    _blockIndex++;
                    _currentBlockIndex = 0;
                    if (_currentBlock == null || _currentBlock.Length == 0)
                        return false;
                }

                Content = _currentBlock[_currentBlockIndex++];
                RelativePath = ContentPath.GetRelativePath(Content.Path, RepositoryRootPath);
            } while (SkipContent(Content));

            return true;
        }

        private bool SkipContent(IContent content)
        {
            foreach (var cutoff in _cutoffs)
                if (content.Path.StartsWith(cutoff + "/", StringComparison.OrdinalIgnoreCase))
                    return true;

            if (content.Type != "SystemFolder")
                return false;
            if (content.Name == "Previews")
                return true;

            var parentPath = ContentPath.GetParentPath(content.Path);
            return parentPath.EndsWith("/Previews", StringComparison.OrdinalIgnoreCase) &&
                   System.Text.RegularExpressions.Regex.IsMatch(content.Name, "[vV]\\d+\\.\\d+\\.[aAdDpPlLrR]");
        }

        private IEnumerator<TransferTask> _referenceUpdateTasksEnumerator;
        public void SetReferenceUpdateTasks(IEnumerable<TransferTask> tasks, int taskCount)
        {
            _referenceUpdateTasksEnumerator = tasks.GetEnumerator();
        }

        public async Task<bool> ReadByReferenceUpdateTasksAsync(CancellationToken cancel)
        {
            if (!_referenceUpdateTasksEnumerator.MoveNext())
                return false;

            var task = _referenceUpdateTasksEnumerator.Current;

            await InitializeConnectionAsync(cancel).ConfigureAwait(false);

            RelativePath = task.ReaderPath;
            var repositoryPath = ContentPath.Combine(RepositoryRootPath, task.ReaderPath);
            Content = await GetContentAsync(repositoryPath, task.BrokenReferences, cancel).ConfigureAwait(false);

            return true;
        }

        private readonly List<string> _cutoffs = new();
        private bool _cutoffChanged;
        public void SkipSubtree(string relativePath)
        {
            var absolutePath = ContentPath.GetAbsolutePath(relativePath, RepositoryRootPath);
            if (_cutoffs.All(c => c.Length >= absolutePath.Length || !absolutePath.StartsWith(c)))
            {
                _cutoffs.Add(absolutePath);
                _cutoffChanged = true;
            }
        }

        private static readonly string[] Keywords =
        {
            ".SELECT", ".SKIP", ".TOP", ".SORT", ".REVERSESORT", ".AUTOFILTERS",
            ".LIFESPAN", ".COUNTONLY", ".QUICK", ".ALLVERSIONS",
        };

        private string InitializeFilter(string filter)
        {
            if (filter == null)
                return null;
            foreach (var keyword in Keywords)
                filter = RemoveKeyword(keyword, filter);
            return filter;
        }

        private string RemoveKeyword(string keyword, string filter)
        {
            while (true)
            {
                var p0 = filter.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
                if (p0 < 0)
                    return filter.Trim();
                var p1 = filter.IndexOf(' ', p0 + keyword.Length);

                filter = p1 < 0
                    ? filter.Remove(p0)
                    : filter.Remove(p0, p1 - p0 + 1);
            }
        }

        protected virtual async Task<int> GetCountAsync(CancellationToken cancel)
        {
            try
            {
                var query = string.IsNullOrEmpty(Filter)
                    ? $"+InTree:'{RepositoryRootPath}'"
                    : $"+InTree:'{RepositoryRootPath}' +({Filter})";

                var json = await GetJsonAsync(BuildQueryUri(query + " .COUNTONLY", false, 0, 0, false), cancel)
                    .ConfigureAwait(false);

                return TryParseCount(json, out var count) ? count : 0;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "LegacyRepositoryReader: cannot get count of contents. Continuing without estimated count.");
                return 0;
            }
        }

        protected virtual async Task<IContent[]> QueryBlockAsync(string rootPath, string[] contentsWithoutChildren,
            string lastPath, int top, int skip, CancellationToken cancel)
        {
            string query;
            var orderByPath = true;
            var cutoffClause = GetCutoffClause();
            if (contentsWithoutChildren.Length == 0)
            {
                query = $"+InTree:'{rootPath}'";
                if (Filter != null)
                    query += $" +({Filter})";
                if (cutoffClause != null)
                    query += $" {cutoffClause}";
            }
            else if (contentsWithoutChildren.Length == 1 && contentsWithoutChildren[0] == string.Empty)
            {
                query = $"Path:'{rootPath}'";
                top = 0;
                orderByPath = false;
            }
            else
            {
                var paths = $"('{string.Join("' '", contentsWithoutChildren.Select(x => RepositoryRootPath + '/' + x))}')";
                query = $"+(Path:{paths} (+InTree:'{rootPath}' -InTree:{paths}))";
                if (Filter != null)
                    query += $" +({Filter})";
                if (cutoffClause != null)
                    query += $" {cutoffClause}";
            }

            return await QueryAsync(query, orderByPath, top, skip, cancel).ConfigureAwait(false);
        }

        private string GetCutoffClause()
        {
            if (_cutoffs.Count == 0)
                return null;

            return _cutoffs.Count == 1
                ? $"-Path:'{_cutoffs[0]}/*'"
                : $"-Path:({string.Join(" ", _cutoffs.Select(x => $"'{x}/*'"))})";
        }

        protected virtual async Task<IContent[]> QueryAsync(string queryText, bool orderByPath, int top, int skip,
            CancellationToken cancel)
        {
            try
            {
                var json = await GetJsonAsync(BuildQueryUri(queryText, orderByPath, top, skip, true), cancel)
                    .ConfigureAwait(false);

                return ExtractContents(json)
                    .Select(x => new LegacyRepositoryContent(x, GetStreamAsync))
                    .Cast<IContent>()
                    .ToArray();
            }
            catch (Exception e)
            {
                throw new SnException(0, "LegacyRepositoryReader: cannot get content block.", e);
            }
        }

        protected virtual async Task<IContent> GetContentAsync(string path, string[] fields, CancellationToken cancel)
        {
            try
            {
                var contents = await QueryAsync($"Path:'{path}'", false, 1, 0, cancel).ConfigureAwait(false);
                if (contents.Length == 0)
                    throw new InvalidOperationException("Content was not found: " + path);
                return contents[0];
            }
            catch (Exception e)
            {
                throw new SnException(0, "LegacyRepositoryReader: cannot get content.", e);
            }
        }

        private async Task<JToken> GetJsonAsync(Uri uri, CancellationToken cancel)
        {
            var response = await _httpClient.GetAsync(uri, cancel).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var body = await ReadErrorBodyAsync(response, cancel).ConfigureAwait(false);
                throw new InvalidOperationException(
                    $"LegacyRepositoryReader: GET failed: {(int)response.StatusCode} {response.ReasonPhrase}. Url: {uri}.{body}");
            }

            var text = await response.Content.ReadAsStringAsync(cancel).ConfigureAwait(false);
            return JToken.Parse(text);
        }

        private async Task<Stream> GetStreamAsync(string url, CancellationToken cancel)
        {
            var uri = BuildUri(url);
            var response = await _httpClient.GetAsync(uri, cancel).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var body = await ReadErrorBodyAsync(response, cancel).ConfigureAwait(false);
                throw new InvalidOperationException(
                    $"LegacyRepositoryReader: attachment GET failed: {(int)response.StatusCode} {response.ReasonPhrase}. Url: {uri}.{body}");
            }

            var result = new MemoryStream();
            await using var stream = await response.Content.ReadAsStreamAsync(cancel).ConfigureAwait(false);
            await stream.CopyToAsync(result, cancel).ConfigureAwait(false);
            result.Seek(0, SeekOrigin.Begin);
            return result;
        }

        private Uri BuildQueryUri(string query, bool orderByPath, int top, int skip, bool includeExpand)
        {
            query = AddLegacyQueryOptions(query, orderByPath, top, skip);

            var parameters = new Dictionary<string, string>
            {
                ["query"] = query
            };

            if (includeExpand && !string.IsNullOrWhiteSpace(Args.Expand))
                parameters["$expand"] = Args.Expand;

            return BuildUri("/OData.svc/Root", parameters);
        }

        private static string AddLegacyQueryOptions(string query, bool orderByPath, int top, int skip)
        {
            if (orderByPath)
                query += " .SORT:Path";
            if (top > 0)
                query += $" .TOP:{top}";
            if (skip > 0)
                query += $" .SKIP:{skip}";
            return query;
        }

        private Uri BuildUri(string path, Dictionary<string, string> query = null)
        {
            if (Uri.TryCreate(path, UriKind.Absolute, out var absolute))
                return absolute;

            var baseUri = Url.EndsWith("/") ? new Uri(Url) : new Uri(Url + "/");
            var relativePath = path.TrimStart('/');
            var builder = new UriBuilder(new Uri(baseUri, relativePath));

            if (query != null && query.Count > 0)
            {
                builder.Query = string.Join("&", query.Select(x =>
                    Uri.EscapeDataString(x.Key) + "=" + Uri.EscapeDataString(x.Value)));
            }

            return builder.Uri;
        }

        private static JObject[] ExtractContents(JToken json)
        {
            var token = json["d"]?["results"] ?? json["d"] ?? json["value"] ?? json;
            if (token is JArray array)
                return array.OfType<JObject>().ToArray();
            if (token is JObject obj)
                return new[] { obj };
            return Array.Empty<JObject>();
        }

        private static bool TryParseCount(JToken json, out int count)
        {
            count = 0;

            var tokens = new[]
            {
                json["d"]?["__count"],
                json["d"]?["Count"],
                json["d"]?["count"],
                json["Count"],
                json["count"],
                json["__count"],
                json["d"]
            };

            foreach (var token in tokens)
            {
                if (token == null)
                    continue;
                if (int.TryParse(token.Value<string>(), out count))
                    return true;
            }

            return false;
        }

        private static async Task<string> ReadErrorBodyAsync(HttpResponseMessage response, CancellationToken cancel)
        {
            if (response.Content == null)
                return string.Empty;

            var body = await response.Content.ReadAsStringAsync(cancel).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(body))
                return string.Empty;

            body = body.Trim();
            if (body.Length > 1000)
                body = body.Substring(0, 1000);

            return " Response: " + body;
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }
}
