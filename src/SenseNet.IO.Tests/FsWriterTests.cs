using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SenseNet.IO.Implementations;

namespace SenseNet.IO.Tests
{
    [TestClass]
    public class FsWriterTests
    {
        private class FsWriterMock : FsWriter
        {
            #region Nested classes
            public FsWriterMock(string outputDirectory, string containerPath, string rootName,
                Func<string, bool> isDirectoryExists,
                Action<string> createDirectory,
                Func<string, bool, TextWriter> createTextWriter,
                Func<string, FileMode, Stream> createBinaryStream) : base(outputDirectory, containerPath, rootName)
            {
                _isDirectoryExists = isDirectoryExists;
                _createDirectory = createDirectory;
                _createTextWriter = createTextWriter;
                _createBinaryStream = createBinaryStream;
            }

            private readonly Func<string, bool> _isDirectoryExists;
            private readonly Action<string> _createDirectory;
            private readonly Func<string, bool, TextWriter> _createTextWriter;
            private readonly Func<string, FileMode, Stream> _createBinaryStream;

            protected override bool IsDirectoryExists(string fileDir)
            {
                return _isDirectoryExists(fileDir);
            }
            protected override void CreateDirectory(string fileDir)
            {
                _createDirectory(fileDir);
            }
            protected override TextWriter CreateTextWriter(string fsPath, bool append)
            {
                return _createTextWriter(fsPath, append);
            }
            protected override Stream CreateBinaryStream(string fsPath, FileMode fileMode)
            {
                return _createBinaryStream(fsPath, fileMode);
            }
        }
        private class TestContent : IContent
        {
            private readonly Dictionary<string, object> _fields;
            private readonly Attachment[] _attachments;

            public string[] FieldNames => _fields.Keys.ToArray();

            public object this[string fieldName]
            {
                get => _fields[fieldName];
                set => _fields[fieldName] = value;
            }

            public string Name { get; }
            public string Path { get; }
            public string Type { get; }
            public PermissionInfo Permissions { get; set; }

            public TestContent(string name, string path, string type, Dictionary<string, object> fields, Attachment[] attachments)
            {
                Name = name;
                Path = path;
                Type = type;
                _fields = fields;
                _attachments = attachments;
            }

            public Task<Attachment[]> GetAttachmentsAsync()
            {
                return Task.FromResult(_attachments);
            }
        }
        private class TestReader : IContentReader
        {
            private int _contentIndex;
            private IContent[] _contentsToRead;

            public string RootPath { get; }
            public int EstimatedCount { get; }
            public IContent Content { get; private set; }
            public string RelativePath { get; private set; }

            public TestReader(string rootPath, IContent[] contentsToRead)
            {
                RootPath = rootPath;
                EstimatedCount = contentsToRead.Length;
                _contentsToRead = contentsToRead;
            }

            public Task<bool> ReadAsync(CancellationToken cancel = default)
            {
                if (_contentIndex < _contentsToRead.Length)
                {
                    Content = _contentsToRead[_contentIndex++];
                    RelativePath = Content.Path;

                    return Task.FromResult(true);
                }

                return Task.FromResult(false);
            }
        }
        #endregion

        [TestMethod]
        public async Task FsWriter_Root()
        {
            // ALIGN
            var checkedPaths = new List<string>();
            var createdPaths = new List<string>();
            var textBuilders = new List<StringBuilder>();

            var reader = new TestReader("/Root", new[]
            {
                new TestContent("Root", "", "PortalRoot", new Dictionary<string, object>(), new Attachment[0])
            });
            var writer = new FsWriterMock(
                outputDirectory: @"Q:\FsRoot",
                containerPath: null,
                rootName: null,
                isDirectoryExists: fsPath => { checkedPaths.Add(fsPath); return false; },
                createDirectory: fsPath => { createdPaths.Add(fsPath); },
                createTextWriter: (fsPath, append) =>
                {
                    if (append)
                        throw new NotSupportedException("Invalid 'append', expected: false.");
                    var sb = new StringBuilder();
                    textBuilders.Add(sb);
                    return new StringWriter(sb);
                },
                createBinaryStream: (fsPath, fileMode) => { throw new NotImplementedException(); });

            // ACTION
            var contentFlow = new ContentFlow(reader, writer);
            await contentFlow.TransferAsync();
            
            // ASSERT
            Assert.AreEqual(1, checkedPaths.Count);
            Assert.AreEqual(@"Q:\FsRoot", checkedPaths[0]);
            Assert.AreEqual(1, createdPaths.Count);
            Assert.AreEqual(@"Q:\FsRoot", createdPaths[0]);
            Assert.AreEqual(1, textBuilders.Count);
            Assert.AreEqual("{\"ContentType\":\"PortalRoot\",\"ContentName\":\"Root\",\"Fields\":{}}",
                textBuilders[0].ToString().RemoveWhitespaces());
        }
        [TestMethod] public void FsWriter_Root_Folder() { Assert.Fail(); }
        [TestMethod] public void FsWriter_Root_Folder_File() { Assert.Fail(); }
        [TestMethod] public void FsWriter_Root_RenamedFolder_File() { Assert.Fail(); }
    }
}
