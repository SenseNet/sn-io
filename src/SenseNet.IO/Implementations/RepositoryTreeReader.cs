using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SenseNet.Client;

namespace SenseNet.IO.Implementations
{
    /// <summary>
    /// Reads a subtree of a sensenet repository order by path.
    /// </summary>
    public class RepositoryTreeReader : IContentReader
    {
        private readonly int _blockSize;
        private int _blockIndex;

        public string Url { get; }
        public string RootName { get; }
        public string RepositoryRootPath { get; }
        public int EstimatedCount { get; private set; }
        public IContent Content { get; private set; }
        public string RelativePath { get; private set; }

        public RepositoryTreeReader(string url, [NotNull] string rootPath, int? blockSize = null)
        {
            Url = url;
            RepositoryRootPath = rootPath;
            RootName = ContentPath.GetName(rootPath);
            _blockSize = blockSize ?? 10;
        }

        private async Task InitializeAsync()
        {
            if (Content == null)
            {
                ClientContext.Current.AddServer(new ServerContext
                {
                    Url = Url,
                    Username = "builtin\\admin",
                    Password = "admin"
                });

                // Get tree size before first read
                EstimatedCount = await GetCountAsync();
            }
        }

//UNDONE://///
        //private IContent[] _contentTypeContents;
        //private int _contentTypeContentsIndex;
        //public async Task<bool> ReadContentTypesAsync_DELETE(CancellationToken cancel = default)
        //{
        //    if (_contentTypeContents == null)
        //    {
        //        await InitializeAsync();

        //        if (!RootPath.Equals("/Root", StringComparison.OrdinalIgnoreCase) &&
        //            !RootPath.Equals("/Root/System", StringComparison.OrdinalIgnoreCase) &&
        //            !RootPath.Equals("/Root/System/Schema", StringComparison.OrdinalIgnoreCase) &&
        //            !RootPath.Equals("/Root/System/Schema/ContentTypes", StringComparison.OrdinalIgnoreCase)
        //            )
        //            return false;

        //        _contentTypeContents = await QueryBlockAsync("/Root/System/Schema/ContentTypes", 1, int.MaxValue, false); // Skip subtree-root
        //    }

        //    if (_contentTypeContentsIndex >= _contentTypeContents.Length)
        //        return false;
        //    Content = _contentTypeContents[_contentTypeContentsIndex++];
        //    RelativePath = ContentPath.GetRelativePath(Content.Path, RootPath);
        //    return true;
        //}

        //private IContent[] _settingsContents;
        //private int _settingsContentsIndex;
        //public async Task<bool> ReadSettingsAsync_DELETE(CancellationToken cancel = default)
        //{
        //    if (_settingsContents == null)
        //    {
        //        await InitializeAsync();

        //        if (!RootPath.Equals("/Root", StringComparison.OrdinalIgnoreCase) &&
        //            !RootPath.Equals("/Root/System", StringComparison.OrdinalIgnoreCase) &&
        //            !RootPath.Equals("/Root/System/Settings", StringComparison.OrdinalIgnoreCase))
        //            return false;

        //        _settingsContents = await QueryBlockAsync("/Root/System/Settings", 1, int.MaxValue, false); // Skip subtree-root
        //    }

        //    if (_settingsContentsIndex >= _settingsContents.Length)
        //        return false;
        //    Content = _settingsContents[_settingsContentsIndex++];
        //    RelativePath = ContentPath.GetRelativePath(Content.Path, RootPath);
        //    return true;
        //}

        //private IContent[] _aspectContents;
        //private int _aspectContentsIndex;
        //public async Task<bool> ReadAspectsAsync_DELETE(CancellationToken cancel = default)
        //{
        //    if (_aspectContents == null)
        //    {
        //        await InitializeAsync();

