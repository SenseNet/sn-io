using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SenseNet.IO.Tests.Implementations
{
    public class TestContentReader : IContentReader<ContentNode>
    {
        private readonly Dictionary<string, ContentNode> _tree;

        private readonly string[] _sortedPaths;
        private int _sortedPathIndex;

        public int EstimatedCount => _tree?.Count ?? 0;
        public ContentNode Content { get; private set; }


        public TestContentReader(string rootPath, Dictionary<string, ContentNode> tree)
        {
            _tree = tree;

            var rootPathTrailing = rootPath + "/";
            _sortedPaths = _tree.Keys
                .Where(x =>x.StartsWith(rootPathTrailing) || x == rootPath)
                .OrderBy(x => x)
                .ToArray();
            _sortedPathIndex = 0;
        }

        public Task<bool> ReadAsync(CancellationToken cancel = default)
        {
            if (_sortedPathIndex >= _sortedPaths.Length)
                return Task.FromResult(false);
            Content = _tree[_sortedPaths[_sortedPathIndex]];
            _sortedPathIndex++;
            return Task.FromResult(true);
        }
    }
}
