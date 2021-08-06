using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SenseNet.IO.Implementations;

namespace SenseNet.IO.Tests
{
    [TestClass]
    public class FsReaderTests
    {
        #region Nested classes
        private class FsContentMock : FsContent
        {
            private readonly Func<string, bool> _isFileExists;
            private readonly Func<string, TextReader> _createStreamReader;
            private readonly Func<string, FileMode, Stream> _createFileStream;

            public FsContentMock(Func<string, bool> isFileExists,
                Func<string, TextReader> createStreamReader,
                Func<string, FileMode, Stream> createFileStream,
                string name, string metaFilePath, bool isDirectory, FsContent parent, string defaultAttachmentPath = null)
                : base(name, metaFilePath, isDirectory, parent, defaultAttachmentPath)
            {
                _isFileExists = isFileExists;
                _createStreamReader = createStreamReader;
                _createFileStream = createFileStream;
            }

            protected override bool IsFileExists(string fsPath)
            {
                return _isFileExists(fsPath);
            }
            protected override TextReader CreateStreamReader(string metaFilePath)
            {
                return _createStreamReader(metaFilePath);
            }
            protected override Stream CreateFileStream(string fsPath, FileMode fileMode)
            {
                return _createFileStream(fsPath, fileMode);
            }
        }

        private class FsReaderMock : FsReader
        {
            private readonly Func<string, bool> _isFileExists;
            private readonly Func<string, bool> _isDirectoryExists;
            private readonly Func<string, string[]> _getFsDirectories;
            private readonly Func<string, string[]> _getFsFiles;
            private readonly Func<string, bool> _fsContentIsFileExists;
            private readonly Func<string, TextReader> _fsContentCreateStreamReader;
            private readonly Func<string, FileMode, Stream> _fsContentCreateFileStream;

            public FsReaderMock(string fsRootPath, string rootPath,
                Func<string, bool> isFileExists,
                Func<string, bool> isDirectoryExists,
                Func<string, string[]> getFsDirectories,
                Func<string, string[]> getFsFiles,
                Func<string, bool> fsContentIsFileExists,
                Func<string, TextReader> fsContentCreateStreamReader,
                Func<string, FileMode, Stream> fsContentCreateFileStream
                ) : base(fsRootPath, rootPath)
            {
                _isFileExists = isFileExists;
                _isDirectoryExists = isDirectoryExists;
                _getFsDirectories = getFsDirectories;
                _getFsFiles = getFsFiles;
                _fsContentIsFileExists = fsContentIsFileExists;
                _fsContentCreateStreamReader = fsContentCreateStreamReader;
                _fsContentCreateFileStream = fsContentCreateFileStream;
            }

            protected override FsContent CreateFsContent(string name, string metaFilePath, bool isDirectory, FsContent parent,
                string defaultAttachmentPath = null)
            {
                return new FsContentMock(_fsContentIsFileExists, _fsContentCreateStreamReader, _fsContentCreateFileStream,
                    name, metaFilePath, isDirectory, parent, defaultAttachmentPath);
            }

            protected override bool IsFileExists(string fsPath)
            {
                return _isFileExists(fsPath);
            }
            protected override bool IsDirectoryExists(string fsPath)
            {
                return _isDirectoryExists(fsPath);
            }
            protected override string[] GetFsDirectories(string fsDirectoryPath)
            {
                return _getFsDirectories(fsDirectoryPath);
            }
            protected override string[] GetFsFiles(string fsDirectoryPath)
            {
                return _getFsFiles(fsDirectoryPath);
            }
        }
        #endregion