        //        if (!RootPath.Equals("/Root", StringComparison.OrdinalIgnoreCase) &&
        //            !RootPath.Equals("/Root/System", StringComparison.OrdinalIgnoreCase) &&
        //            !RootPath.Equals("/Root/System/Schema", StringComparison.OrdinalIgnoreCase) &&
        //            !RootPath.Equals("/Root/System/Schema/Aspects", StringComparison.OrdinalIgnoreCase)
        //        )
        //            return false;

        //        _aspectContents = await QueryBlockAsync("/Root/System/Schema/Aspects", 1, int.MaxValue, false); // Skip subtree-root
        //    }

        //    if (_aspectContentsIndex >= _aspectContents.Length)
        //        return false;
        //    Content = _aspectContents[_aspectContentsIndex++];
        //    RelativePath = ContentPath.GetRelativePath(Content.Path, RootPath);
        //    return true;
        //}

        public Task<bool> ReadSubTreeAsync(string relativePath, CancellationToken cancel = default)
        {
            //UNDONE:!!!!!!!!!!!!!! ReadSubTreeAsync is not implemented
            throw new NotImplementedException();
        }

        private IContent[] _currentBlock;
        private int _currentBlockIndex;
        public async Task<bool> ReadAllAsync(string[] contentsWithoutChildren, CancellationToken cancel = default)
        {
            if (contentsWithoutChildren != null && contentsWithoutChildren.Length != 0)
                //UNDONE:!!!!!!!!! Process "contentsWithoutChildren" parameter
                throw new NotImplementedException();

            await InitializeAsync();

            //TODO: Raise performance: read the next block (background)
            if (_currentBlock == null || _currentBlockIndex >= _currentBlock.Length)
            {
                _currentBlock = await QueryBlockAsync(RepositoryRootPath, _blockIndex * _blockSize, _blockSize, false);
                _blockIndex++;
                _currentBlockIndex = 0;
                if (_currentBlock == null || _currentBlock.Length == 0)
                    return false;
            }

            Content = _currentBlock[_currentBlockIndex++];
            RelativePath = ContentPath.GetRelativePath(Content.Path, RepositoryRootPath);
            return true;
        }

        public void SetReferenceUpdateTasks(IEnumerable<TransferTask> tasks, int taskCount)
        {
            //UNDONE: implement SetReferenceUpdateTasks()
            throw new NotImplementedException();
        }
        public Task<bool> ReadByReferenceUpdateTasksAsync(CancellationToken cancel)
        {
            //UNDONE: implement ReadRandomAsync()
            throw new NotImplementedException();
        }

        private async Task<int> GetCountAsync()
        {
            var result = await RESTCaller.GetResponseStringAsync(RepositoryRootPath, "GetContentCountInTree");
            return int.TryParse(result, out var count) ? count : default;
        }
        private async Task<IContent[]> QueryBlockAsync(string rootPath, int skip, int top, bool useTypeRestrictions)
        {
            var query = useTypeRestrictions
                ? $"InTree:'{rootPath}' -TypeIs:'ContentType' -TypeIs:'Settings' -TypeIs:'Aspect' .SORT:Path .TOP:{top} .SKIP:{skip} .AUTOFILTERS:OFF"
                : $"InTree:'{rootPath}' .SORT:Path .TOP:{top} .SKIP:{skip} .AUTOFILTERS:OFF";
            var queryResult = await QueryAsync(query).ConfigureAwait(false);

            var result = queryResult.Select(x => new RepositoryReaderContent(x)).ToArray();

            // ReSharper disable once CoVariantArrayConversion
            return result;
        }
        public static async Task<IEnumerable<Content>> QueryAsync(string queryText, ServerContext server = null)
        {
            var oDataRequest = new ODataRequest(server)
            {
                Path = "/Root",
                ContentQuery = queryText,
                Parameters = {{"$format", "export"}}
            };

            return await Client.Content.LoadCollectionAsync(oDataRequest, server).ConfigureAwait(false);
        }

    }
}
