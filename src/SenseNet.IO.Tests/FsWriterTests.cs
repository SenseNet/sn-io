using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SenseNet.IO.Implementations;

namespace SenseNet.IO.Tests
{
    [TestClass]
    public class FsWriterTests : TestBase
    {
        #region Nested classes
        private class FsWriterMock : FsWriter
        {
            public FsWriterMock(string outputDirectory, string rootName, bool flatten,
                Func<string, bool> isDirectoryExists,
                Func<string, bool> isFileExists,
                Action<string> createDirectory,
                Func<string, bool, TextWriter> createTextWriter,
                Func<string, FileMode, Stream> createBinaryStream) : base(Options.Create(
                new FsWriterArgs { Path = outputDirectory, Name = rootName, Flatten = flatten}))
            {
                _isDirectoryExists = isDirectoryExists;
                _isFileExists = isFileExists;
                _createDirectory = createDirectory;
                _createTextWriter = createTextWriter;
                _createBinaryStream = createBinaryStream;
            }

            private readonly Func<string, bool> _isDirectoryExists;
            private readonly Func<string, bool> _isFileExists;
            private readonly Action<string> _createDirectory;
            private readonly Func<string, bool, TextWriter> _createTextWriter;
            private readonly Func<string, FileMode, Stream> _createBinaryStream;

            protected override bool IsDirectoryExists(string fileDir)
            {
                return _isDirectoryExists(fileDir);
            }
            protected override bool IsFileExists(string fsPath)
            {
                return _isFileExists(fsPath);
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

            public string Name { get; set; }
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
                foreach (var attachment in _attachments)
                {
                    var ext = System.IO.Path.GetExtension(attachment.FileName);
                    var fileName = this.Name;
                    if (!attachment.FieldName.Equals("Binary", StringComparison.OrdinalIgnoreCase))
                        fileName += "." + attachment.FieldName;
                    attachment.FileName = fileName;
                }
                return Task.FromResult(_attachments);
            }
        }
        private class TestReader : IContentReader
        {
            private int _contentIndex;
            private IContent[] _contentsToRead;

            public string RootName { get; }
            public string RepositoryRootPath { get; }
            public int EstimatedCount { get; }
            public IContent Content { get; private set; }
            public string RelativePath { get; private set; }

            public TestReader(string rootPath, IContent[] contentsToRead)
            {
                RepositoryRootPath = rootPath;
                RootName = ContentPath.GetName(rootPath);
                EstimatedCount = contentsToRead.Length;
                _contentsToRead = contentsToRead;
            }

            public Task<bool> ReadSubTreeAsync(string relativePath, CancellationToken cancel = default)
            {
                throw new NotImplementedException();
            }
            public Task<bool> ReadAllAsync(string[] contentsWithoutChildren, CancellationToken cancel = default)
            {
                if (contentsWithoutChildren != null && contentsWithoutChildren.Length != 0)
                    throw new NotImplementedException();

                if (_contentIndex < _contentsToRead.Length)
                {
                    Content = _contentsToRead[_contentIndex++];
                    RelativePath = Content.Path;
                    return Task.FromResult(true);
                }
                return Task.FromResult(false);
            }

            public void SetReferenceUpdateTasks(IEnumerable<TransferTask> tasks, int taskCount) { }
            public Task<bool> ReadByReferenceUpdateTasksAsync(CancellationToken cancel) { return Task.FromResult(false); }
        }
        #endregion

        [TestMethod]
        public async Task FsWriter_Root()
        {
            // ALIGN
            var checkedPaths = new List<string>();
            var createdPaths = new List<string>();
            var createdMetaFiles = new List<string>();
            var textBuilders = new List<StringBuilder>();

            var reader = new TestReader("/Root", new[]
            {
                new TestContent("Root", "", "PortalRoot", new Dictionary<string, object>(), new Attachment[0])
            });
            var writer = new FsWriterMock(
                outputDirectory: @"Q:\FsRoot",
                rootName: null,
                flatten: false,
                isDirectoryExists: fsPath => { checkedPaths.Add(fsPath); return false; },
                isFileExists: fsPath => createdMetaFiles.Contains(fsPath),
                createDirectory: fsPath => { createdPaths.Add(fsPath); },
                createTextWriter: (fsPath, append) =>
                {
                    if (append)
                        throw new NotSupportedException("Invalid 'append', expected: false.");
                    createdMetaFiles.Add(fsPath.Replace('/', '\\'));
                    var sb = new StringBuilder();
                    textBuilders.Add(sb);
                    return new StringWriter(sb);
                },
                createBinaryStream: (fsPath, fileMode) => throw new NotImplementedException());

            // ACTION
            var contentFlow = new SimpleContentFlow(reader, writer, GetLogger<ContentFlow>());
            await contentFlow.TransferAsync(null);
            
            // ASSERT
            Assert.AreEqual(1, checkedPaths.Count);
            Assert.AreEqual(@"Q:\FsRoot", checkedPaths[0]);
            Assert.AreEqual(1, createdPaths.Count);
            Assert.AreEqual(@"Q:\FsRoot", createdPaths[0]);
            Assert.AreEqual(1, createdMetaFiles.Count);
            Assert.AreEqual(@"Q:\FsRoot\Root.Content", createdMetaFiles[0]);
            Assert.AreEqual(1, textBuilders.Count);
            Assert.AreEqual("{\"ContentType\":\"PortalRoot\",\"ContentName\":\"Root\",\"Fields\":{}}",
                textBuilders[0].ToString().RemoveWhitespaces());
        }

