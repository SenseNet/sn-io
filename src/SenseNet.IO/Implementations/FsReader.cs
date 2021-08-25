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
        /// <param name="rootPath">Repository path under the <paramref name="fsRootPath"/>.</param>
        public FsReader(string fsRootPath, [NotNull] string rootPath)
        {
            _fsRootPath = fsRootPath;
            RootPath = rootPath;
        }

        public Task<bool> ReadContentTypesAsync(CancellationToken cancel = default)
        {
            //UNDONE: Implement ReadContentTypesAsync()
            throw new NotImplementedException();
        }
        public Task<bool> ReadSettingsAsync(CancellationToken cancel = default)
        {
            //UNDONE: Implement ReadSettingsAsync()
            throw new NotImplementedException();
        }
        public Task<bool> ReadAspectsAsync(CancellationToken cancel = default)
        {
            //UNDONE: Implement ReadAspectsAsync()
            throw new NotImplementedException();
        }
        private int _contentIndex;
        private List<FsContent> _contents;
        public Task<bool> ReadAllAsync(CancellationToken cancel = default)
        {
            //UNDONE: Implement ReadAllAsync()
            throw new NotImplementedException();
            if (_contents == null)
            {
                var contents = new List<FsContent>();
                var fsRootPath = RootPath == null
                    ? _fsRootPath
                    : Path.Combine(_fsRootPath, RootPath.TrimStart('/'));
                fsRootPath = Path.GetFullPath(fsRootPath); // normalize path separators

                var rootContent = GetRootContent(fsRootPath);
                contents.Add(rootContent);

                ReadPriorityContents(fsRootPath, rootContent, contents);
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

        private FsContent GetRootContent(string fsRootPath)
        {
            var contentName = Path.GetFileName(fsRootPath);
            var metaFilePath = fsRootPath + ".Content";
            if (!IsFileExists(metaFilePath))
                metaFilePath = null;
            var contentIsDirectory = IsDirectoryExists(fsRootPath);

            return CreateFsContent(contentName, metaFilePath, contentIsDirectory, null);
        }

        private static readonly string[] PriorityNames = new[] {"System", "Schema", "ContentTypes", "Aspects"};
        private void ReadPriorityContents(string currentPath, FsContent currentContent, List<FsContent> container)
        {
            var dirs = GetFsDirectories(currentPath)
                .Where(x => PriorityNames.Contains(Path.GetFileName(x), StringComparer.OrdinalIgnoreCase))
                .OrderByDescending(x => x) // ensures that "ContentTypes" precedes "Aspects"
                .ToList();
            var filePaths = GetFsFiles(currentPath)
                .Where(x => PriorityNames.Contains(Path.GetFileNameWithoutExtension(x), StringComparer.OrdinalIgnoreCase))
                .ToList();
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
                content = CreateFsContent(Path.GetFileName(directoryPath), metaFilePath, true, currentContent);
                container.Add(content);
                localContents.Add(content);

                // recursion
                if (content.Name.Equals("ContentTypes", StringComparison.OrdinalIgnoreCase))
                    ReadContents(directoryPath, content, container);
                else if(content.Name.Equals("Aspects", StringComparison.OrdinalIgnoreCase))
                    ReadContents(directoryPath, content, container);
                else
                    ReadPriorityContents(directoryPath, content, container);
            }

            // get metaFile leaves and add to container
            var metaFilePaths = filePaths
                .Where(p => p.EndsWith(".content", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            foreach (var metaFilePath in metaFilePaths)
            {
                var name = Path.GetFileNameWithoutExtension(metaFilePath);

                content = CreateFsContent(name, metaFilePath, false, currentContent);
                container.Add(content);
                localContents.Add(content);
            }

            // preprocess attachments if the content has metaFile.
            var attachmentPaths = filePaths.Except(metaFilePaths).Select(x => x.ToLowerInvariant()).ToList();
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

        private void ReadContents(string currentPath, FsContent currentContent, List<FsContent> container)
        {
            var dirs = GetFsDirectories(currentPath).OrderBy(x => x).ToList();
            var filePaths = GetFsFiles(currentPath).OrderBy(x => x).ToList();
            var localContents = new List<FsContent>();
            FsContent content;
            bool isPriorityContent;
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
                content = currentContent.Children.FirstOrDefault(x => x.Name == name);
                isPriorityContent = content != null;
                if(!isPriorityContent)
                    content = CreateFsContent(name, metaFilePath, true, currentContent);
                localContents.Add(content);
                if(!isPriorityContent)
                    container.Add(content);

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

                content = currentContent.Children.FirstOrDefault(x => x.Name == name);
                isPriorityContent = content != null;
                if (!isPriorityContent)
                    content = CreateFsContent(name, metaFilePath, false, currentContent);
                localContents.Add(content);
                if (!isPriorityContent)
                    container.Add(content);
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

                content = currentContent.Children.FirstOrDefault(x => x.Name == name);
                isPriorityContent = content != null;
                if (!isPriorityContent)
                    content = CreateFsContent(name, null, false, currentContent);
                localContents.Add(content);
                if (!isPriorityContent)
                    container.Add(content);
            }
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
