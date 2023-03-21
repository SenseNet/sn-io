﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SenseNet.Client;

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
    public class RepositoryReaderArgs
    {
        public string Url { get; set; }
        public string Path { get; set; }
        public int? BlockSize { get; set; }
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
        private async Task InitializeInternalAsync()
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
            EstimatedCount = await GetCountAsync();
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
            await InitializeInternalAsync();

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
                        treeState.BlockIndex * _blockSize, _blockSize);
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
            await InitializeInternalAsync();

            do
            {
                //TODO: Raise performance: read the next block (background)
                if (_currentBlock == null || _currentBlockIndex >= _currentBlock.Length)
                {
                    _currentBlock = await QueryBlockAsync(RepositoryRootPath, contentsWithoutChildren,
                        _blockIndex * _blockSize, _blockSize);
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

        protected virtual async Task<int> GetCountAsync()
        {
            string result;
            if (string.IsNullOrEmpty(Filter))
            {
                try
                {
                    result = await RESTCaller.GetResponseStringAsync(RepositoryRootPath, "GetContentCountInTree",
                        server: _repository.Server);
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
                result = await RESTCaller.GetResponseStringAsync(req, server: _repository.Server);
                return int.TryParse(result, out var count2) ? count2 : default;
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
            var request = new LoadCollectionRequest
            {
                Path = "/Root",
                ContentQuery = queryText,
                Parameters = {{"$format", "export"}}
            };
            try
            {
                var result = await _repository.LoadCollectionAsync(request, CancellationToken.None)
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