        [TestMethod]
        public async Task FsWriter_Root_Folder()
        {
            // ALIGN
            var checkedPaths = new List<string>();
            var createdPaths = new List<string>();
            var createdMetaFiles = new List<string>();
            var textBuilders = new List<StringBuilder>();

            var reader = new TestReader("/Root", new[]
            {
                new TestContent("Root", "", "PortalRoot", new Dictionary<string, object>(), new Attachment[0]),
                new TestContent("F1", "F1", "Folder", new Dictionary<string, object>(), new Attachment[0]),
            });
            var writer = new FsWriterMock(
                outputDirectory: @"Q:\FsRoot",
                rootName: null,
                flatten: false,
                isDirectoryExists: fsPath => { checkedPaths.Add(fsPath); return false; },
                isFileExists: fsPath => createdMetaFiles.Contains(fsPath),
                createDirectory: fsPath => { createdPaths.Add(fsPath); },
                createTextWriter: (fsPath, append) =>
                {
                    if (append)
                        throw new NotSupportedException("Invalid 'append', expected: false.");
                    createdMetaFiles.Add(fsPath.Replace('/', '\\'));
                    var sb = new StringBuilder();
                    textBuilders.Add(sb);
                    return new StringWriter(sb);
                },
                createBinaryStream: (fsPath, fileMode) => throw new NotImplementedException());

            // ACTION
            var contentFlow = new SimpleContentFlow(reader, writer, GetLogger<ContentFlow>());
            await contentFlow.TransferAsync(null);

            // ASSERT
            Assert.AreEqual(2, checkedPaths.Count);
            Assert.AreEqual(@"Q:\FsRoot", checkedPaths[0]);
            Assert.AreEqual(@"Q:\FsRoot\Root", checkedPaths[1]);
            Assert.AreEqual(2, createdPaths.Count);
            Assert.AreEqual(@"Q:\FsRoot", createdPaths[0]);
            Assert.AreEqual(@"Q:\FsRoot\Root", createdPaths[1]);
            Assert.AreEqual(2, createdMetaFiles.Count);
            Assert.AreEqual(@"Q:\FsRoot\Root.Content", createdMetaFiles[0]);
            Assert.AreEqual(@"Q:\FsRoot\Root\F1.Content", createdMetaFiles[1]);
            Assert.AreEqual(2, textBuilders.Count);
            Assert.AreEqual("{\"ContentType\":\"Folder\",\"ContentName\":\"F1\",\"Fields\":{}}",
                textBuilders[1].ToString().RemoveWhitespaces());
        }
        [TestMethod]
        public async Task FsWriter_Root_Folder_Folder()
        {
            // ALIGN
            var checkedPaths = new List<string>();
            var createdPaths = new List<string>();
            var createdMetaFiles = new List<string>();
            var textBuilders = new List<StringBuilder>();

            var reader = new TestReader("/Root", new[]
            {
                new TestContent("Root", "", "PortalRoot", new Dictionary<string, object>(), new Attachment[0]),
                new TestContent("F1", "F1", "Folder", new Dictionary<string, object>(), new Attachment[0]),
                new TestContent("F2", "F1/F2", "Folder", new Dictionary<string, object>(), new Attachment[0]),
            });
            var writer = new FsWriterMock(
                outputDirectory: @"Q:\FsRoot",
                rootName: null,
                flatten: false,
                isDirectoryExists: fsPath => { checkedPaths.Add(fsPath); return false; },
                isFileExists: fsPath => createdMetaFiles.Contains(fsPath),
                createDirectory: fsPath => { createdPaths.Add(fsPath); },
                createTextWriter: (fsPath, append) =>
                {
                    if (append)
                        throw new NotSupportedException("Invalid 'append', expected: false.");
                    createdMetaFiles.Add(fsPath.Replace('/', '\\'));
                    var sb = new StringBuilder();
                    textBuilders.Add(sb);
                    return new StringWriter(sb);
                },
                createBinaryStream: (fsPath, fileMode) => throw new NotImplementedException());

            // ACTION
            var contentFlow = new SimpleContentFlow(reader, writer, GetLogger<ContentFlow>());
            await contentFlow.TransferAsync(null);

            // ASSERT
            Assert.AreEqual(3, checkedPaths.Count);
            Assert.AreEqual(@"Q:\FsRoot", checkedPaths[0]);
            Assert.AreEqual(@"Q:\FsRoot\Root", checkedPaths[1]);
            Assert.AreEqual(@"Q:\FsRoot\Root\F1", checkedPaths[2]);
            Assert.AreEqual(3, createdPaths.Count);
            Assert.AreEqual(@"Q:\FsRoot", createdPaths[0]);
            Assert.AreEqual(@"Q:\FsRoot\Root", createdPaths[1]);
            Assert.AreEqual(@"Q:\FsRoot\Root\F1", createdPaths[2]);
            Assert.AreEqual(3, createdMetaFiles.Count);
            Assert.AreEqual(@"Q:\FsRoot\Root.Content", createdMetaFiles[0]);
            Assert.AreEqual(@"Q:\FsRoot\Root\F1.Content", createdMetaFiles[1]);
            Assert.AreEqual(@"Q:\FsRoot\Root\F1\F2.Content", createdMetaFiles[2]);
            Assert.AreEqual(3, textBuilders.Count);
            Assert.AreEqual("{\"ContentType\":\"Folder\",\"ContentName\":\"F1\",\"Fields\":{}}",
                textBuilders[1].ToString().RemoveWhitespaces());
            Assert.AreEqual("{\"ContentType\":\"Folder\",\"ContentName\":\"F2\",\"Fields\":{}}",
                textBuilders[2].ToString().RemoveWhitespaces());
        }
        [TestMethod]
        public async Task FsWriter_Root_RenamedFolder_Folder()
        {
            // ALIGN
            var checkedPaths = new List<string>();
            var createdPaths = new List<string>();
            var createdMetaFiles = new List<string>();
            var textBuilders = new List<StringBuilder>();

            var reader = new TestReader("/Root", new[]
            {
                //new TestContent("Root", "", "PortalRoot", new Dictionary<string, object>(), new Attachment[0]),
                new TestContent("F1", "", "Folder", new Dictionary<string, object>(), new Attachment[0]),
                new TestContent("F2", "F2", "Folder", new Dictionary<string, object>(), new Attachment[0]),
            });
            var writer = new FsWriterMock(
                outputDirectory: @"Q:\FsRoot\Root",
                rootName: "Fx",
                flatten: false,
                isDirectoryExists: fsPath =>
                {
                    checkedPaths.Add(fsPath); return false;
                },
                isFileExists: fsPath => createdMetaFiles.Contains(fsPath),
                createDirectory: fsPath => { createdPaths.Add(fsPath); },
                createTextWriter: (fsPath, append) =>
                {
                    if (append)
                        throw new NotSupportedException("Invalid 'append', expected: false.");
                    createdMetaFiles.Add(fsPath.Replace('/', '\\'));
                    var sb = new StringBuilder();
                    textBuilders.Add(sb);
                    return new StringWriter(sb);
                },
                createBinaryStream: (fsPath, fileMode) => throw new NotImplementedException());

            // ACTION
            var contentFlow = new SimpleContentFlow(reader, writer, GetLogger<ContentFlow>());
            await contentFlow.TransferAsync(null);

            // ASSERT
            Assert.AreEqual(2, checkedPaths.Count);
            Assert.AreEqual(@"Q:\FsRoot\Root", checkedPaths[0]);
            Assert.AreEqual(@"Q:\FsRoot\Root\Fx", checkedPaths[1]);
            Assert.AreEqual(2, createdPaths.Count);
            Assert.AreEqual(@"Q:\FsRoot\Root", createdPaths[0]);
            Assert.AreEqual(@"Q:\FsRoot\Root\Fx", createdPaths[1]);
            Assert.AreEqual(2, createdMetaFiles.Count);
            Assert.AreEqual(@"Q:\FsRoot\Root\Fx.Content", createdMetaFiles[0]);
            Assert.AreEqual(@"Q:\FsRoot\Root\Fx\F2.Content", createdMetaFiles[1]);
            Assert.AreEqual(2, textBuilders.Count);
            Assert.AreEqual("{\"ContentType\":\"Folder\",\"ContentName\":\"Fx\",\"Fields\":{}}",
                textBuilders[0].ToString().RemoveWhitespaces());
            Assert.AreEqual("{\"ContentType\":\"Folder\",\"ContentName\":\"F2\",\"Fields\":{}}",
                textBuilders[1].ToString().RemoveWhitespaces());
        }

