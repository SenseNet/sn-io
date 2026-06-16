using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SenseNet.Tools.Configuration;

namespace SenseNet.IO.Implementations
{
    [OptionsClass(sectionName: "fsXmlReader")]
    public class FsXmlReaderArgs
    {
        /// <summary>
        /// Source file system folder path containing old SenseNet 6 XML exports.
        /// </summary>
        public string Path { get; set; }
        /// <summary>
        /// A string array containing relative paths of the skipped subtrees.
        /// </summary>
        public string[] Skip { get; set; }
        /// <summary>
        /// A string array containing field names to exclude during reading.
        /// </summary>
        public string[] ExcludedFields { get; set; }

        internal FsXmlReaderArgs Clone()
        {
            return new FsXmlReaderArgs
            {
                Path = Path,
                Skip = Skip,
                ExcludedFields = ExcludedFields,
            };
        }
    }

    /// <summary>
    /// Reads old SenseNet 6 XML-based file system exports.
    /// The XML metafiles have a .Content extension with XML content in the format:
    /// &lt;ContentMetaData&gt;
    ///   &lt;ContentType&gt;...&lt;/ContentType&gt;
    ///   &lt;ContentName&gt;...&lt;/ContentName&gt;
    ///   &lt;Fields&gt;...&lt;/Fields&gt;
    /// &lt;/ContentMetaData&gt;
    /// </summary>
    public class FsXmlReader : IFilesystemReader
    {
        // IFilesystemReader requires FsReaderArgs, so we adapt our args
        public FsReaderArgs ReaderOptions { get; }

        private readonly FsXmlReaderArgs _args;
        private FsXmlContent _content;

        public string RootName => ContentPath.GetName(_args.Path);
        public int EstimatedCount { get; private set; }
        public IContent Content => _content;
        public string RelativePath => _content?.Path;

        private readonly ILogger<FsXmlReader> _logger;

        public FsXmlReader(IOptions<FsXmlReaderArgs> args, ILogger<FsXmlReader> logger)
        {
            if (args?.Value == null)
                throw new ArgumentNullException(nameof(args));
            _args = args.Value.Clone();

            if (!string.IsNullOrEmpty(_args.Path))
                _args.Path = Path.GetFullPath(_args.Path);

            _logger = logger;

            // Adapt to FsReaderArgs for IFilesystemReader compatibility
            ReaderOptions = new FsReaderArgs { Path = _args.Path, Skip = _args.Skip };
        }

        private bool _initialized;
        public virtual Task InitializeAsync()
        {
            if (_initialized)
                return Task.CompletedTask;
            _initialized = true;

            if (_args.Path == null)
                throw new ArgumentException("FsXmlReader: empty root path.");

            if (_args.Skip != null)
                foreach (var item in _args.Skip)
                    SkipSubtree(item);

            EstimatedCount = Directory.Exists(_args.Path) || File.Exists(_args.Path) || File.Exists(_args.Path + ".Content") ? 1 : 0;
            Task.Run(() => GetContentCount(_args.Path));

            return Task.CompletedTask;
        }

        private TreeState _mainState;
        private readonly Dictionary<string, TreeState> _treeStates = new();

        public async Task<bool> ReadSubTreeAsync(string relativePath, CancellationToken cancel = default)
        {
            await InitializeAsync().ConfigureAwait(false);

            if (!_treeStates.TryGetValue(relativePath, out var treeState))
            {
                var absPath = Path.GetFullPath(Path.Combine(_args.Path, relativePath));
                treeState = new TreeState { FsRootPath = absPath };
                _treeStates.Add(relativePath, treeState);
            }

            return ReadTree(treeState);
        }

        public async Task<bool> ReadAllAsync(string[] contentsWithoutChildren, CancellationToken cancel = default)
        {
            if (_mainState == null)
            {
                await InitializeAsync().ConfigureAwait(false);
                _mainState = new TreeState
                {
                    FsRootPath = _args.Path,
                    ContentsWithoutChildren = contentsWithoutChildren ?? Array.Empty<string>()
                };
            }

            return ReadTree(_mainState);
        }

        private IEnumerator<TransferTask> _referenceUpdateTasksEnumerator;
        public void SetReferenceUpdateTasks(IEnumerable<TransferTask> tasks, int taskCount)
        {
            _referenceUpdateTasksEnumerator = tasks.GetEnumerator();
        }

