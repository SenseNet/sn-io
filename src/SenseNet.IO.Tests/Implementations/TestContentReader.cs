using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SenseNet.IO.Tests.Implementations
{
    public class TestContentReader : IContentReader
    {
        private readonly Dictionary<string, ContentNode> _tree;
        private readonly string _separator;

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
            _separator = rootPath.Contains("/") ? "/" : "\\";

            var rootPathTrailing = rootPath + _separator;
            _sortedPaths = _tree.Keys
                .Where(x =>x.StartsWith(rootPathTrailing) || x == rootPath)
                .OrderBy(x => x)
                .ToArray();
            _sortedPathIndex = 0;
        }

        public Task<bool> ReadContentTypesAsync(CancellationToken cancel = default) { return Task.FromResult(false); }
        public Task<bool> ReadSettingsAsync(CancellationToken cancel = default) { return Task.FromResult(false); }
        public Task<bool> ReadAspectsAsync(CancellationToken cancel = default) { return Task.FromResult(false); }

        private Dictionary<string, int> _indexes = new Dictionary<string, int>();
        public Task<bool> ReadSubTreeAsync(string relativePath, CancellationToken cancel = default)
        {
            if (!_indexes.TryGetValue(relativePath, out var index))
                _indexes.Add(relativePath, index = 0);

            var subTreePath = NormalizePath(ContentPath.GetAbsolutePath(relativePath, RootPath));

            var paths = _sortedPaths.Where(x => x.StartsWith(subTreePath + _separator) || x == subTreePath).ToArray();
            if (index >= paths.Length)
                return Task.FromResult(false);

            var content = _tree[paths[index]];
            RelativePath = ContentPath.GetRelativePath(content.Path, RootPath);
            Content = content.Clone();

            _indexes[relativePath] = ++index;
            return Task.FromResult(true);
        }

        public Task<bool> ReadAllAsync(string[] contentsWithoutChildren, CancellationToken cancel = default)
        {
            //UNDONE:!!!!!!!!! Process "contentsWithoutChildren" parameter
            if (_sortedPathIndex >= _sortedPaths.Length)
                return Task.FromResult(false);
            var sourceContent = _tree[_sortedPaths[_sortedPathIndex]];
            RelativePath = ContentPath.GetRelativePath(sourceContent.Path, RootPath);
            Content = sourceContent.Clone();

            _sortedPathIndex++;
            return Task.FromResult(true);
        }

        public void SetReferenceUpdateTasks(IEnumerable<TransferTask> tasks, int taskCount) { }
        public Task<bool> ReadByReferenceUpdateTasksAsync(CancellationToken cancel) { return Task.FromResult(false); }


        private string NormalizePath(string path)
        {
            return _separator == "/" ? path.Replace('\\', '/') : path.Replace('/', '\\');
        }

    }
}