        [TestMethod]
        public async Task FsWriter_Fields()
        {
            // ALIGN
            var textBuilders = new List<StringBuilder>();

            var reader = new TestReader("/Root", new[]
            {
                new TestContent("Root", "", "PortalRoot", new Dictionary<string, object>
                {
                    {"String1", "StringValue1"},
                    {"Int1", 42},
                    {"DateTime1", new DateTime(2021, 08,05,8,21,12, DateTimeKind.Utc)},
                    {"NullValue", null}
                }, new Attachment[0])
            });
            var writer = new FsWriterMock(
                outputDirectory: @"Q:\FsRoot",
                rootName: null,
                flatten: false,
                isDirectoryExists: fsPath => false,
                isFileExists: fsPath => false,
                createDirectory: fsPath => { },
                createTextWriter: (fsPath, append) =>
                {
                    if (append)
                        throw new NotSupportedException("Invalid 'append', expected: false.");
                    var sb = new StringBuilder();
                    textBuilders.Add(sb);
                    return new StringWriter(sb);
                },
                createBinaryStream: (fsPath, fileMode) => throw new NotImplementedException());

            // ACTION
            var contentFlow = new SimpleContentFlow(reader, writer, GetLogger<ContentFlow>());
            await contentFlow.TransferAsync(null);

            // ASSERT
            Assert.AreEqual(1, textBuilders.Count);
            Assert.AreEqual("{'ContentType':'PortalRoot','ContentName':'Root'," +
                            "'Fields':{'String1':'StringValue1','Int1':42,'DateTime1':'2021-08-05T08:21:12Z'}}",
                textBuilders[0].ToString().RemoveWhitespaces().Replace('"', '\''));
        }

