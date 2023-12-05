using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using SenseNet.Tools.Configuration;

namespace SenseNet.IO.Implementations
{
    [OptionsClass(sectionName: "fsReader")]
    public class FsReaderArgs
    {
        /// <summary>
        /// Source file system folder path.
        /// </summary>
        public string Path { get; set; }
        /// <summary>
        /// A string array containing relative paths of the skipped subtrees.
        /// </summary>
        public string[] Skip { get; set; }

        internal FsReaderArgs Clone()
        {
            return new FsReaderArgs
            {
                Path = Path,
                Skip = Skip,
            };
        }
    }

    public class FsReader : IFilesystemReader
    {
        public FsReaderArgs Args { get; }
        private FsContent _content; // Current IContent
        public string ReaderRootPath => Args.Path;

        public string RootName => ContentPath.GetName(Args.Path);
        public int EstimatedCount { get; private set; }
        public IContent Content => _content;
        public string RelativePath => _content?.Path;

        public FsReaderArgs ReaderOptions => Args;

        public FsReader(IOptions<FsReaderArgs> args)
        {
            if (args?.Value == null)
                throw new ArgumentNullException(nameof(args));
            Args = args.Value.Clone();

            // deal with relative paths like '..\\..\\MyFolder'
            if (!string.IsNullOrEmpty(Args.Path))
                Args.Path = Path.GetFullPath(Args.Path);
        }

        private bool _initialized;
        public virtual Task InitializeAsync()
        {
            if (_initialized)
                return Task.CompletedTask;
            _initialized = true;

            if (ReaderRootPath == null)
                throw new ArgumentException("FsReader: empty root path.");

            if(Args.Skip != null)
                foreach (var item in Args.Skip)
                    SkipSubtree(item);

            EstimatedCount = IsDirectoryExists(ReaderRootPath) || IsFileExists(ReaderRootPath) || IsFileExists(ReaderRootPath + ".Content") ? 1 : 0;
            Task.Run(() => GetContentCount(ReaderRootPath));

            return Task.CompletedTask;
        }

        private TreeState _mainState;
        private readonly Dictionary<string, TreeState> _treeStates = new Dictionary<string, TreeState>();
        public async Task<bool> ReadSubTreeAsync(string relativePath, CancellationToken cancel = default)
        {
            await InitializeAsync().ConfigureAwait(false);

            if (!_treeStates.TryGetValue(relativePath, out var treeState))
            {
                var absPath = Path.GetFullPath(Path.Combine(ReaderRootPath, relativePath));
                treeState = new TreeState{FsRootPath = absPath};
                _treeStates.Add(relativePath, treeState);
            }

            var goAhead = ReadTree(treeState);
            return goAhead;
        }

        private class TreeState
        {
            public string FsRootPath { get; set; }
            public string[] ContentsWithoutChildren { get; set; } = Array.Empty<string>();
            public Stack<Level> Levels { get; } = new Stack<Level>();
        }
        private class Level
        {
            public FsContent CurrentContent => Contents[Index];
            public int Index { get; set; }
            public FsContent[] Contents { get; set; }
        }
        public async Task<bool> ReadAllAsync(string[] contentsWithoutChildren, CancellationToken cancel = default)
        {
            if (_mainState == null)
            {
                await InitializeAsync().ConfigureAwait(false);
                _mainState = new TreeState
                {
                    FsRootPath = ReaderRootPath,
                    ContentsWithoutChildren = contentsWithoutChildren ?? Array.Empty<string>()
                };
            }

            var goAhead = ReadTree(_mainState);
            return goAhead;
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
            var repositoryPath = "/" + relativePath;
            var metaFilePath = Path.GetFullPath(Path.Combine(ReaderRootPath, relativePath)) + ".Content";
            var name = ContentPath.GetName(repositoryPath);
            var content = new FsContent(name, relativePath, metaFilePath, false, false);
            content.InitializeMetadata(task.BrokenReferences, task.RetryPermissions);
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
            var relativePath = ContentPath.GetRelativePath(path, ReaderRootPath);
            return _cutoffs.Any(relativePath.StartsWith);
        }

