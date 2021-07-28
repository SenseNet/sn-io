using System;
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
    internal class TestCQReader : IContentReader<ContentNode>
    {
        private readonly Dictionary<string, ContentNode> _tree;
        private readonly string _rootPath;
        private readonly int _blockSize;
        private int _blockIndex;

        public int EstimatedCount => _tree?.Count ?? 0;

        public ContentNode Content { get; private set; }

        public TestCQReader(string rootPath, int blockSize, Dictionary<string, ContentNode> tree)
        {
            _rootPath = rootPath;
            _blockSize = blockSize;
            _blockIndex = 0;
            _tree = tree;
        }

        private ContentNode[] _currentBlock;
        private int _currentBlockIndex;
        public Task<bool> ReadAsync(CancellationToken cancel = default)
        {
            if (_currentBlock == null || _currentBlockIndex >= _currentBlock.Length)
            {
                _currentBlock = QueryBlock(_rootPath, _blockIndex * _blockSize, _blockSize);
                _blockIndex++;
                _currentBlockIndex = 0;
                if (_currentBlock == null || _currentBlock.Length == 0)
                    return Task.FromResult(false);
            }

            Content = _currentBlock[_currentBlockIndex++];
            return Task.FromResult(true);
        }

        private ContentNode[] QueryBlock(string rootPath, int skip, int top)
        {
            var rootPathTrailing = rootPath + "/";
            var contents = _tree.Keys
                .Where(x => x.StartsWith(rootPathTrailing) || x == rootPath)
                .OrderBy(x => x)
                .Skip(skip)
                .Take(top)
                .Select(p=>_tree[p])
                .ToArray();

            return contents;
        }
    }
}