        [TestMethod]
        public async Task FsWriter_Root_Folder_File()
        {
            // ALIGN
            var checkedPaths = new List<string>();
            var createdPaths = new List<string>();
            var createdMetaFiles = new List<string>();
            var textBuilders = new List<StringBuilder>();
            var createdBinaryFiles = new List<string>();
            var createdBinaries = new List<MemoryStream>();

            var reader = new TestReader("/Root", new[]
            {
                new TestContent("Root", "", "PortalRoot", new Dictionary<string, object>(), new Attachment[0]),
                new TestContent("F1", "F1", "Folder", new Dictionary<string, object>(), new Attachment[0]),
                new TestContent("File1.txt", "F1/File1.txt", "File", new Dictionary<string, object>(), new []
                {
                    new Attachment
                    {
                        FieldName = "Binary",
                        FileName = "File1.txt",
                        Stream = "Text content".ToStream()
                    }
                }),
            });
            var writer = new FsWriterMock(
                outputDirectory: @"Q:\FsRoot",
                rootName: null,
                flatten: false,
                isDirectoryExists: fsPath => { checkedPaths.Add(fsPath); return false; },
                isFileExists: fsPath => createdMetaFiles.Contains(fsPath),
                createDirectory: fsPath => { createdPaths.Add(fsPath); },
                createTextWriter: (fsPath, append) =>
                {
                    if (append)
                        throw new NotSupportedException("Invalid 'append', expected: false.");
                    createdMetaFiles.Add(fsPath.Replace('/', '\\'));
                    var sb = new StringBuilder();
                    textBuilders.Add(sb);
                    return new StringWriter(sb);
                },
                createBinaryStream: (fsPath, fileMode) =>
                {
                    if(fileMode != FileMode.OpenOrCreate)
                        throw new NotSupportedException("Invalid 'fileMode', expected: OpenOrCreate.");
                    createdBinaryFiles.Add(fsPath.Replace('/', '\\'));
                    var stream = new MemoryStream();
                    createdBinaries.Add(stream);
                    return stream;
                });

            // ACTION
            var contentFlow = new SimpleContentFlow(reader, writer, GetLogger<ContentFlow>());
            await contentFlow.TransferAsync(null);

            // ASSERT
            Assert.AreEqual(3, checkedPaths.Count);
            Assert.AreEqual(@"Q:\FsRoot", checkedPaths[0]);
            Assert.AreEqual(@"Q:\FsRoot\Root", checkedPaths[1]);
            Assert.AreEqual(@"Q:\FsRoot\Root\F1", checkedPaths[2]);
            Assert.AreEqual(3, createdPaths.Count);
            Assert.AreEqual(@"Q:\FsRoot", createdPaths[0]);
            Assert.AreEqual(@"Q:\FsRoot\Root", createdPaths[1]);
            Assert.AreEqual(@"Q:\FsRoot\Root\F1", createdPaths[2]);
            Assert.AreEqual(3, createdMetaFiles.Count);
            Assert.AreEqual(@"Q:\FsRoot\Root.Content", createdMetaFiles[0]);
            Assert.AreEqual(@"Q:\FsRoot\Root\F1.Content", createdMetaFiles[1]);
            Assert.AreEqual(@"Q:\FsRoot\Root\F1\File1.txt.Content", createdMetaFiles[2]);
            Assert.AreEqual(3, textBuilders.Count);
            Assert.AreEqual("{\"ContentType\":\"Folder\",\"ContentName\":\"F1\",\"Fields\":{}}",
                textBuilders[1].ToString().RemoveWhitespaces());
            Assert.AreEqual("{\"ContentType\":\"File\",\"ContentName\":\"File1.txt\",\"Fields\":{}}",
                textBuilders[2].ToString().RemoveWhitespaces());
            Assert.AreEqual(1, createdBinaryFiles.Count);
            Assert.AreEqual(@"Q:\FsRoot\Root\F1\File1.txt", createdBinaryFiles[0]);
            Assert.AreEqual(1, createdBinaries.Count);
            Assert.AreEqual("Text content", createdBinaries[0].ReadAsString());
        }
        [TestMethod]
        public async Task FsWriter_Root_Folder_FileMoreAttachments()
        {
            // ALIGN
            var checkedPaths = new List<string>();
            var createdPaths = new List<string>();
            var createdMetaFiles = new List<string>();
            var textBuilders = new List<StringBuilder>();
            var createdBinaryFiles = new List<string>();
            var createdBinaries = new List<MemoryStream>();

            var reader = new TestReader("/Root", new[]
            {
                new TestContent("Root", "", "PortalRoot", new Dictionary<string, object>(), new Attachment[0]),
                new TestContent("F1", "F1", "Folder", new Dictionary<string, object>(), new Attachment[0]),
                new TestContent("File1.txt", "F1/File1.txt", "File", new Dictionary<string, object>(), new []
                {
                    new Attachment {FieldName = "Binary", FileName = "File1.txt", Stream = "Text content 1".ToStream()},
                    new Attachment {FieldName = "Bin2", FileName = "File1.txt.Bin2", Stream = "Text content 2".ToStream()},
                    new Attachment {FieldName = "Bin3", FileName = "File1.txt.Bin3", Stream = "Text content 3".ToStream()},
                }),
            });
            var writer = new FsWriterMock(
                outputDirectory: @"Q:\FsRoot",
                rootName: null,
                flatten: false,
                isDirectoryExists: fsPath => { checkedPaths.Add(fsPath); return false; },
                isFileExists: fsPath => createdMetaFiles.Contains(fsPath),
                createDirectory: fsPath => { createdPaths.Add(fsPath); },
                createTextWriter: (fsPath, append) =>
                {
                    if (append)
                        throw new NotSupportedException("Invalid 'append', expected: false.");
                    createdMetaFiles.Add(fsPath.Replace('/', '\\'));
                    var sb = new StringBuilder();
                    textBuilders.Add(sb);
                    return new StringWriter(sb);
                },
                createBinaryStream: (fsPath, fileMode) =>
                {
                    if (fileMode != FileMode.OpenOrCreate)
                        throw new NotSupportedException("Invalid 'fileMode', expected: OpenOrCreate.");
                    createdBinaryFiles.Add(fsPath.Replace('/', '\\'));
                    var stream = new MemoryStream();
                    createdBinaries.Add(stream);
                    return stream;
                });

            // ACTION
            var contentFlow = new SimpleContentFlow(reader, writer, GetLogger<ContentFlow>());
            await contentFlow.TransferAsync(null);

            // ASSERT
            Assert.AreEqual(3, checkedPaths.Count);
            Assert.AreEqual(@"Q:\FsRoot", checkedPaths[0]);
            Assert.AreEqual(@"Q:\FsRoot\Root", checkedPaths[1]);
            Assert.AreEqual(@"Q:\FsRoot\Root\F1", checkedPaths[2]);
            Assert.AreEqual(3, createdPaths.Count);
            Assert.AreEqual(@"Q:\FsRoot", createdPaths[0]);
            Assert.AreEqual(@"Q:\FsRoot\Root", createdPaths[1]);
            Assert.AreEqual(@"Q:\FsRoot\Root\F1", createdPaths[2]);
            Assert.AreEqual(3, createdMetaFiles.Count);
            Assert.AreEqual(@"Q:\FsRoot\Root.Content", createdMetaFiles[0]);
            Assert.AreEqual(@"Q:\FsRoot\Root\F1.Content", createdMetaFiles[1]);
            Assert.AreEqual(@"Q:\FsRoot\Root\F1\File1.txt.Content", createdMetaFiles[2]);
            Assert.AreEqual(3, textBuilders.Count);
            Assert.AreEqual("{\"ContentType\":\"Folder\",\"ContentName\":\"F1\",\"Fields\":{}}",
                textBuilders[1].ToString().RemoveWhitespaces());
            Assert.AreEqual("{\"ContentType\":\"File\",\"ContentName\":\"File1.txt\",\"Fields\":{}}",
                textBuilders[2].ToString().RemoveWhitespaces());
            Assert.AreEqual(3, createdBinaryFiles.Count);
            Assert.AreEqual(@"Q:\FsRoot\Root\F1\File1.txt", createdBinaryFiles[0]);
            Assert.AreEqual(@"Q:\FsRoot\Root\F1\File1.txt.Bin2", createdBinaryFiles[1]);
            Assert.AreEqual(@"Q:\FsRoot\Root\F1\File1.txt.Bin3", createdBinaryFiles[2]);
            Assert.AreEqual(3, createdBinaries.Count);
            Assert.AreEqual("Text content 1", createdBinaries[0].ReadAsString());
            Assert.AreEqual("Text content 2", createdBinaries[1].ReadAsString());
            Assert.AreEqual("Text content 3", createdBinaries[2].ReadAsString());
        }

