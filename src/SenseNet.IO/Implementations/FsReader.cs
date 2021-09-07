using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using IdentityModel;

namespace SenseNet.IO.Implementations
{
    public class FsReader : IContentReader
    {
        private readonly string _fsRootDirectory; // Constructor param
        private FsContent _content; // Current IContent
        private string _fsRootPath;

        public string RootPath { get; }
        public int EstimatedCount { get; private set; }
        public IContent Content => _content;
        public string RelativePath => _content.Path;

        /// <summary>
        /// Initializes an FsReader instance.
        /// </summary>
        /// <param name="fsRootPath">Parent of "/Root" of repository.</param>
        /// <param name="rootPath">Repository path under the <paramref name="fsRootPath"/>.</param>
        public FsReader(string fsRootPath, [NotNull] string rootPath)
        {
            _fsRootDirectory = fsRootPath;
            RootPath = rootPath;
        }

        private void Initialize()
        {
            if (_fsRootPath != null)
                return;

            var fsRootPath = RootPath == null
                ? _fsRootDirectory
                : Path.Combine(_fsRootDirectory, RootPath.TrimStart('/'));
            _fsRootPath = Path.GetFullPath(fsRootPath); // normalize path separators

            EstimatedCount = 1;
            Task.Run(() => GetContentCount(_fsRootPath));
        }

        private List<FsContent> _contentTypeContents;
        private int _contentTypeContentsIndex;
        public Task<bool> ReadContentTypesAsync(CancellationToken cancel = default)
        {
            if (_contentTypeContents == null)
            {
                Initialize();

                if (!RootPath.Equals("/Root", StringComparison.OrdinalIgnoreCase) && 
                    !RootPath.Equals("/Root/System", StringComparison.OrdinalIgnoreCase) &&
                    !RootPath.Equals("/Root/System/Schema", StringComparison.OrdinalIgnoreCase) &&
                    !RootPath.Equals("/Root/System/Schema/ContentTypes", StringComparison.OrdinalIgnoreCase)
                    )
                    return Task.FromResult(false);

                var ctdRootRelPath = "/Root/System/Schema/ContentTypes".Substring(RootPath.Length).TrimStart('/');
                var ctdRootContent = CreateFsContent("ContentTypes", ctdRootRelPath, null, true);
                _contentTypeContents = new List<FsContent>();
                ReadSubTree(ctdRootContent, _contentTypeContents);
            }

            if (_contentTypeContentsIndex >= _contentTypeContents.Count)
                return Task.FromResult(false);

            _content = _contentTypeContents[_contentTypeContentsIndex++];

            return Task.FromResult(true);
        }

        private List<FsContent> _settingsContents;
        private int _settingsContentsIndex;
        public Task<bool> ReadSettingsAsync(CancellationToken cancel = default)
        {
            if (_settingsContents == null)
            {
                Initialize();

                if (!RootPath.Equals("/Root", StringComparison.OrdinalIgnoreCase) &&
                    !RootPath.Equals("/Root/System", StringComparison.OrdinalIgnoreCase) &&
                    !RootPath.Equals("/Root/System/Settings", StringComparison.OrdinalIgnoreCase))
                    return Task.FromResult(false);

                var settingsRootRelPath = "/Root/System/Settings".Substring(RootPath.Length).TrimStart('/');
                var settingsRootContent = CreateFsContent("Settings", settingsRootRelPath, null, true);
                _settingsContents = new List<FsContent>();
                ReadSubTree(settingsRootContent, _settingsContents);
            }

            if (_settingsContentsIndex >= _settingsContents.Count)
                return Task.FromResult(false);

            _content = _settingsContents[_settingsContentsIndex++];

            return Task.FromResult(true);
        }

        private List<FsContent> _aspectContents;
        private int _aspectContentsIndex;
        public Task<bool> ReadAspectsAsync(CancellationToken cancel = default)
        {
            if (_aspectContents == null)
            {
                Initialize();

                if (!RootPath.Equals("/Root", StringComparison.OrdinalIgnoreCase) &&
                    !RootPath.Equals("/Root/System", StringComparison.OrdinalIgnoreCase) &&
                    !RootPath.Equals("/Root/System/Schema", StringComparison.OrdinalIgnoreCase) &&
                    !RootPath.Equals("/Root/System/Schema/Aspects", StringComparison.OrdinalIgnoreCase)
                )
                    return Task.FromResult(false);

                var ctdRootRelPath = "/Root/System/Schema/Aspects".Substring(RootPath.Length).TrimStart('/');
                var ctdRootContent = CreateFsContent("Aspects", ctdRootRelPath, null, true);
                _aspectContents = new List<FsContent>();
                ReadSubTree(ctdRootContent, _aspectContents);
            }

            if (_aspectContentsIndex >= _aspectContents.Count)
                return Task.FromResult(false);

            _content = _aspectContents[_aspectContentsIndex++];

            return Task.FromResult(true);
        }

