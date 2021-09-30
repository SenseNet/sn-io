using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using SenseNet.Client;
using SenseNet.Client.Authentication;

namespace SenseNet.IO.Implementations
{
    public class RepositoryAuthenticationOptions
    {
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
    }
    public class RepositoryReaderArgs
    {
        public string Url { get; set; }
        public string Path { get; set; }
        public int? BlockSize { get; set; }
        public string Query { get; set; }
        public RepositoryAuthenticationOptions Authentication { get; set; } = new RepositoryAuthenticationOptions();
    }

    /// <summary>
    /// Reads a subtree of a sensenet repository order by path.
    /// </summary>
    public class RepositoryReader : IContentReader
    {
        public RepositoryReaderArgs Args { get; }
        private readonly int _blockSize;
        private int _blockIndex;
        private readonly ITokenStore _tokenStore;
        private ServerContext _server;

        public string Url { get; }
        public string RootName { get; }
        public string RepositoryRootPath { get; }
        public string Filter { get; }
        public int EstimatedCount { get; private set; }
        public IContent Content { get; private set; }
        public string RelativePath { get; private set; }

        public RepositoryReader(ITokenStore tokenStore, IOptions<RepositoryReaderArgs> args)
        {
            if (args == null)
                throw new ArgumentNullException(nameof(args));
            Args = args.Value;

            Args.Path ??= "/Root";
            Args.BlockSize ??= 10;

            Url = Args.Url ?? throw new ArgumentException("RepositoryReader: Invalid URL.");
            RepositoryRootPath = Args.Path;
            RootName = ContentPath.GetName(Args.Path);
            Filter = InitializeFilter(args.Value.Query);
            _blockSize = Args.BlockSize.Value;
            _tokenStore = tokenStore;
        }

        private async Task InitializeAsync()
        {
            if (Content == null)
            {
                var server = new ServerContext
                {
                    Url = Url,
                    Username = "builtin\\admin",
                    Password = "admin"
                };

                // this will take precedence over the username and password
                if (!string.IsNullOrEmpty(Args.Authentication.ClientId))
                {
                    server.Authentication.AccessToken = await _tokenStore
                        .GetTokenAsync(server, Args.Authentication.ClientId, Args.Authentication.ClientSecret);
                }

                //ClientContext.Current.AddServer(server);
                _server = server;

                // Get tree size before first read
                EstimatedCount = await GetCountAsync();
            }
        }

        private class TreeState
        {
            public string AbsolutePath;
            public IContent[] CurrentBlock;
            public int BlockIndex;
            public int CurrentBlockIndex;
        }
        private Dictionary<string, TreeState> _treeStates = new();
        public async Task<bool> ReadSubTreeAsync(string relativePath, CancellationToken cancel = default)
        {
            await InitializeAsync();

            if (!_treeStates.TryGetValue(relativePath, out var treeState))
            {
                treeState = new TreeState
                {
                    AbsolutePath = ContentPath.Combine(RepositoryRootPath, relativePath),
                };
                _treeStates.Add(relativePath, treeState);
            }

            //TODO: Raise performance: read the next block (background)
            if (treeState.CurrentBlock == null || treeState.CurrentBlockIndex >= treeState.CurrentBlock.Length)
            {
                treeState.CurrentBlock = await QueryBlockAsync(treeState.AbsolutePath, Array.Empty<string>(), treeState.BlockIndex * _blockSize, _blockSize);
                treeState.BlockIndex++;
                treeState.CurrentBlockIndex = 0;
                if (treeState.CurrentBlock == null || treeState.CurrentBlock.Length == 0)
                    return false;
            }

            Content = treeState.CurrentBlock[treeState.CurrentBlockIndex++];
            RelativePath = ContentPath.GetRelativePath(Content.Path, RepositoryRootPath);
            return true;
        }

