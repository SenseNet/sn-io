using System;
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
        private readonly string _url;
        private readonly int _blockSize;
        private int _blockIndex;

        public string RootPath { get; }
        public int EstimatedCount { get; private set; }
        public IContent Content { get; private set; }
        public string RelativePath { get; private set; }

        public RepositoryTreeReader(string url, [NotNull] string rootPath, int? blockSize = null)
        {
            _url = url;
            RootPath = rootPath;
            _blockSize = blockSize ?? 10;
        }

        private IContent[] _currentBlock;
        private int _currentBlockIndex;
        public async Task<bool> ReadAsync(CancellationToken cancel = default)
        {
            if (Content == null)
            {
                ClientContext.Current.AddServer(new ServerContext
                {
                    Url = _url,
                    Username = "builtin\\admin",
                    Password = "admin"
                });

                // Get tree size before first read
                EstimatedCount = await GetCountAsync();
            }

            //TODO: Raise performance: read the next block (background)
            if (_currentBlock == null || _currentBlockIndex >= _currentBlock.Length)
            {
                _currentBlock = await QueryBlockAsync(_blockIndex * _blockSize, _blockSize);
                _blockIndex++;
                _currentBlockIndex = 0;
                if (_currentBlock == null || _currentBlock.Length == 0)
                    return false;
            }

            Content = _currentBlock[_currentBlockIndex++];
            RelativePath = ContentPath.GetRelativePath(Content.Path, RootPath);

            return true;
        }

        private async Task<int> GetCountAsync()
        {
            var result = await RESTCaller.GetResponseStringAsync(RootPath, "GetContentCountInTree");
            return int.TryParse(result, out var count) ? count : default;
        }

        private async Task<IContent[]> QueryBlockAsync(int skip, int top)
        {
            var query = $"InTree:'{RootPath}' .SORT:Path .TOP:{top} .SKIP:{skip} .AUTOFILTERS:OFF";
            var queryResult = await Client.Content.QueryAsync(query).ConfigureAwait(false);

            var result = queryResult.Select(x => new ClientContentWrapper(x)).ToArray();

            return result;
        }
    }
}