        public Task<bool> ReadByReferenceUpdateTasksAsync(CancellationToken cancel)
        {
            if (!_referenceUpdateTasksEnumerator.MoveNext())
                return Task.FromResult(false);

            var task = _referenceUpdateTasksEnumerator.Current;
            var relativePath = task.ReaderPath;
            var metaFilePath = Path.GetFullPath(Path.Combine(_args.Path, relativePath)) + ".Content";
            var name = ContentPath.GetName("/" + relativePath);
            var content = new FsXmlContent(name, relativePath, metaFilePath, false, false, _logger);
            content.InitializeMetadata(task.BrokenReferences, !task.RetryPermissions);
            _content = content;

            return Task.FromResult(true);
        }

        private readonly List<string> _cutoffs = new();
        public void SkipSubtree(string relativePath)
        {
            _cutoffs.Add(relativePath);
        }

        private bool NeedToSkip(string path)
        {
            var relativePath = ContentPath.GetRelativePath(path, _args.Path);
            return _cutoffs.Any(relativePath.StartsWith);
        }

        // ============================== Tree traversal (same pattern as FsReader)

        private class TreeState
        {
            public string FsRootPath { get; set; }
            public string[] ContentsWithoutChildren { get; set; } = Array.Empty<string>();
            public Stack<Level> Levels { get; } = new();
        }

        private class Level
        {
            public FsXmlContent CurrentContent => Contents[Index];
            public int Index { get; set; }
            public FsXmlContent[] Contents { get; set; }
        }

        private bool ReadTree(TreeState state)
        {
            if (state.Levels.Count == 0)
                return MoveToFirst(state);

            if (!NeedToSkip(Content.Path))
            {
                if (MoveToFirstChild(state))
                    return true;
            }

            if (MoveToNextSibling(state))
                return true;

            while (true)
            {
                if (MoveToParent(state))
                    if (MoveToNextSibling(state))
                        return true;
                if (state.Levels.Count == 0)
                    break;
            }
            return false;
        }

        private void SetCurrentContent(Stack<Level> levels)
        {
            var level = levels.Peek();
            _content = level.Contents[level.Index];
        }

        private bool MoveToFirst(TreeState state)
        {
            var rootContent = GetRootContent(state.FsRootPath);
            if (rootContent == null)
                return false;
            var level = new Level { Contents = new[] { rootContent } };
            state.Levels.Push(level);
            SetCurrentContent(state.Levels);
            return true;
        }

        private bool MoveToParent(TreeState state)
        {
            if (state.Levels.Count == 0)
                return false;
            state.Levels.Pop();
            if (state.Levels.Count == 0)
                return false;
            SetCurrentContent(state.Levels);
            return true;
        }

        private bool MoveToNextSibling(TreeState state)
        {
            var level = state.Levels.Peek();
            var index = level.Index + 1;
            if (index >= level.Contents.Length)
                return false;
            level.Index = index;
            SetCurrentContent(state.Levels);
            return true;
        }

        private bool MoveToFirstChild(TreeState state)
        {
            var currentContent = state.Levels.Peek().CurrentContent;
            if (state.ContentsWithoutChildren.Contains(currentContent.Path, StringComparer.OrdinalIgnoreCase))
                return false;
            var contents = ReadChildren(currentContent);
            if (contents.Length == 0)
                return false;
            var level = new Level { Contents = contents };
            state.Levels.Push(level);
            SetCurrentContent(state.Levels);
            return true;
        }

        private FsXmlContent GetRootContent(string fsRootPath)
        {
            var contentName = Path.GetFileName(fsRootPath);
            var metaFilePath = fsRootPath + ".Content";
            if (!File.Exists(metaFilePath))
                metaFilePath = null;
            var contentIsDirectory = Directory.Exists(fsRootPath);
            if (!contentIsDirectory && metaFilePath == null)
                return null;
            var relativePath = fsRootPath.Length > _args.Path.Length
                ? fsRootPath.Remove(0, _args.Path.Length).Replace('\\', '/').TrimStart('/')
                : string.Empty;
            var content = new FsXmlContent(contentName, relativePath, metaFilePath, contentIsDirectory, _cutoffs.Contains(relativePath), _logger);
            content.GetPreprocessedAttachmentNames();
            content.InitializeMetadata(_args.ExcludedFields);
            return content;
        }

