using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SenseNet.IO.Tests.Implementations
{
    public class TestContentReader : IContentReader
    {
        private readonly Dictionary<string, ContentNode> _tree;

        private readonly string[] _sortedPaths;
        private int _sortedPathIndex;

        public string RootPath { get; }
        public int EstimatedCount => _tree?.Count ?? 0;
        public IContent Content { get; private set; }
        public string RelativePath { get; private set; }

        public TestContentReader(string rootPath, Dictionary<string, ContentNode> tree)
        {
            _tree = tree;
            RootPath = rootPath;

            var rootPathTrailing = rootPath + "/";
            _sortedPaths = _tree.Keys
                .Where(x =>x.StartsWith(rootPathTrailing) || x == rootPath)
                .OrderBy(x => x)
                .ToArray();
            _sortedPathIndex = 0;
        }

        public Task<bool> ReadContentTypesAsync(CancellationToken cancel = default) { throw new System.NotImplementedException(); }
        public Task<bool> ReadSettingsAsync(CancellationToken cancel = default) { throw new System.NotImplementedException(); }
        public Task<bool> ReadAspectsAsync(CancellationToken cancel = default) { throw new System.NotImplementedException(); }
        public Task<bool> ReadAllAsync(CancellationToken cancel = default)
        {
            if (_sortedPathIndex >= _sortedPaths.Length)
                return Task.FromResult(false);
            var sourceContent = _tree[_sortedPaths[_sortedPathIndex]];
            RelativePath = ContentPath.GetRelativePath(sourceContent.Path, RootPath);
            Content = sourceContent.Clone();

            _sortedPathIndex++;
            return Task.FromResult(true);
        }
    }
}