        [TestMethod]
        public async Task FsReader_Read_Root()
        {
            var directories = new[]
            {
                @"Q:\Import"
            };
            var files = new Dictionary<string, string>
            {
                {@"Q:\Import\Root.Content", "{'ContentType':'PortalRoot','ContentName':'Root','Fields':{},'Permissions':{" +
                                            "'IsInherited':true,'Entries':[{'Identity':'/Root/IMS/BuiltIn/Portal/Everyone'," +
                                            "'LocalOnly':true,'Permissions':{'See':'allow'}}]}}"}
            };

            var reader = new FsReaderMock(@"Q:\Import", "/Root",
                isFileExists: fsPath => files.ContainsKey(fsPath),
                isDirectoryExists: fsPath => directories.Contains(fsPath),
                getFsDirectories: fsPath => { if(fsPath == @"Q:\Import\Root") return new string[0]; throw new Exception("##"); },
                getFsFiles: fsPath => { if (fsPath == @"Q:\Import\Root") return new string[0]; throw new Exception("##"); },
                fsContentIsFileExists: null,
                fsContentCreateStreamReader: fsPath => new StringReader(files[fsPath]),
                fsContentCreateFileStream: null);

            var readings = new Dictionary<string, IContent>();

            // ACTION
            while (await reader.ReadAsync())
                readings.Add(reader.RelativePath, reader.Content);

            // ASSERT
            var contents = readings.ToArray();
            Assert.AreEqual(1, contents.Length);
            Assert.AreEqual("", contents[0].Key);
            var content = contents[0].Value;
            Assert.AreEqual("Root", content.Name);
            Assert.AreEqual("PortalRoot", content.Type);
            Assert.AreEqual(0, content.FieldNames.Length);
            Assert.AreEqual(0, (await content.GetAttachmentsAsync()).Length);
            Assert.AreEqual(true, content.Permissions.IsInherited);
            Assert.AreEqual(1, content.Permissions.Entries.Length);
        }
        [TestMethod]
        public async Task FsReader_Read_Root_ContentTypeIsIrrelevant()
        {
            var directories = new[]
            {
                @"Q:\Import"
            };
            var files = new Dictionary<string, string>
            {
                {@"Q:\Import\Root.Content", "{'ContentType':'PortalRoot','ContentName':'Root'," +
                                            "'Fields':{'Type':'ContentType1'}," +
                                            "'Permissions':{'IsInherited':true,'Entries':[{'Identity':" +
                                            "'/Root/IMS/BuiltIn/Portal/Everyone','LocalOnly':true,'Permissions':{'See':'allow'}}]}}"}
            };

            var reader = new FsReaderMock(@"Q:\Import", "/Root",
                isFileExists: fsPath => files.ContainsKey(fsPath),
                isDirectoryExists: fsPath => directories.Contains(fsPath),
                getFsDirectories: fsPath => { if (fsPath == @"Q:\Import\Root") return new string[0]; throw new Exception("##"); },
                getFsFiles: fsPath => { if (fsPath == @"Q:\Import\Root") return new string[0]; throw new Exception("##"); },
                fsContentIsFileExists: null,
                fsContentCreateStreamReader: fsPath => new StringReader(files[fsPath]),
                fsContentCreateFileStream: null);

            var readings = new Dictionary<string, IContent>();

            // ACTION
            while (await reader.ReadAsync())
                readings.Add(reader.RelativePath, reader.Content);

            // ASSERT
            var contents = readings.ToArray();
            Assert.AreEqual(1, contents.Length);
            Assert.AreEqual("", contents[0].Key);
            var content = contents[0].Value;
            Assert.AreEqual("Root", content.Name);
            Assert.AreEqual("PortalRoot", content.Type);
            Assert.AreEqual(1, content.FieldNames.Length);
            Assert.AreEqual("Type", content.FieldNames[0]);
            Assert.AreEqual("ContentType1", content["Type"]);
            Assert.AreEqual(0, (await content.GetAttachmentsAsync()).Length);
            Assert.AreEqual(true, content.Permissions.IsInherited);
            Assert.AreEqual(1, content.Permissions.Entries.Length);
        }
        //[TestMethod]
        //public async Task FsReader_Read_____()
        //{

        //    Assert.Fail("##########################################################################################");
        //}
    }
}