        private FsXmlContent[] ReadChildren(FsXmlContent parentContent)
        {
            string GetPath(string name) => ContentPath.Combine(parentContent.Path, name);

            if (parentContent.CutOff)
                return Array.Empty<FsXmlContent>();
            if (!parentContent.IsDirectory)
                return Array.Empty<FsXmlContent>();

            var fsPath = string.IsNullOrEmpty(parentContent.Path)
                ? _args.Path
                : Path.GetFullPath(Path.Combine(_args.Path, parentContent.Path));

            var dirs = Directory.Exists(fsPath) ? Directory.GetDirectories(fsPath).OrderBy(x => x).ToList() : new List<string>();
            var filePaths = Directory.Exists(fsPath) ? Directory.GetFiles(fsPath).OrderBy(x => x).ToList() : new List<string>();
            var localContents = new List<FsXmlContent>();
            var container = new List<FsXmlContent>();

            foreach (var directoryPath in dirs)
            {
                var metaFilePath = directoryPath + ".Content";
                if (!File.Exists(metaFilePath))
                    metaFilePath = null;
                else
                    filePaths.Remove(metaFilePath);

                var name = Path.GetFileName(directoryPath);
                var content = new FsXmlContent(name, GetPath(name), metaFilePath, true, _cutoffs.Contains(GetPath(name)), _logger);
                content.InitializeMetadata(_args.ExcludedFields);
                localContents.Add(content);
                container.Add(content);
            }

            var metaFilePaths = filePaths
                .Where(p => p.EndsWith(".Content", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            foreach (var metaFilePath in metaFilePaths)
            {
                var name = Path.GetFileNameWithoutExtension(metaFilePath);
                var content = new FsXmlContent(name, GetPath(name), metaFilePath, false, false, _logger);
                content.InitializeMetadata(_args.ExcludedFields);
                localContents.Add(content);
                container.Add(content);
            }

            var attachmentPaths = filePaths.Except(metaFilePaths).Select(x => x.ToLowerInvariant()).ToList();
            var attachmentNames = localContents.SelectMany(x => x.GetPreprocessedAttachmentNames());
            foreach (var attachmentName in attachmentNames)
                attachmentPaths.Remove(Path.Combine(fsPath, attachmentName).ToLowerInvariant());

            foreach (var attachmentPath in attachmentPaths)
            {
                var name = Path.GetFileName(attachmentPath);
                var content = new FsXmlContent(name, GetPath(name), null, false, false, _logger, attachmentPath);
                content.InitializeMetadata(_args.ExcludedFields);
                localContents.Add(content);
                container.Add(content);
            }

            return container.ToArray();
        }

        private void GetContentCount(string fsPath)
        {
            if (!Directory.Exists(fsPath))
                return;

            var dirs = Directory.GetDirectories(fsPath).OrderBy(x => x).ToList();
            var filePaths = Directory.GetFiles(fsPath).OrderBy(x => x).ToList();
            var localContents = new List<FsXmlContent>();

            foreach (var directoryPath in dirs)
            {
                var metaFilePath = directoryPath + ".Content";
                if (!File.Exists(metaFilePath))
                    metaFilePath = null;
                else
                    filePaths.Remove(metaFilePath);

                var name = Path.GetFileName(directoryPath);
                var content = new FsXmlContent(name, string.Empty, metaFilePath, true, false, _logger);
                localContents.Add(content);
            }

            var metaFilePaths = filePaths
                .Where(p => p.EndsWith(".Content", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            foreach (var metaFilePath in metaFilePaths)
            {
                var name = Path.GetFileNameWithoutExtension(metaFilePath);
                var content = new FsXmlContent(name, string.Empty, metaFilePath, false, false, _logger);
                localContents.Add(content);
            }

            var attachmentPaths = filePaths.Except(metaFilePaths).Select(x => x.ToLowerInvariant()).ToList();
            var attachmentNames = localContents.SelectMany(x => x.GetPreprocessedAttachmentNames());
            foreach (var attachmentName in attachmentNames)
                attachmentPaths.Remove(Path.Combine(fsPath, attachmentName).ToLowerInvariant());

            foreach (var attachmentPath in attachmentPaths)
            {
                localContents.Add(new FsXmlContent(Path.GetFileName(attachmentPath), string.Empty, null, false, false, _logger));
            }

            EstimatedCount += localContents.Count;

            foreach (var subDir in dirs)
                GetContentCount(subDir);
        }
    }
}
