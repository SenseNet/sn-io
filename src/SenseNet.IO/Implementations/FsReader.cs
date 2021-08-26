using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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

        public Task<bool> ReadContentTypesAsync(CancellationToken cancel = default)
        {
            //UNDONE: Implement ReadContentTypesAsync
            return Task.FromResult(false);
        }
        public Task<bool> ReadSettingsAsync(CancellationToken cancel = default)
        {
            //UNDONE: Implement ReadSettingsAsync
            return Task.FromResult(false);
        }
        public Task<bool> ReadAspectsAsync(CancellationToken cancel = default)
        {
            //UNDONE: Implement ReadAspectsAsync
            return Task.FromResult(false);
        }

        private class Level
        {
            public FsContent CurrentContent => Contents[Index];
            public int Index { get; set; }
            public FsContent[] Contents { get; set; }
        }
        public Task<bool> ReadAllAsync(CancellationToken cancel = default)
        {
            var fsRootPath = RootPath == null
                ? _fsRootDirectory
                : Path.Combine(_fsRootDirectory, RootPath.TrimStart('/'));
            _fsRootPath = Path.GetFullPath(fsRootPath); // normalize path separators

            return ReadTreeAsync(cancel);
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

        private readonly Stack<Level> _levels = new Stack<Level>();
        private Task<bool> ReadTreeAsync(CancellationToken cancel)
        {
            if (Content == null)
                return Task.FromResult(MoveToFirst());
            if (MoveToFirstChild())
                return Task.FromResult(true);
            if (MoveToNextSibling())
                return Task.FromResult(true);
            while (true)
            {
                if (MoveToParent())
                    if (MoveToNextSibling())
                        return Task.FromResult(true);
                if (_levels.Count == 0)
                    break;
            }
            return Task.FromResult(false);
        }

        private void SetCurrentContent()
        {
            var level = _levels.Peek();
            _content = level.Contents[level.Index];
        }
        private bool MoveToFirst()
        {
            var rootContent = GetRootContent(_fsRootPath);
            if (rootContent == null)
                return false;
            var level = new Level {Contents = new[] {rootContent}};
            _levels.Push(level);
            SetCurrentContent();
            return true;
        }
        private bool MoveToParent()
        {
            if (_levels.Count == 0)
                return false;
            _levels.Pop();
            if (_levels.Count == 0)
                return false;
            SetCurrentContent();
            return true;
        }
        private bool MoveToNextSibling()
        {
            var level = _levels.Peek();
            var index = level.Index + 1;
            if (index >= level.Contents.Length)
                return false;
            level.Index = index;
            SetCurrentContent();
            return true;
        }
        private bool MoveToFirstChild()
        {
            var contents = ReadChildren(_levels.Peek().CurrentContent);
            if (contents.Length == 0)
                return false;
            var level = new Level { Contents = contents };
            _levels.Push(level);
            SetCurrentContent();
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

                content = CreateFsContent(name, GetPath(name), null, false);
                content.InitializeMetadata();
                localContents.Add(content);
                container.Add(content);
            }

            return container.ToArray();
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
            return Directory.GetDirectories(fsDirectoryPath);
        }
        protected virtual string[] GetFsFiles(string fsDirectoryPath)
        {
            return Directory.GetFiles(fsDirectoryPath);
        }

    }
}
