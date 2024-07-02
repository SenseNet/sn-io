using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SenseNet.Client;
using SenseNet.Tools.Configuration;

namespace SenseNet.IO.Implementations
{
    public class RepositoryAuthenticationOptions
    {
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string ApiKey { get; set; }

        internal RepositoryAuthenticationOptions Clone()
        {
            return new RepositoryAuthenticationOptions
            {
                ClientId = ClientId,
                ClientSecret = ClientSecret,
                ApiKey = ApiKey
            };
        }
    }

    [OptionsClass(sectionName: "repositoryReader")]
    public class RepositoryReaderArgs
    {
        /// <summary>
        /// Repository url.
        /// </summary>
        public string Url { get; set; }
        /// <summary>
        /// Content path in the repository. Default: /Root
        /// </summary>
        public string Path { get; set; }
        /// <summary>
        /// Number of loaded contents per request. Default: 10.
        /// </summary>
        public int? BlockSize { get; set; }
        /// <summary>
        /// Content query filter for reading contents from the repository.
        /// </summary>
        public string Filter { get; set; }
        public RepositoryAuthenticationOptions Authentication { get; set; } = new();

        internal RepositoryReaderArgs Clone()
        {
            return new RepositoryReaderArgs
            {
                Url = Url,
                Path = Path,
                BlockSize = BlockSize,
                Filter = Filter,
                Authentication = Authentication?.Clone() ?? new RepositoryAuthenticationOptions()
            };
        }
    }

    /// <summary>
    /// Reads a subtree of a sensenet repository order by path.
    /// </summary>
    public class RepositoryReader : ISnRepositoryReader
    {
        public RepositoryReaderArgs Args { get; }
        private int _blockSize;
        private int _blockIndex;
        private readonly IRepositoryCollection _repositoryCollection;
        private readonly ILogger<RepositoryReader> _logger;
        private IRepository _repository;

        public string Url => Args.Url;
        public string RootName => ContentPath.GetName(Args.Path);
        public string RepositoryRootPath => Args.Path;
        public string Filter { get; private set; }
        public int EstimatedCount { get; private set; }
        public IContent Content { get; private set; }
        public string RelativePath { get; private set; }

        public RepositoryReaderArgs ReaderOptions => Args;

        public RepositoryReader(IRepositoryCollection repositoryCollection, IOptions<RepositoryReaderArgs> args, 
            ILogger<RepositoryReader> logger)
        {
            if (args?.Value == null)
                throw new ArgumentNullException(nameof(args));
            Args = args.Value.Clone();

            Args.Path ??= "/Root";
            Args.BlockSize ??= 10;

            _blockSize = Args.BlockSize.Value;
            _repositoryCollection = repositoryCollection;
            _logger = logger;
        }

        public virtual Task InitializeAsync()
        {
            if (Content == null)
            {
                //initialize properties from configured options
                if (Url == null)
                    throw new ArgumentException("RepositoryReader: empty URL.");

                Filter = InitializeFilter(Args.Filter);
                _blockSize = Args.BlockSize ??= 10;
            }

            return Task.CompletedTask;
        }