        private FsContent GetRootContent(string fsRootPath)
        {
            var contentName = Path.GetFileName(fsRootPath);
            var metaFilePath = fsRootPath + ".Content";
            if (!IsFileExists(metaFilePath))
                metaFilePath = null;
            var contentIsDirectory = IsDirectoryExists(fsRootPath);
            if (!contentIsDirectory && metaFilePath == null)
                return null;
            var relativePath = fsRootPath.Remove(0, ReaderRootPath.Length).Replace('\\', '/').TrimStart('/');
            var content = CreateFsContent(contentName, relativePath, metaFilePath, contentIsDirectory);
            var _ = content.GetPreprocessedAttachmentNames();
            content.InitializeMetadata();
            return content;
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
            else
            {
                int q = 1;
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
            var level = new Level {Contents = new[] {rootContent}};
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
        private FsContent[] ReadChildren(FsContent parentContent)
        {
            string GetPath(string name) { return ContentPath.Combine(parentContent.Path, name); }

            if(parentContent.CutOff)
                return Array.Empty<FsContent>();
            if (!parentContent.IsDirectory)
                return Array.Empty<FsContent>();

            var fsPath = Path.GetFullPath(Path.Combine(ReaderRootPath, parentContent.Path));
            var dirs = GetFsDirectories(fsPath).OrderBy(x => x).ToList();
            var filePaths = GetFsFiles(fsPath).OrderBy(x => x).ToList();
            var localContents = new List<FsContent>();
            var container = new List<FsContent>();
            FsContent content;
            foreach (var directoryPath in dirs)
            {
                // get metaFile's path if exists or null
                var metaFilePath = directoryPath + ".Content";
                if (!IsFileExists(metaFilePath))
                    metaFilePath = null;
                else
                    filePaths.Remove(metaFilePath);

                // create current content and add to container
                var name = Path.GetFileName(directoryPath);
                content = CreateFsContent(name, GetPath(name), metaFilePath, true);
                content.InitializeMetadata();
                localContents.Add(content);
                container.Add(content);
            }

            // get metaFile leaves and add to container
            var metaFilePaths = filePaths
                .Where(p => p.EndsWith(".content", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            foreach (var metaFilePath in metaFilePaths)
            {
                var name = Path.GetFileNameWithoutExtension(metaFilePath);

                content = CreateFsContent(name, GetPath(name), metaFilePath, false);
                content.InitializeMetadata();
                localContents.Add(content);
                container.Add(content);
            }

            // preprocess attachments if the content has metaFile.
            var attachmentPaths = filePaths.Except(metaFilePaths).Select(x => x.ToLowerInvariant()).ToList();
            var attachmentNames = localContents.SelectMany(x => x.GetPreprocessedAttachmentNames());
            foreach (var attachmentName in attachmentNames)
                attachmentPaths.Remove(Path.Combine(fsPath, attachmentName).ToLowerInvariant());

            // add contents by files without metaFile.
            foreach (var attachmentPath in attachmentPaths)
            {
                var name = Path.GetFileName(attachmentPath);

                content = CreateFsContent(name, GetPath(name), null, false, attachmentPath);
                content.InitializeMetadata();
                localContents.Add(content);
                container.Add(content);
            }

            return container.ToArray();
        }

        private void GetContentCount(string fsPath)
        {
            var dirs = GetFsDirectories(fsPath).OrderBy(x => x).ToList();
            var filePaths = GetFsFiles(fsPath).OrderBy(x => x).ToList();
            var localContents = new List<FsContent>();
            FsContent content;
            foreach (var directoryPath in dirs)
            {
                // get metaFile's path if exists or null
                var metaFilePath = directoryPath + ".Content";
                if (!IsFileExists(metaFilePath))
                    metaFilePath = null;
                else
                    filePaths.Remove(metaFilePath);

                // create current content and add to container
                var name = Path.GetFileName(directoryPath);
                content = CreateFsContent(name, string.Empty, metaFilePath, true);
                localContents.Add(content);
            }

            // get metaFile leaves and add to container
            var metaFilePaths = filePaths
                .Where(p => p.EndsWith(".content", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            foreach (var metaFilePath in metaFilePaths)
            {
                var name = Path.GetFileNameWithoutExtension(metaFilePath);

                content = CreateFsContent(name, string.Empty, metaFilePath, false);
                localContents.Add(content);
            }

            // preprocess attachments if the content has metaFile.
            var attachmentPaths = filePaths.Except(metaFilePaths).Select(x => x.ToLowerInvariant()).ToList();
            var attachmentNames = localContents.SelectMany(x => x.GetPreprocessedAttachmentNames());
            foreach (var attachmentName in attachmentNames)
                attachmentPaths.Remove(Path.Combine(fsPath, attachmentName).ToLowerInvariant());

            // add contents by files without metaFile.
            foreach (var attachmentPath in attachmentPaths)
            {
                var name = Path.GetFileName(attachmentPath);
                content = CreateFsContent(name, string.Empty, null, false);
                localContents.Add(content);
            }

            EstimatedCount += localContents.Count;

            foreach (var subDir in dirs)
                GetContentCount(subDir);
        }

        /* ========================================================================== TESTABILITY */

        protected virtual FsContent CreateFsContent(string name, string relativePath, string metaFilePath, bool isDirectory,
            string defaultAttachmentPath = null)
        {
            return new FsContent(name, relativePath, metaFilePath, isDirectory, _cutoffs.Contains(relativePath),
                defaultAttachmentPath);
        }

        protected virtual bool IsDirectoryExists(string fsPath)
        {
            return Directory.Exists(fsPath);
        }
        protected virtual bool IsFileExists(string fsPath)
        {
            return File.Exists(fsPath);
        }
        protected virtual string[] GetFsDirectories(string fsDirectoryPath)
        {
            return IsDirectoryExists(fsDirectoryPath) ? Directory.GetDirectories(fsDirectoryPath) : Array.Empty<string>();
        }
        protected virtual string[] GetFsFiles(string fsDirectoryPath)
        {
            return IsDirectoryExists(fsDirectoryPath) ? Directory.GetFiles(fsDirectoryPath) : Array.Empty<string>();
        }
    }
}
