using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SenseNet.IO.Implementations
{
    
    public class FsReader : IContentReader
    {
        private string _fsRootPath;
        private FsContent _content; // Current IContent

        public string RootPath { get; }
        public int EstimatedCount { get; private set; }
        public IContent Content => _content;
        public string RelativePath { get; private set; }

        /// <summary>
        /// Initializes an FsReader instance.
        /// </summary>
        /// <param name="fsRootPath">Parent of "/Root" of repository.</param>
        /// <param name="rootPath">Repository path under the <paramref name="fsPath"/>.</param>
        public FsReader(string fsRootPath, [NotNull] string rootPath)
        {
            _fsRootPath = fsRootPath;
            RootPath = rootPath;
        }

        private int _contentIndex;
        private List<FsContent> _contents;
        public Task<bool> ReadAsync(CancellationToken cancel = default)
        {
            if (_contents == null)
            {
                var contents = new List<FsContent>();
                var fsRootPath = RootPath == null
                    ? _fsRootPath
                    : Path.Combine(_fsRootPath, RootPath.TrimStart('/'));
                var rootContent = GetRootContent(fsRootPath, RootPath);
                contents.Add(rootContent);
                ReadContents(fsRootPath, rootContent, contents);
                EstimatedCount = contents.Count;
                _contents = contents;
            }

            if (_contentIndex < _contents.Count)
            {
                _content = _contents[_contentIndex++];
                _content.InitializeMetadata();

                RelativePath = _content.Path;

                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        private FsContent GetRootContent(string fsRootPath, string repoPath)
        {
            var contentName = Path.GetFileName(fsRootPath);
            var metaFilePath = fsRootPath + ".Content";
            if (!IsFileExists(metaFilePath))
                metaFilePath = null;
            var contentIsDirectory = IsDirectoryExists(fsRootPath);

            return CreateFsContent(contentName, metaFilePath, contentIsDirectory, null);
        }

        private void ReadContents(string currentPath, FsContent currentContent, List<FsContent> container)
        {
            var dirs = GetFsDirectories(currentPath).ToList();
            var filePaths = GetFsFiles(currentPath).ToList();
            var localContents = new List<FsContent>();
            FsContent content;
            foreach (var directoryPath in GetFsDirectories(currentPath))
            {
                // get metaFile's path if exists or null
                var metaFilePath = directoryPath + ".Content";
                if (!IsFileExists(metaFilePath))
                    metaFilePath = null;
                else
                    filePaths.Remove(metaFilePath);

                // create current content and add to container
                content = CreateFsContent(Path.GetFileName(directoryPath), metaFilePath, true, currentContent);
                container.Add(content);
                localContents.Add(content);

                // recursion
                ReadContents(directoryPath, content, container);
            }

            // get metaFile leaves and add to container
            var metaFilePaths = filePaths
                .Where(p => p.EndsWith(".content", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            foreach (var metaFilePath in metaFilePaths)
            {
                var name = Path.GetFileNameWithoutExtension(metaFilePath);
                var ext = Path.GetExtension(metaFilePath);

                content = CreateFsContent(name, metaFilePath, false, currentContent);
                container.Add(content);
                localContents.Add(content);
            }

            // preprocess attachments if the content has metaFile.
            var attachmentPaths = filePaths.Except(metaFilePaths).Select(x=>x.ToLowerInvariant()).ToList();
            var attachmentNames = localContents.SelectMany(x => x.GetPreprocessedAttachmentNames());
            foreach (var attachmentName in attachmentNames)
                attachmentPaths.Remove(Path.Combine(currentPath, attachmentName).ToLowerInvariant());

            // add contents by files without metaFile.
            foreach (var attachmentPath in attachmentPaths)
            {
                var name = Path.GetFileName(attachmentPath);
                content = CreateFsContent(name, null, false, currentContent, attachmentPath);
                container.Add(content);
            }

        }

        private void ReadAllPaths(string currentPath, List<string> container)
        {
            var dirs = GetFsDirectories(currentPath);
            foreach (var directoryPath in dirs)
            {
                container.Add(directoryPath);
                ReadAllPaths(directoryPath, container);
            }
            container.AddRange(GetFsFiles(currentPath));
        }

        /* ========================================================================== TESTABILITY */

        protected virtual FsContent CreateFsContent(string name, string metaFilePath, bool isDirectory,
            FsContent parent, string defaultAttachmentPath = null)
        {
            return new FsContent(name, metaFilePath, isDirectory, parent, defaultAttachmentPath);
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