        private bool _initializedInternal;
        private async Task InitializeInternalAsync(CancellationToken cancel)
        {
            if (_initializedInternal)
                return;

            _initializedInternal = true;

            await InitializeAsync().ConfigureAwait(false);
            
            _repository = await _repositoryCollection.GetRepositoryAsync("source", CancellationToken.None)
                .ConfigureAwait(false);

            // workaround while the client api sets the logger internally
            if (_repository.Server is { Logger: null })
                _repository.Server.Logger = _logger;

            // Get tree size before first read
            EstimatedCount = await GetCountAsync(cancel);
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
            await InitializeInternalAsync(cancel);

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
                //TODO: Raise performance: read the next block (background)
                if (treeState.CurrentBlock == null || treeState.CurrentBlockIndex >= treeState.CurrentBlock.Length)
                {
                    treeState.CurrentBlock = await QueryBlockAsync(treeState.AbsolutePath, Array.Empty<string>(),
                        treeState.CurrentBlock?[^1].Path, _blockSize, treeState.BlockIndex * _blockSize, cancel);
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
            await InitializeInternalAsync(cancel);

            do
            {
                if (_cutoffChanged)
                {
                    // Query the last block again with the new cutoff path
                    _currentBlock = await QueryBlockAsync(RepositoryRootPath, contentsWithoutChildren,
                        _currentBlock?[^1].Path, _blockSize, (_blockIndex - 1) * _blockSize, cancel);
                    // Serve the content from the last position again
                    _currentBlockIndex--;
                    // Changes processed
                    _cutoffChanged = false;
                }
                //TODO: Raise performance: read the next block (background)
                else if (_currentBlock == null || _currentBlockIndex >= _currentBlock.Length)
                {
                    _currentBlock = await QueryBlockAsync(RepositoryRootPath, contentsWithoutChildren,
                        _currentBlock?[^1].Path, _blockSize, _blockIndex * _blockSize, cancel);
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

        private readonly Regex _previewFolderRegex = new("[vV]\\d+\\.\\d+\\.[aAdDpPlLrR]", RegexOptions.Compiled);

        private bool SkipContent(IContent content)
        {
            foreach (var cutoff in _cutoffs)
                if (content.Path.StartsWith(cutoff + "/"))
                    return true;

            //TODO: find a better algorithm for marking contents to skip.
            // Currently we skip preview folders based on their names. A better solution 
            // would be to move this flag to the server and make it available in a search query
            // (a field or a new container type).
            if (content.Type != "SystemFolder")
                return false;
            if (content.Name == "Previews")
                return true;

            // check if this is a version folder under Previews
            var parentPath = RepositoryPath.GetParentPath(content.Path);
            if (parentPath.EndsWith("/Previews") && _previewFolderRegex.IsMatch(content.Name))
                return true;

            return false;
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

        private readonly List<string> _cutoffs = new();
        private bool _cutoffChanged;
        public void SkipSubtree(string relativePath)
        {
            var absolutePath = ContentPath.GetAbsolutePath(relativePath, RepositoryRootPath);
            // Add to _cutoffs if the new item is not a subtree of any stored item.
            if (_cutoffs.All(c => c.Length >= absolutePath.Length || !absolutePath.StartsWith(c)))
            {
                _cutoffs.Add(absolutePath);
                _cutoffChanged = true;
            }
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
        private string InitializeFilter(string filter)
        {
            if (filter == null)
                return null;
            foreach (var keyword in _keywords)
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
                    : filter.Remove(p0, p1 - p0 +1);
            }
        }

        /* =================================================================================== TESTABLE METHODS FOR MOCKS */

        protected virtual async Task<int> GetCountAsync(CancellationToken cancel)
        {
            string result;
            if (string.IsNullOrEmpty(Filter))
            {
                try
                {
//result = await RESTCaller.GetResponseStringAsync(RepositoryRootPath, "GetContentCountInTree",
//    server: _repository.Server);
                    result = await _repository.GetResponseStringAsync(
                        new ODataRequest(_repository.Server) {Path = RepositoryRootPath, ActionName = "GetContentCountInTree"},
                        HttpMethod.Get, cancel);
                    return int.TryParse(result, out var count1) ? count1 : default;
                }
                catch (Exception e)
                {
                    throw new SnException(0, "RepositoryReader: cannot get count of contents.", e);
                }
            }

            try
            {
                var req = new ODataRequest(_repository.Server)
                {
                    Path = RepositoryRootPath,
                    ActionName = "GetContentCountInTree",
                    ContentQuery = Filter
                };
//result = await RESTCaller.GetResponseStringAsync(req, server: _repository.Server);
                result = await _repository.GetResponseStringAsync(req, HttpMethod.Get, cancel);
                return int.TryParse(result, out var count2) ? count2 : default;
            }
            catch (Exception e)
            {
                throw new SnException(0, "RepositoryReader: cannot get count of contents.", e);
            }
        }
        protected virtual async Task<IContent[]> QueryBlockAsync(string rootPath, string[] contentsWithoutChildren,
            string lastPath, int top, int skip, CancellationToken cancel)
        {
            // Build query
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
            var queryResult = await QueryAsync(query, orderByPath, top, skip, cancel).ConfigureAwait(false);
            return queryResult;
        }
        private string GetCutoffClause()
        {
            if (_cutoffs.Count == 0)
                return null;

            return _cutoffs.Count == 1
                ? $"-Path:'{_cutoffs[0]}/*'"
                : $"-Path:({string.Join(" ", _cutoffs.Select(x => $"'{x}/*'"))})";
        }

        protected virtual async Task<IContent[]> QueryAsync(string queryText, bool orderByPath, int top, int skip, CancellationToken cancel)
        {
            var request = new QueryContentRequest
            {
                Path = "/Root",
                ContentQuery = queryText,
                AutoFilters = FilterStatus.Disabled,
                Parameters = {{"$format", "export"}}
            };
            if (top > 0)
                request.Top = top;
            if (skip > 0)
                request.Skip = skip;
            if (orderByPath)
                request.OrderBy = new[] {"Path"};

            try
            {
                var result = await _repository.QueryAsync(request, cancel)
                    .ConfigureAwait(false);
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
            var request = new LoadContentRequest
            {
                Path = path,
                Select = _idFields.Union(fields),
                Parameters = { { "$format", "export" } }
            };
            try
            {
                var result = await _repository.LoadContentAsync(request, CancellationToken.None)
                    .ConfigureAwait(false);
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