        private IContent[] _currentBlock;
        private int _currentBlockIndex;
        public async Task<bool> ReadAllAsync(string[] contentsWithoutChildren, CancellationToken cancel = default)
        {
            await InitializeAsync();

            //TODO: Raise performance: read the next block (background)
            if (_currentBlock == null || _currentBlockIndex >= _currentBlock.Length)
            {
                _currentBlock = await QueryBlockAsync(RepositoryRootPath, contentsWithoutChildren, _blockIndex * _blockSize, _blockSize);
                _blockIndex++;
                _currentBlockIndex = 0;
                if (_currentBlock == null || _currentBlock.Length == 0)
                    return false;
            }

            Content = _currentBlock[_currentBlockIndex++];
            RelativePath = ContentPath.GetRelativePath(Content.Path, RepositoryRootPath);
            return true;
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

            RelativePath = task.ReaderPath;
            var repositoryPath = ContentPath.Combine(RepositoryRootPath, task.ReaderPath);
            Content = await GetContentAsync(repositoryPath, task.BrokenReferences);

            return true;
        }


        private static readonly string[] _keywords = new[]
        {
            ".SELECT",
            ".SKIP",
            ".TOP",
            ".SORT",
            ".REVERSESORT",
            ".AUTOFILTERS",
            ".LIFESPAN",
            ".COUNTONLY",
            ".QUICK",
            ".ALLVERSIONS",
        };
        private string InitializeFilter(string query)
        {
            if (query == null)
                return null;
            foreach (var keyword in _keywords)
                query = RemoveKeyword(keyword, query);
            return query;
        }
        private string RemoveKeyword(string keyword, string query)
        {
            while (true)
            {
                var p0 = query.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
                if (p0 < 0)
                    return query.Trim();
                var p1 = query.IndexOf(' ', p0 + keyword.Length);

                query = p1 < 0
                    ? query.Remove(p0)
                    : query.Remove(p0, p1 - p0 +1);
            }
        }

        /* =================================================================================== TESTABLE METHODS FOR MOCKS */

        protected virtual async Task<int> GetCountAsync()
        {
            try
            {
                var result = await RESTCaller.GetResponseStringAsync(RepositoryRootPath, "GetContentCountInTree", server: _server);
                return int.TryParse(result, out var count) ? count : default;
            }
            catch (Exception e)
            {
                throw new SnException(0, "RepositoryReader: cannot get count of contents.", e);
            }
        }
        protected virtual async Task<IContent[]> QueryBlockAsync(string rootPath, string[] contentsWithoutChildren, int skip, int top)
        {
            string query;
            if (contentsWithoutChildren.Length == 0)
            {
                query = Filter != null ? $"+InTree:'{rootPath}' +({Filter})" : $"InTree:'{rootPath}'";
                query += $" .SORT:Path .TOP:{top} .SKIP:{skip} .AUTOFILTERS:OFF";
            }
            else if (contentsWithoutChildren.Length == 1 && contentsWithoutChildren[0] == string.Empty)
            {
                query = $"Path:'{rootPath}' .AUTOFILTERS:OFF";
            }
            else
            {
                var paths = $"('{string.Join("' '", contentsWithoutChildren.Select(x => RepositoryRootPath + '/' + x))}')";
                query = $"Path:{paths} (+InTree:'{rootPath}' -InTree:{paths}) .SORT:Path .TOP:{top} .SKIP:{skip} .AUTOFILTERS:OFF";
            }

            var queryResult = await QueryAsync(query).ConfigureAwait(false);
            return queryResult;
        }
        protected virtual async Task<IContent[]> QueryAsync(string queryText)
        {
            var oDataRequest = new ODataRequest(_server)
            {
                Path = "/Root",
                ContentQuery = queryText,
                Parameters = {{"$format", "export"}}
            };
            try
            {
                var result = await Client.Content.LoadCollectionAsync(oDataRequest, _server).ConfigureAwait(false);
                var transformed = result.Select(x => new RepositoryReaderContent(x)).ToArray();
                // ReSharper disable once CoVariantArrayConversion
                return transformed;
            }
            catch (Exception e)
            {
                throw new SnException(0, "RepositoryReader: cannot get content block.", e);
            }
        }

        readonly string[] _idFields = {"Id", "Path"} ;
        protected virtual async Task<IContent> GetContentAsync(string path, string[] fields)
        {
            var oDataRequest = new ODataRequest(_server)
            {
                Path = path,
                Select = _idFields.Union(fields),
                Parameters = { { "$format", "export" } }
            };
            try
            {
                var result = await Client.Content.LoadAsync(oDataRequest, _server).ConfigureAwait(false);
                var transformed = new RepositoryReaderContent(result);
                return transformed;
            }
            catch (Exception e)
            {
                throw new SnException(0, "RepositoryReader: cannot get content.", e);
            }
        }
    }
}