        [TestMethod]
        public async Task FsWriter_QueryResult()
        {
            TestContent CreateFile(string name, string path)
            {
                return new TestContent(name, path, "File", new Dictionary<string, object>(),
                    new[]
                    {
                        new Attachment {FieldName = "Binary", FileName = name, Stream = $"{name} content".ToStream()}
                    });
            }
            // ALIGN
            var checkedPaths = new List<string>();
            var createdPaths = new List<string>();
            var createdMetaFiles = new List<string>();
            var textBuilders = new List<StringBuilder>();
            var createdBinaryFiles = new List<string>();
            var createdBinaries = new List<MemoryStream>();

            var reader = new TestReader("/Root/Content", new[]
            {
                CreateFile("File-1.txt", "Docs/F1/F2/file-1.txt"),
                CreateFile("File-2.txt", "Docs/F1/F2/file-2.txt"),
                CreateFile("File-3.txt", "Docs/F1/F2/file-3.txt"),
                CreateFile("File-4.txt", "Docs/F1/F3/file-4.txt"),
                CreateFile("File-5.txt", "Docs/F1/F3/file-5.txt"),
                CreateFile("File-6.txt", "Docs/F1/F3/file-6.txt"),
            });
            var writer = new FsWriterMock(
                outputDirectory: @"Q:\FsRoot",
                rootName: null,
                flatten: false,
                isDirectoryExists: fsPath =>
                {
                    if (checkedPaths.Contains(fsPath))
                        return true;
                    checkedPaths.Add(fsPath);
                    return false;
                },
                isFileExists: fsPath => createdMetaFiles.Contains(fsPath),
                createDirectory: fsPath =>
                {
                    if (!createdPaths.Contains(fsPath))
                        createdPaths.Add(fsPath);
                },
                createTextWriter: (fsPath, append) =>
                {
                    if (append)
                        throw new NotSupportedException("Invalid 'append', expected: false.");
                    createdMetaFiles.Add(fsPath.Replace('/', '\\'));
                    var sb = new StringBuilder();
                    textBuilders.Add(sb);
                    return new StringWriter(sb);
                },
                createBinaryStream: (fsPath, fileMode) =>
                {
                    if (fileMode != FileMode.OpenOrCreate)
                        throw new NotSupportedException("Invalid 'fileMode', expected: OpenOrCreate.");
                    createdBinaryFiles.Add(fsPath.Replace('/', '\\'));
                    var stream = new MemoryStream();
                    createdBinaries.Add(stream);
                    return stream;
                });

            // ACTION
            var contentFlow = new SimpleContentFlow(reader, writer, GetLogger<ContentFlow>());
            await contentFlow.TransferAsync(null);

            // ASSERT
            Assert.AreEqual(2, checkedPaths.Count);
            Assert.AreEqual(@"Q:\FsRoot\Content\Docs\F1\F2", checkedPaths[0]);
            Assert.AreEqual(@"Q:\FsRoot\Content\Docs\F1\F3", checkedPaths[1]);
            Assert.AreEqual(2, createdPaths.Count);
            Assert.AreEqual(@"Q:\FsRoot\Content\Docs\F1\F2", createdPaths[0]);
            Assert.AreEqual(@"Q:\FsRoot\Content\Docs\F1\F3", createdPaths[1]);
            Assert.AreEqual(6, createdMetaFiles.Count);
            Assert.AreEqual(@"Q:\FsRoot\Content\Docs\F1\F2\file-1.txt.Content", createdMetaFiles[0]);
            Assert.AreEqual(@"Q:\FsRoot\Content\Docs\F1\F2\file-2.txt.Content", createdMetaFiles[1]);
            Assert.AreEqual(@"Q:\FsRoot\Content\Docs\F1\F2\file-3.txt.Content", createdMetaFiles[2]);
            Assert.AreEqual(@"Q:\FsRoot\Content\Docs\F1\F3\file-4.txt.Content", createdMetaFiles[3]);
            Assert.AreEqual(@"Q:\FsRoot\Content\Docs\F1\F3\file-5.txt.Content", createdMetaFiles[4]);
            Assert.AreEqual(@"Q:\FsRoot\Content\Docs\F1\F3\file-6.txt.Content", createdMetaFiles[5]);
            Assert.AreEqual(6, textBuilders.Count);
            Assert.AreEqual("{\"ContentType\":\"File\",\"ContentName\":\"File-1.txt\",\"Fields\":{}}", textBuilders[0].ToString().RemoveWhitespaces());
            Assert.AreEqual("{\"ContentType\":\"File\",\"ContentName\":\"File-2.txt\",\"Fields\":{}}", textBuilders[1].ToString().RemoveWhitespaces());
            Assert.AreEqual("{\"ContentType\":\"File\",\"ContentName\":\"File-3.txt\",\"Fields\":{}}", textBuilders[2].ToString().RemoveWhitespaces());
            Assert.AreEqual("{\"ContentType\":\"File\",\"ContentName\":\"File-4.txt\",\"Fields\":{}}", textBuilders[3].ToString().RemoveWhitespaces());
            Assert.AreEqual("{\"ContentType\":\"File\",\"ContentName\":\"File-5.txt\",\"Fields\":{}}", textBuilders[4].ToString().RemoveWhitespaces());
            Assert.AreEqual("{\"ContentType\":\"File\",\"ContentName\":\"File-6.txt\",\"Fields\":{}}", textBuilders[5].ToString().RemoveWhitespaces());
            Assert.AreEqual(6, createdBinaryFiles.Count);
            Assert.AreEqual(@"Q:\FsRoot\Content\Docs\F1\F2\File-1.txt", createdBinaryFiles[0]);
            Assert.AreEqual(@"Q:\FsRoot\Content\Docs\F1\F2\File-2.txt", createdBinaryFiles[1]);
            Assert.AreEqual(@"Q:\FsRoot\Content\Docs\F1\F2\File-3.txt", createdBinaryFiles[2]);
            Assert.AreEqual(@"Q:\FsRoot\Content\Docs\F1\F3\File-4.txt", createdBinaryFiles[3]);
            Assert.AreEqual(@"Q:\FsRoot\Content\Docs\F1\F3\File-5.txt", createdBinaryFiles[4]);
            Assert.AreEqual(@"Q:\FsRoot\Content\Docs\F1\F3\File-6.txt", createdBinaryFiles[5]);
            Assert.AreEqual(6, createdBinaries.Count);
            Assert.AreEqual("File-1.txt content", createdBinaries[0].ReadAsString());
            Assert.AreEqual("File-2.txt content", createdBinaries[1].ReadAsString());
            Assert.AreEqual("File-3.txt content", createdBinaries[2].ReadAsString());
            Assert.AreEqual("File-4.txt content", createdBinaries[3].ReadAsString());
            Assert.AreEqual("File-5.txt content", createdBinaries[4].ReadAsString());
            Assert.AreEqual("File-6.txt content", createdBinaries[5].ReadAsString());
        }
        [TestMethod]
        public async Task FsWriter_QueryResult_Flatten()
        {
            TestContent CreateFile(string name, string path)
            {
                return new TestContent(name, path, "File", new Dictionary<string, object>(),
                    new[] {new Attachment {FieldName = "Binary", FileName = name, Stream = $"{name} content".ToStream()}});
            }
            // ALIGN
            var checkedPaths = new List<string>();
            var createdPaths = new List<string>();
            var createdMetaFiles = new List<string>();
            var textBuilders = new List<StringBuilder>();
            var createdBinaryFiles = new List<string>();
            var createdBinaries = new List<MemoryStream>();

            var reader = new TestReader("/Root/Content", new[]
            {
                CreateFile("File-1.txt", "Docs/F1/F2/file-1.txt"),
                CreateFile("File-2.txt", "Docs/F1/F2/file-2.txt"),
                CreateFile("File-3.txt", "Docs/F1/F2/file-3.txt"),
                CreateFile("File-4.txt", "Docs/F1/F3/file-4.txt"),
                CreateFile("File-5.txt", "Docs/F1/F3/file-5.txt"),
                CreateFile("File-6.txt", "Docs/F1/F3/file-6.txt"),
            });
            var writer = new FsWriterMock(
                outputDirectory: @"Q:\FsRoot",
                rootName: null,
                flatten: true,
                isDirectoryExists: fsPath =>
                {
                    if (checkedPaths.Contains(fsPath))
                        return true;
                    checkedPaths.Add(fsPath);
                    return false;
                },
                isFileExists: fsPath => createdMetaFiles.Contains(fsPath),
                createDirectory: fsPath =>
                {
                    if (!createdPaths.Contains(fsPath))
                        createdPaths.Add(fsPath);
                },
                createTextWriter: (fsPath, append) =>
                {
                    if (append)
                        throw new NotSupportedException("Invalid 'append', expected: false.");
                    createdMetaFiles.Add(fsPath.Replace('/', '\\'));
                    var sb = new StringBuilder();
                    textBuilders.Add(sb);
                    return new StringWriter(sb);
                },
                createBinaryStream: (fsPath, fileMode) =>
                {
                    if (fileMode != FileMode.OpenOrCreate)
                        throw new NotSupportedException("Invalid 'fileMode', expected: OpenOrCreate.");
                    createdBinaryFiles.Add(fsPath.Replace('/', '\\'));
                    var stream = new MemoryStream();
                    createdBinaries.Add(stream);
                    return stream;
                });

            // ACTION
            var contentFlow = new SimpleContentFlow(reader, writer, GetLogger<ContentFlow>());
            await contentFlow.TransferAsync(null);

            // ASSERT
            Assert.AreEqual(1, checkedPaths.Count);
            Assert.AreEqual(@"Q:\FsRoot", checkedPaths[0]);
            Assert.AreEqual(1, createdPaths.Count);
            Assert.AreEqual(@"Q:\FsRoot", createdPaths[0]);
            Assert.AreEqual(6, createdMetaFiles.Count);
            Assert.AreEqual(@"Q:\FsRoot\file-1.txt.Content", createdMetaFiles[0]);
            Assert.AreEqual(@"Q:\FsRoot\file-2.txt.Content", createdMetaFiles[1]);
            Assert.AreEqual(@"Q:\FsRoot\file-3.txt.Content", createdMetaFiles[2]);
            Assert.AreEqual(@"Q:\FsRoot\file-4.txt.Content", createdMetaFiles[3]);
            Assert.AreEqual(@"Q:\FsRoot\file-5.txt.Content", createdMetaFiles[4]);
            Assert.AreEqual(@"Q:\FsRoot\file-6.txt.Content", createdMetaFiles[5]);
            Assert.AreEqual(6, textBuilders.Count);
            Assert.AreEqual("{\"ContentType\":\"File\",\"ContentName\":\"File-1.txt\",\"Fields\":{}}", textBuilders[0].ToString().RemoveWhitespaces());
            Assert.AreEqual("{\"ContentType\":\"File\",\"ContentName\":\"File-2.txt\",\"Fields\":{}}", textBuilders[1].ToString().RemoveWhitespaces());
            Assert.AreEqual("{\"ContentType\":\"File\",\"ContentName\":\"File-3.txt\",\"Fields\":{}}", textBuilders[2].ToString().RemoveWhitespaces());
            Assert.AreEqual("{\"ContentType\":\"File\",\"ContentName\":\"File-4.txt\",\"Fields\":{}}", textBuilders[3].ToString().RemoveWhitespaces());
            Assert.AreEqual("{\"ContentType\":\"File\",\"ContentName\":\"File-5.txt\",\"Fields\":{}}", textBuilders[4].ToString().RemoveWhitespaces());
            Assert.AreEqual("{\"ContentType\":\"File\",\"ContentName\":\"File-6.txt\",\"Fields\":{}}", textBuilders[5].ToString().RemoveWhitespaces());
            Assert.AreEqual(6, createdBinaryFiles.Count);
            Assert.AreEqual(@"Q:\FsRoot\File-1.txt", createdBinaryFiles[0]);
            Assert.AreEqual(@"Q:\FsRoot\File-2.txt", createdBinaryFiles[1]);
            Assert.AreEqual(@"Q:\FsRoot\File-3.txt", createdBinaryFiles[2]);
            Assert.AreEqual(@"Q:\FsRoot\File-4.txt", createdBinaryFiles[3]);
            Assert.AreEqual(@"Q:\FsRoot\File-5.txt", createdBinaryFiles[4]);
            Assert.AreEqual(@"Q:\FsRoot\File-6.txt", createdBinaryFiles[5]);
            Assert.AreEqual(6, createdBinaries.Count);
            Assert.AreEqual("File-1.txt content", createdBinaries[0].ReadAsString());
            Assert.AreEqual("File-2.txt content", createdBinaries[1].ReadAsString());
            Assert.AreEqual("File-3.txt content", createdBinaries[2].ReadAsString());
            Assert.AreEqual("File-4.txt content", createdBinaries[3].ReadAsString());
            Assert.AreEqual("File-5.txt content", createdBinaries[4].ReadAsString());
            Assert.AreEqual("File-6.txt content", createdBinaries[5].ReadAsString());
        }
        [TestMethod]
        public async Task FsWriter_QueryResult_Flatten_Suffixes()
        {
            TestContent CreateFile(string name, string path)
            {
                return new TestContent(name, path, "File", new Dictionary<string, object>(),
                    new[] { new Attachment { FieldName = "Binary", FileName = name, Stream = $"{name} content".ToStream() } });
            }
            // ALIGN
            var checkedPaths = new List<string>();
            var createdPaths = new List<string>();
            var createdMetaFiles = new List<string>();
            var textBuilders = new List<StringBuilder>();
            var createdBinaryFiles = new List<string>();
            var createdBinaries = new List<MemoryStream>();

            var reader = new TestReader("/Root/Content", new[]
            {
                CreateFile("File-1.txt", "Docs/F1/F2/File-1.txt"),
                CreateFile("File-2.txt", "Docs/F1/F2/File-2.txt"),
                CreateFile("File-3.txt", "Docs/F1/F2/File-3.txt"),
                CreateFile("File-1.txt", "Docs/F1/F3/File-1.txt"),
                CreateFile("File-3.txt", "Docs/F1/F3/File-3.txt"),
                CreateFile("File-4.txt", "Docs/F1/F3/File-4.txt"),
            });
            var writer = new FsWriterMock(
                outputDirectory: @"Q:\FsRoot",
                rootName: null,
                flatten: true,
                isDirectoryExists: fsPath =>
                {
                    if (checkedPaths.Contains(fsPath))
                        return true;
                    checkedPaths.Add(fsPath);
                    return false;
                },
                isFileExists: fsPath =>
                {
                    return createdMetaFiles.Contains(fsPath);
                },
                createDirectory: fsPath =>
                {
                    if (!createdPaths.Contains(fsPath))
                        createdPaths.Add(fsPath);
                },
                createTextWriter: (fsPath, append) =>
                {
                    if (append)
                        throw new NotSupportedException("Invalid 'append', expected: false.");
                    createdMetaFiles.Add(fsPath.Replace('/', '\\'));
                    var sb = new StringBuilder();
                    textBuilders.Add(sb);
                    return new StringWriter(sb);
                },
                createBinaryStream: (fsPath, fileMode) =>
                {
                    if (fileMode != FileMode.OpenOrCreate)
                        throw new NotSupportedException("Invalid 'fileMode', expected: OpenOrCreate.");
                    createdBinaryFiles.Add(fsPath.Replace('/', '\\'));
                    var stream = new MemoryStream();
                    createdBinaries.Add(stream);
                    return stream;
                });

            // ACTION
            var contentFlow = new SimpleContentFlow(reader, writer, GetLogger<ContentFlow>());
            await contentFlow.TransferAsync(null);

            // ASSERT
            Assert.AreEqual(1, checkedPaths.Count);
            Assert.AreEqual(@"Q:\FsRoot", checkedPaths[0]);
            Assert.AreEqual(1, createdPaths.Count);
            Assert.AreEqual(@"Q:\FsRoot", createdPaths[0]);
            Assert.AreEqual(6, createdMetaFiles.Count);
            Assert.AreEqual(@"Q:\FsRoot\File-1.txt.Content", createdMetaFiles[0]);
            Assert.AreEqual(@"Q:\FsRoot\File-2.txt.Content", createdMetaFiles[1]);
            Assert.AreEqual(@"Q:\FsRoot\File-3.txt.Content", createdMetaFiles[2]);
            Assert.AreEqual(@"Q:\FsRoot\File-1(1).txt.Content", createdMetaFiles[3]);
            Assert.AreEqual(@"Q:\FsRoot\File-3(1).txt.Content", createdMetaFiles[4]);
            Assert.AreEqual(@"Q:\FsRoot\File-4.txt.Content", createdMetaFiles[5]);
            Assert.AreEqual(6, textBuilders.Count);
            Assert.AreEqual("{\"ContentType\":\"File\",\"ContentName\":\"File-1.txt\",\"Fields\":{}}", textBuilders[0].ToString().RemoveWhitespaces());
            Assert.AreEqual("{\"ContentType\":\"File\",\"ContentName\":\"File-2.txt\",\"Fields\":{}}", textBuilders[1].ToString().RemoveWhitespaces());
            Assert.AreEqual("{\"ContentType\":\"File\",\"ContentName\":\"File-3.txt\",\"Fields\":{}}", textBuilders[2].ToString().RemoveWhitespaces());
            Assert.AreEqual("{\"ContentType\":\"File\",\"ContentName\":\"File-1(1).txt\",\"Fields\":{}}", textBuilders[3].ToString().RemoveWhitespaces());
            Assert.AreEqual("{\"ContentType\":\"File\",\"ContentName\":\"File-3(1).txt\",\"Fields\":{}}", textBuilders[4].ToString().RemoveWhitespaces());
            Assert.AreEqual("{\"ContentType\":\"File\",\"ContentName\":\"File-4.txt\",\"Fields\":{}}", textBuilders[5].ToString().RemoveWhitespaces());
            Assert.AreEqual(6, createdBinaryFiles.Count);
            Assert.AreEqual(@"Q:\FsRoot\File-1.txt", createdBinaryFiles[0]);
            Assert.AreEqual(@"Q:\FsRoot\File-2.txt", createdBinaryFiles[1]);
            Assert.AreEqual(@"Q:\FsRoot\File-3.txt", createdBinaryFiles[2]);
            Assert.AreEqual(@"Q:\FsRoot\File-1(1).txt", createdBinaryFiles[3]);
            Assert.AreEqual(@"Q:\FsRoot\File-3(1).txt", createdBinaryFiles[4]);
            Assert.AreEqual(@"Q:\FsRoot\File-4.txt", createdBinaryFiles[5]);
            Assert.AreEqual(6, createdBinaries.Count);
            Assert.AreEqual("File-1.txt content", createdBinaries[0].ReadAsString());
            Assert.AreEqual("File-2.txt content", createdBinaries[1].ReadAsString());
            Assert.AreEqual("File-3.txt content", createdBinaries[2].ReadAsString());
            Assert.AreEqual("File-1.txt content", createdBinaries[3].ReadAsString());
            Assert.AreEqual("File-3.txt content", createdBinaries[4].ReadAsString());
            Assert.AreEqual("File-4.txt content", createdBinaries[5].ReadAsString());
        }
    }
}
