using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SenseNet.IO.Tests.Implementations
{
    /// <summary>
    /// Simulates ContentQuery-based block reader
    /// </summary>
    // ReSharper disable once InconsistentNaming
    internal class TestCQReader : IContentReader
    {
        private readonly Dictionary<string, ContentNode> _tree;
        private readonly int _blockSize;
        private int _blockIndex;

        public string RootPath { get; }
        public int EstimatedCount => _tree?.Count ?? 0;

        public IContent Content { get; private set; }
        public string RelativePath { get; private set; }

        public TestCQReader(string rootPath, int blockSize, Dictionary<string, ContentNode> tree)
        {
            RootPath = rootPath;
            _blockSize = blockSize;
            _blockIndex = 0;
            _tree = tree;
        }

        private int _contentTypeIndex;
        private ContentNode[] _contentTypes;
        public Task<bool> ReadContentTypesAsync(CancellationToken cancel = default)
        {
            _contentTypes ??= _tree.Values.Where(x => x.Type == "ContentType").ToArray();
            if (_contentTypeIndex >= _contentTypes.Length)
                return Task.FromResult(false);
            Content = _contentTypes[_contentTypeIndex++].Clone();
            RelativePath = ContentPath.GetRelativePath(Content.Path, RootPath);
            return Task.FromResult(true);
        }

        private int _settingsIndex;
        private ContentNode[] _settings;
        public Task<bool> ReadSettingsAsync(CancellationToken cancel = default)
        {
            _settings ??= _tree.Values.Where(x => x.Type == "Settings").ToArray();
            if (_settingsIndex >= _settings.Length)
                return Task.FromResult(false);
            Content = _settings[_settingsIndex++].Clone();
            RelativePath = ContentPath.GetRelativePath(Content.Path, RootPath);
            return Task.FromResult(true);
        }

        private int _aspectIndex;
        private ContentNode[] _aspects;
        public Task<bool> ReadAspectsAsync(CancellationToken cancel = default)
        {
            _aspects ??= _tree.Values.Where(x => x.Type == "Aspect").ToArray();
            if (_aspectIndex >= _aspects.Length)
                return Task.FromResult(false);
            Content = _aspects[_aspectIndex++].Clone();
            RelativePath = ContentPath.GetRelativePath(Content.Path, RootPath);
            return Task.FromResult(true);
        }

        private ContentNode[] _currentBlock;
        private int _currentBlockIndex;
        public Task<bool> ReadAllAsync(CancellationToken cancel = default)
        {
            //UNDONE: Implement ReadAllAsync methods well.

            //TODO: Raise performance: read the next block (background)
            if (_currentBlock == null || _currentBlockIndex >= _currentBlock.Length)
            {
                _currentBlock = QueryBlock(RootPath, _blockIndex * _blockSize, _blockSize);
                _blockIndex++;
                _currentBlockIndex = 0;
                if (_currentBlock == null || _currentBlock.Length == 0)
                    return Task.FromResult(false);
            }

            Content = _currentBlock[_currentBlockIndex++].Clone();
            RelativePath = ContentPath.GetRelativePath(Content.Path, RootPath);

            return Task.FromResult(true);
        }

        public void SetReferenceUpdateTasks(IEnumerable<TransferTask> tasks, int taskCount) { }
        public Task<bool> ReadByReferenceUpdateTasksAsync(CancellationToken cancel) { return Task.FromResult(false); }

        private ContentNode[] QueryBlock(string rootPath, int skip, int top)
        {
            var rootPathTrailing = rootPath + "/";
            var contents = _tree.Keys
                .Where(x => x.StartsWith(rootPathTrailing) || x == rootPath)
                .Where(x => !x.StartsWith("/Root/System/Schema/ContentTypes/"))
                .Where(x => !x.StartsWith("/Root/System/Schema/Aspects/"))
                .Where(x => !x.StartsWith("/Root/System/Settings/"))
                .OrderBy(x => x)
                .Skip(skip)
                .Take(top)
                .Select(p => _tree[p])
                .ToArray();

            return contents;
        }
    }
}
