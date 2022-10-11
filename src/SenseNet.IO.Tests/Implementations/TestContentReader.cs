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
        private int _filteredPathIndex;

        public string RootName { get; }
        public string ReaderRootPath { get; }
        public int EstimatedCount => _tree?.Count ?? 0;
        public IContent Content { get; private set; }
        public string RelativePath { get; private set; }

        public TestContentReader(string rootPath, Dictionary<string, ContentNode> tree)
        {
            _tree = tree;
            ReaderRootPath = rootPath;
            RootName = ContentPath.GetName(rootPath);
            _separator = rootPath.Contains("/") ? "/" : "\\";

            var rootPathTrailing = rootPath + _separator;
            _sortedPaths = _tree.Keys
                .Where(x =>x.StartsWith(rootPathTrailing) || x == rootPath)
                .OrderBy(x => x)
                .ToArray();
            _filteredPathIndex = 0;
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        private Dictionary<string, int> _indexes = new Dictionary<string, int>();
        public Task<bool> ReadSubTreeAsync(string relativePath, CancellationToken cancel = default)
        {
            if (!_indexes.TryGetValue(relativePath, out var index))
                _indexes.Add(relativePath, index = 0);

            var subTreePath = NormalizePath(ContentPath.GetAbsolutePath(relativePath, ReaderRootPath));

            var paths = _sortedPaths.Where(x => x.StartsWith(subTreePath + _separator) || x == subTreePath).ToArray();
            if (index >= paths.Length)
                return Task.FromResult(false);

            var content = _tree[paths[index]];
            RelativePath = ContentPath.GetRelativePath(content.Path, ReaderRootPath);
            Content = content.Clone();

            _indexes[relativePath] = ++index;
            return Task.FromResult(true);
        }

        private string[] _filteredPaths;
        public Task<bool> ReadAllAsync(string[] contentsWithoutChildren, CancellationToken cancel = default)
        {
            if (_filteredPaths == null)
            {
                var filters = contentsWithoutChildren
                    .Select(x => NormalizePath(ContentPath.GetAbsolutePath(x, ReaderRootPath)) + "\\")
                    .ToArray();
                _filteredPaths = _sortedPaths
                    .Where(x =>
                    {
                        foreach (var filter in filters)
                            if (x.StartsWith(filter))
                                return false;
                        return true;
                    })
                    .ToArray();
            }

            if (_filteredPathIndex >= _filteredPaths.Length)
                return Task.FromResult(false);
            var sourceContent = _tree[_filteredPaths[_filteredPathIndex]];
            RelativePath = ContentPath.GetRelativePath(sourceContent.Path, ReaderRootPath);
            Content = sourceContent.Clone();

            _filteredPathIndex++;
            return Task.FromResult(true);
        }

        public void SetReferenceUpdateTasks(IEnumerable<TransferTask> tasks, int taskCount)
        {
            _referenceUpdateTasks = tasks.ToArray();
        }

        private int _referenceUpdateTaskIndex;
        private TransferTask[] _referenceUpdateTasks;
        public Task<bool> ReadByReferenceUpdateTasksAsync(CancellationToken cancel)
        {
            if(_referenceUpdateTaskIndex >= _referenceUpdateTasks.Length)
                return Task.FromResult(false);

            var task = _referenceUpdateTasks[_referenceUpdateTaskIndex++];
            var path = NormalizePath(ContentPath.GetAbsolutePath(task.ReaderPath, ReaderRootPath));
            Content = _tree[path];
            RelativePath = task.ReaderPath;

            return Task.FromResult(true);
        }


        private string NormalizePath(string path)
        {
            return _separator == "/" ? path.Replace('\\', '/') : path.Replace('/', '\\');
        }

    }
}