        public Task<bool> ReadSubTreeAsync(string relativePath, CancellationToken cancel = default)
        {
            //UNDONE:!!!!!!!!!!!!!! ReadSubTreeAsync is not implemented
            throw new NotImplementedException();
        }

        private void ReadSubTree(FsContent parentContent, List<FsContent> container)
        {
            var children = ReadChildren(parentContent);
            container.AddRange(children);
            foreach (var child in children)
                ReadSubTree(child, container);
        }

        private class Level
        {
            public FsContent CurrentContent => Contents[Index];
            public int Index { get; set; }
            public FsContent[] Contents { get; set; }
        }
        public Task<bool> ReadAllAsync(string[] contentsWithoutChildren, CancellationToken cancel = default)
        {
            //UNDONE:!!!!!!!!! Process "contentsWithoutChildren" parameter
            Initialize();

            bool goAhead;
            // ReSharper disable once AssignmentInConditionalExpression
            while (goAhead = ReadTree())
            {
                if (!(IsContentType(Content) || IsAspect(Content) || IsSettings(Content)))
                    break;
            }

            return Task.FromResult(goAhead);
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
            var repositoryPath = ContentPath.Combine(RootPath, relativePath);
            var metaFilePath = Path.GetFullPath(Path.Combine(_fsRootPath, relativePath)) + ".Content";
            var name = ContentPath.GetName(repositoryPath);
            var content = new FsContent(name, relativePath, metaFilePath, false);
            content.InitializeMetadata(task.BrokenReferences, task.RetryPermissions);
            _content = content;

            return Task.FromResult(true);
        }

        private bool IsContentType(IContent content)
        {
            return ContentPath.Combine(RootPath, content.Path)
                .StartsWith("/Root/System/Schema/ContentTypes/", StringComparison.OrdinalIgnoreCase);
        }
        private bool IsAspect(IContent content)
        {
            return ContentPath.Combine(RootPath, content.Path)
                .StartsWith("/Root/System/Schema/Aspects/", StringComparison.OrdinalIgnoreCase);
        }
        private bool IsSettings(IContent content)
        {
            return ContentPath.Combine(RootPath, content.Path)
                .StartsWith("/Root/System/Settings/", StringComparison.OrdinalIgnoreCase);
        }

        private FsContent GetRootContent(string fsRootPath)
        {
            var contentName = Path.GetFileName(fsRootPath);
            var metaFilePath = fsRootPath + ".Content";
            if (!IsFileExists(metaFilePath))
                metaFilePath = null;
            var contentIsDirectory = IsDirectoryExists(fsRootPath);

            var content = CreateFsContent(contentName, string.Empty, metaFilePath, contentIsDirectory, null);
            content.InitializeMetadata();
            return content;
        }

        private Stack<Level> _levels;
        private bool ReadTree()
        {
            if (_levels == null)
            {
                _levels = new Stack<Level>();
                return MoveToFirst();
            }
            if (MoveToFirstChild())
                return true;
            if (MoveToNextSibling())
                return true;
            while (true)
            {
                if (MoveToParent())
                    if (MoveToNextSibling())
                        return true;
                if (_levels.Count == 0)
                    break;
            }
            return false;
        }

        private void SetCurrentContent(Stack<Level> levels)
        {
            var level = levels.Peek();
            _content = level.Contents[level.Index];
        }
        private bool MoveToFirst()
        {
            var rootContent = GetRootContent(_fsRootPath);
            if (rootContent == null)
                return false;
            var level = new Level {Contents = new[] {rootContent}};
            _levels.Push(level);
            SetCurrentContent(_levels);
            return true;
        }
        private bool MoveToParent()
        {
            if (_levels.Count == 0)
                return false;
            _levels.Pop();
            if (_levels.Count == 0)
                return false;
            SetCurrentContent(_levels);
            return true;
        }
        private bool MoveToNextSibling()
        {
            var level = _levels.Peek();
            var index = level.Index + 1;
            if (index >= level.Contents.Length)
                return false;
            level.Index = index;
            SetCurrentContent(_levels);
            return true;
        }
        private bool MoveToFirstChild()
        {
            var contents = ReadChildren(_levels.Peek().CurrentContent);
            if (contents.Length == 0)
                return false;
            var level = new Level { Contents = contents };
            _levels.Push(level);
            SetCurrentContent(_levels);
            return true;

        }
        private FsContent[] ReadChildren(FsContent parentContent)
        {
            string GetPath(string name) { return ContentPath.Combine(parentContent.Path, name); }

            if (!parentContent.IsDirectory)
                return new FsContent[0];
            var fsPath = Path.GetFullPath(Path.Combine(_fsRootPath, parentContent.Path));

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
            return new FsContent(name, relativePath, metaFilePath, isDirectory, defaultAttachmentPath);
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
