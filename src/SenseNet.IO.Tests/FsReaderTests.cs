using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using SenseNet.IO.Implementations;

namespace SenseNet.IO.Tests
{
    [TestClass]
    public class FsReaderTests
    {
        #region Nested classes
        [DebuggerDisplay("{" + nameof(Path) + "}")]
        private class FsContentMock : FsContent
        {
            private readonly Func<string, bool> _isFileExists;
            private readonly Func<string, TextReader> _createStreamReader;
            private readonly Func<string, FileMode, Stream> _createFileStream;

            public FsContentMock(Func<string, bool> isFileExists,
                Func<string, TextReader> createStreamReader,
                Func<string, FileMode, Stream> createFileStream,
                string name, string relativePath, string metaFilePath, bool isDirectory, string defaultAttachmentPath = null)
                : base(name, relativePath, metaFilePath, isDirectory, defaultAttachmentPath)
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

            public FsReaderMock(string fsRootPath,
                Func<string, bool> isFileExists,
                Func<string, bool> isDirectoryExists,
                Func<string, string[]> getFsDirectories,
                Func<string, string[]> getFsFiles,
                Func<string, bool> fsContentIsFileExists,
                Func<string, TextReader> fsContentCreateStreamReader,
                Func<string, FileMode, Stream> fsContentCreateFileStream
                ) : base(Options.Create(new FsReaderArgs { Path = fsRootPath }))
            {
                _isFileExists = isFileExists;
                _isDirectoryExists = isDirectoryExists;
                _getFsDirectories = getFsDirectories;
                _getFsFiles = getFsFiles;
                _fsContentIsFileExists = fsContentIsFileExists;
                _fsContentCreateStreamReader = fsContentCreateStreamReader;
                _fsContentCreateFileStream = fsContentCreateFileStream;
            }

            protected override FsContent CreateFsContent(string name, string relativePath, string metaFilePath, bool isDirectory,
                string defaultAttachmentPath = null)
            {
                return new FsContentMock(_fsContentIsFileExists, _fsContentCreateStreamReader, _fsContentCreateFileStream,
                    name, relativePath, metaFilePath, isDirectory, defaultAttachmentPath);
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
        public async Task FsReader_Read_RootFileOnly()
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

            var reader = new FsReaderMock(@"Q:\Import\Root",
                isFileExists: fsPath => files.ContainsKey(fsPath),
                isDirectoryExists: fsPath => directories.Contains(fsPath),
                getFsDirectories: fsPath => GetDirectories(fsPath, directories),
                getFsFiles: fsPath => { if (fsPath == @"Q:\Import\Root") return new string[0]; throw new Exception("##"); },
                fsContentIsFileExists: null,
                fsContentCreateStreamReader: fsPath => new StringReader(files[fsPath]),
                fsContentCreateFileStream: null);

            var readings = new Dictionary<string, IContent>();

            // ACTION
            while (await reader.ReadAllAsync(Array.Empty<string>()))
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
        public async Task FsReader_Read_Root()
        {
            var directories = new[]
            {
                @"Q:\Import",
                @"Q:\Import\Root",
            };
            var files = new Dictionary<string, string>
            {
                {@"Q:\Import\Root.Content", "{'ContentType':'PortalRoot','ContentName':'Root','Fields':{},'Permissions':{" +
                                            "'IsInherited':true,'Entries':[{'Identity':'/Root/IMS/BuiltIn/Portal/Everyone'," +
                                            "'LocalOnly':true,'Permissions':{'See':'allow'}}]}}"}
            };

            var reader = new FsReaderMock(@"Q:\Import\Root",
                isFileExists: fsPath => files.ContainsKey(fsPath),
                isDirectoryExists: fsPath => directories.Contains(fsPath),
                getFsDirectories: fsPath => GetDirectories(fsPath, directories),
                getFsFiles: fsPath => { if (fsPath == @"Q:\Import\Root") return new string[0]; throw new Exception("##"); },
                fsContentIsFileExists: null,
                fsContentCreateStreamReader: fsPath => new StringReader(files[fsPath]),
                fsContentCreateFileStream: null);

            var readings = new Dictionary<string, IContent>();

            // ACTION
            while (await reader.ReadAllAsync(Array.Empty<string>()))
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

        //UNDONE: Missing test: ContentName meta-field overrides the name of the filesystem file/folder

        [TestMethod]
        public async Task FsReader_Read_Root_TypeFieldIsIrrelevant()
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


            var reader = new FsReaderMock(@"Q:\Import\Root",
                isFileExists: fsPath => files.ContainsKey(fsPath),
                isDirectoryExists: fsPath => directories.Contains(fsPath),
                getFsDirectories: fsPath => GetDirectories(fsPath, directories),
                getFsFiles: fsPath => { if (fsPath == @"Q:\Import\Root") return new string[0]; throw new Exception("##"); },
                fsContentIsFileExists: null,
                fsContentCreateStreamReader: fsPath => new StringReader(files[fsPath]),
                fsContentCreateFileStream: null);

            var readings = new Dictionary<string, IContent>();

            // ACTION
            while (await reader.ReadAllAsync(Array.Empty<string>()))
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
        [TestMethod]
        public async Task FsReader_Read_Folders()
        {
            var directories = new[]
            {
                @"Q:",
                @"Q:\Import",
                @"Q:\Import\Root",
            };
            var files = new Dictionary<string, string>
            {
                {@"Q:\Import\Root.Content", "{'ContentType':'PortalRoot','ContentName':'Root','Fields':{}}"},
                {@"Q:\Import\Root\F1.Content", "{'ContentType':'Folder','ContentName':'F1','Fields':{}}"},
                {@"Q:\Import\Root\F2.Content", "{'ContentType':'Folder','ContentName':'F2','Fields':{}}"}
            };

            var reader = new FsReaderMock(@"Q:\Import\Root",
                isFileExists: fsPath => files.ContainsKey(fsPath),
                isDirectoryExists: fsPath => directories.Contains(fsPath),
                getFsDirectories: fsPath => GetDirectories(fsPath, directories),
                getFsFiles: fsPath => GetFiles(fsPath, files.Keys.ToArray()),
                fsContentIsFileExists: null,
                fsContentCreateStreamReader: fsPath => new StringReader(files[fsPath]),
                fsContentCreateFileStream: null);

            var readings = new Dictionary<string, IContent>();

            // ACTION
            while (await reader.ReadAllAsync(Array.Empty<string>()))
                readings.Add(reader.RelativePath, reader.Content);

            // ASSERT
            var contents = readings.ToArray();
            Assert.AreEqual(3, contents.Length);

            Assert.AreEqual("", contents[0].Key);
            var content = contents[0].Value;
            Assert.AreEqual("Root", content.Name);
            Assert.AreEqual("PortalRoot", content.Type);
            Assert.AreEqual(0, content.FieldNames.Length);
            Assert.AreEqual(0, (await content.GetAttachmentsAsync()).Length);
            Assert.AreEqual(null, content.Permissions);

            Assert.AreEqual("F1", contents[1].Key);
            content = contents[1].Value;
            Assert.AreEqual("F1", content.Name);
            Assert.AreEqual("Folder", content.Type);
            Assert.AreEqual(0, content.FieldNames.Length);
            Assert.AreEqual(0, (await content.GetAttachmentsAsync()).Length);
            Assert.AreEqual(null, content.Permissions);

            Assert.AreEqual("F2", contents[2].Key);
            content = contents[2].Value;
            Assert.AreEqual("F2", content.Name);
            Assert.AreEqual("Folder", content.Type);
            Assert.AreEqual(0, content.FieldNames.Length);
            Assert.AreEqual(0, (await content.GetAttachmentsAsync()).Length);
            Assert.AreEqual(null, content.Permissions);
        }
        [TestMethod]
        public async Task FsReader_Read_File()
        {
            var directories = new[]
            {
                @"Q:",
                @"Q:\Import",
                @"Q:\Import\Root",
            };
            var files = new Dictionary<string, string>
            {
                {@"Q:\Import\Root.Content", "{'ContentType':'PortalRoot','ContentName':'Root','Fields':{}}"},
                {@"Q:\Import\Root\F1.txt.Content", "{'ContentType':'File','ContentName':'F1.txt','Fields':{'Binary':{'Attachment':'F1.txt'}}}"},
                {@"Q:\Import\Root\F1.txt", "Text content 1."}
            };

            var reader = new FsReaderMock(@"Q:\Import\Root",
                isFileExists: fsPath => files.ContainsKey(fsPath),
                isDirectoryExists: fsPath => directories.Contains(fsPath),
                getFsDirectories: fsPath => GetDirectories(fsPath, directories),
                getFsFiles: fsPath => GetFiles(fsPath, files.Keys.ToArray()),
                fsContentIsFileExists: fsPath => files.ContainsKey(fsPath),
                fsContentCreateStreamReader: fsPath => new StringReader(files[fsPath]),
                fsContentCreateFileStream: (fsPath, fileMode) =>
                {
                    if (fileMode != FileMode.Open)
                        throw new NotSupportedException($"Invalid 'fileMode': {fileMode}. Expected: 'Open'");
                    return files[fsPath].ToStream();
                });

            var readings = new Dictionary<string, IContent>();

            // ACTION
            while (await reader.ReadAllAsync(Array.Empty<string>()))
                readings.Add(reader.RelativePath, reader.Content);

            // ASSERT
            var contents = readings.ToArray();
            Assert.AreEqual(2, contents.Length);

            Assert.AreEqual("", contents[0].Key);
            Assert.AreEqual("Root", contents[0].Value.Name);

            Assert.AreEqual("F1.txt", contents[1].Key);
            var content = contents[1].Value;
            Assert.AreEqual("F1.txt", content.Name);
            Assert.AreEqual("File", content.Type);
            Assert.AreEqual(1, content.FieldNames.Length);
            Assert.AreEqual("Binary", content.FieldNames[0]);
            Assert.AreEqual("F1.txt", ((JObject)content["Binary"])["Attachment"]?.Value<string>());
            var attachments = await content.GetAttachmentsAsync();
            Assert.AreEqual(1, attachments.Length);
            Assert.AreEqual("Binary", attachments[0].FieldName);
            Assert.AreEqual("F1.txt", attachments[0].FileName);
            Assert.AreEqual("Text content 1.", ((MemoryStream)attachments[0].Stream).ReadAsString());
        }
        [TestMethod]
        public async Task FsReader_Read_File_TwoBinaries()
        {
            var directories = new[]
            {
                @"Q:",
                @"Q:\Import",
                @"Q:\Import\Root",
            };
            var files = new Dictionary<string, string>
            {
                {@"Q:\Import\Root.Content", "{'ContentType':'PortalRoot','ContentName':'Root','Fields':{}}"},
                {@"Q:\Import\Root\F1.txt.Content", "{'ContentType':'File','ContentName':'F1.txt'," +
                                                   "'Fields':{" +
                                                   "'Binary':{'Attachment':'F1.txt'}," +
                                                   "'Bin2':{'Attachment':'F1.txt.Bin2'}" +
                                                   "}}"},
                {@"Q:\Import\Root\F1.txt", "Text content 1."},
                {@"Q:\Import\Root\F1.txt.Bin2", "Text content 2."}
            };

            var reader = new FsReaderMock(@"Q:\Import\Root",
                isFileExists: fsPath => files.ContainsKey(fsPath),
                isDirectoryExists: fsPath => directories.Contains(fsPath),
                getFsDirectories: fsPath => GetDirectories(fsPath, directories),
                getFsFiles: fsPath => GetFiles(fsPath, files.Keys.ToArray()),
                fsContentIsFileExists: fsPath => files.ContainsKey(fsPath),
                fsContentCreateStreamReader: fsPath => new StringReader(files[fsPath]),
                fsContentCreateFileStream: (fsPath, fileMode) =>
                {
                    if (fileMode != FileMode.Open)
                        throw new NotSupportedException($"Invalid 'fileMode': {fileMode}. Expected: 'Open'");
                    return files[fsPath].ToStream();
                });

            var readings = new Dictionary<string, IContent>();

            // ACTION
            while (await reader.ReadAllAsync(Array.Empty<string>()))
                readings.Add(reader.RelativePath, reader.Content);

            // ASSERT
            var contents = readings.ToArray();
            Assert.AreEqual(2, contents.Length);

            Assert.AreEqual("", contents[0].Key);
            Assert.AreEqual("Root", contents[0].Value.Name);

            Assert.AreEqual("F1.txt", contents[1].Key);
            var content = contents[1].Value;
            Assert.AreEqual("F1.txt", content.Name);
            Assert.AreEqual("File", content.Type);
            Assert.AreEqual(2, content.FieldNames.Length);
            Assert.AreEqual("Binary", content.FieldNames[0]);
            Assert.AreEqual("F1.txt", ((JObject)content["Binary"])["Attachment"]?.Value<string>());
            Assert.AreEqual("Bin2", content.FieldNames[1]);
            Assert.AreEqual("F1.txt.Bin2", ((JObject)content["Bin2"])["Attachment"]?.Value<string>());
            var attachments = await content.GetAttachmentsAsync();
            Assert.AreEqual(2, attachments.Length);
            Assert.AreEqual("Binary", attachments[0].FieldName);
            Assert.AreEqual("F1.txt", attachments[0].FileName);
            Assert.AreEqual("Text content 1.", ((MemoryStream)attachments[0].Stream).ReadAsString());
            Assert.AreEqual("Bin2", attachments[1].FieldName);
            Assert.AreEqual("F1.txt.Bin2", attachments[1].FileName);
            Assert.AreEqual("Text content 2.", ((MemoryStream)attachments[1].Stream).ReadAsString());
        }

        /* ============================================================================ TREE */

        [TestMethod]
        public async Task FsReader_ReadTree_Root()
        {
            var directories = new[]
            {
                @"Q:",
                @"Q:\Import",
                @"Q:\Import\Root",
                @"Q:\Import\Root\System\F2",
                @"Q:\Import\Root\System\F1",
                @"Q:\Import\Root\Content",
                @"Q:\Import\Root\System\Schema\Aspects",
                @"Q:\Import\Root\System\Schema\ContentTypes\GenericContent",
                @"Q:\Import\Root\System\Schema\ContentTypes\GenericContent\Folder",
                @"Q:\Import\Root\System\Schema\ContentTypes",
                @"Q:\Import\Root\System\Schema",
                @"Q:\Import\Root\System",
                @"Q:\Import\Root\System\Settings",
            };
            var files = new Dictionary<string, string>
            {
                {@"Q:\Import\Root\F2.Content", "{'ContentType':'Folder','ContentName':'F2','Fields':{}}"},
                {@"Q:\Import\Root\System\F3.Content", "{'ContentType':'Folder','ContentName':'F3','Fields':{}}"},
                {@"Q:\Import\Root.Content", "{'ContentType':'PortalRoot','ContentName':'Root','Fields':{}}"},
                {@"Q:\Import\Root\F1.Content", "{'ContentType':'Folder','ContentName':'F1','Fields':{}}"},
                {@"Q:\Import\Root\System\Schema\ContentTypes.Content", "{'ContentType':'SystemFolder','ContentName':'ContentTypes','Fields':{}}"},
                {@"Q:\Import\Root\System\Schema.Content", "{'ContentType':'SystemFolder','ContentName':'Schema','Fields':{}}"},
                {@"Q:\Import\Root\System.Content", "{'ContentType':'SystemFolder','ContentName':'System','Fields':{}}"},
                {@"Q:\Import\Root\System\Settings\Settings1.Content", "{'ContentType':'Settings','ContentName':'Setting1','Fields':{}}"},
                {@"Q:\Import\Root\System\Settings\Settings2.Content", "{'ContentType':'Settings','ContentName':'Setting2','Fields':{}}"},
                {@"Q:\Import\Root\System\Schema\ContentTypes\GenericContent.Content", "{'ContentType':'ContentType','ContentName':'GenericContent','Fields':{}}"},
                {@"Q:\Import\Root\System\Schema\Aspects\Aspect1.Content", "{'ContentType':'Aspect','ContentName':'Aspect1','Fields':{}}"},
                {@"Q:\Import\Root\System\Schema\ContentTypes\GenericContent\Folder.Content", "{'ContentType':'ContentType','ContentName':'Folder','Fields':{}}"},
                {@"Q:\Import\Root\System\Schema\ContentTypes\GenericContent\File.Content", "{'ContentType':'ContentType','ContentName':'File','Fields':{}}"},
            };

            var reader = new FsReaderMock(@"Q:\Import\Root",
                isFileExists: fsPath => files.ContainsKey(fsPath),
                isDirectoryExists: fsPath => directories.Contains(fsPath),
                getFsDirectories: fsPath => GetDirectories(fsPath, directories),
                getFsFiles: fsPath => GetFiles(fsPath, files.Keys.ToArray()),
                fsContentIsFileExists: null,
                fsContentCreateStreamReader: fsPath => new StringReader(files[fsPath]),
                fsContentCreateFileStream: null);

            var readings = new Dictionary<string, IContent>();
            var actualRelativePaths = new List<string>();

            // ACTION
//UNDONE://///
            //while (await reader.ReadContentTypesAsync_DELETE())
            //{
            //    actualRelativePaths.Add(reader.RelativePath);
            //    readings.Add(reader.RelativePath, reader.Content);
            //}
            //while (await reader.ReadSettingsAsync_DELETE())
            //{
            //    actualRelativePaths.Add(reader.RelativePath);
            //    readings.Add(reader.RelativePath, reader.Content);
            //}
            //while (await reader.ReadAspectsAsync_DELETE())
            //{
            //    actualRelativePaths.Add(reader.RelativePath);
            //    readings.Add(reader.RelativePath, reader.Content);
            //}
            while (await reader.ReadAllAsync(Array.Empty<string>()))
            {
                actualRelativePaths.Add(reader.RelativePath);
                readings.Add(reader.RelativePath, reader.Content);
            }

            // ASSERT
            var contents = readings.ToArray();
            Assert.AreEqual(18, contents.Length);
            var expectedRelativePaths = new []
            {
                "",
                "Content",
                "System",
                "System/F1",
                "System/F2",
                "System/Schema",
                "System/Schema/Aspects",
                "System/Schema/Aspects/Aspect1",
                "System/Schema/ContentTypes",
                "System/Schema/ContentTypes/GenericContent",
                "System/Schema/ContentTypes/GenericContent/Folder",
                "System/Schema/ContentTypes/GenericContent/File",
                "System/Settings",
                "System/Settings/Settings1",
                "System/Settings/Settings2",
                "System/F3",
                "F1", // "F1" follows the "System" because "F1" is only a metafile.
                "F2",
            };
            AssertSequencesAreEqual(expectedRelativePaths, actualRelativePaths);

        }
        [TestMethod]
        public async Task FsReader_ReadTree_RootSystemSchema()
        {
            var directories = new[]
            {
                @"Q:",
                @"Q:\Import",
                @"Q:\Import\Root",
                @"Q:\Import\Root\System\F2",
                @"Q:\Import\Root\System\F1",
                @"Q:\Import\Root\Content",
                @"Q:\Import\Root\System\Schema\Aspects",
                @"Q:\Import\Root\System\Schema\ContentTypes\GenericContent",
                @"Q:\Import\Root\System\Schema\ContentTypes\GenericContent\Folder",
                @"Q:\Import\Root\System\Schema\ContentTypes",
                @"Q:\Import\Root\System\Schema",
                @"Q:\Import\Root\System",
                @"Q:\Import\Root\System\Settings",
            };
            var files = new Dictionary<string, string>
            {
                {@"Q:\Import\Root\F2.Content", "{'ContentType':'Folder','ContentName':'F2','Fields':{}}"},
                {@"Q:\Import\Root\System\F3.Content", "{'ContentType':'Folder','ContentName':'F3','Fields':{}}"},
                {@"Q:\Import\Root.Content", "{'ContentType':'PortalRoot','ContentName':'Root','Fields':{}}"},
                {@"Q:\Import\Root\F1.Content", "{'ContentType':'Folder','ContentName':'F1','Fields':{}}"},
                {@"Q:\Import\Root\System\Schema\ContentTypes.Content", "{'ContentType':'SystemFolder','ContentName':'ContentTypes','Fields':{}}"},
                {@"Q:\Import\Root\System\Schema.Content", "{'ContentType':'SystemFolder','ContentName':'Schema','Fields':{}}"},
                {@"Q:\Import\Root\System.Content", "{'ContentType':'SystemFolder','ContentName':'System','Fields':{}}"},
                {@"Q:\Import\Root\System\Settings\Settings1.Content", "{'ContentType':'Settings','ContentName':'Setting1','Fields':{}}"},
                {@"Q:\Import\Root\System\Settings\Settings2.Content", "{'ContentType':'Settings','ContentName':'Setting2','Fields':{}}"},
                {@"Q:\Import\Root\System\Schema\ContentTypes\GenericContent.Content", "{'ContentType':'ContentType','ContentName':'GenericContent','Fields':{}}"},
                {@"Q:\Import\Root\System\Schema\Aspects\Aspect1.Content", "{'ContentType':'Aspect','ContentName':'Aspect1','Fields':{}}"},
                {@"Q:\Import\Root\System\Schema\ContentTypes\GenericContent\Folder.Content", "{'ContentType':'ContentType','ContentName':'Folder','Fields':{}}"},
                {@"Q:\Import\Root\System\Schema\ContentTypes\GenericContent\File.Content", "{'ContentType':'ContentType','ContentName':'File','Fields':{}}"},
            };

            var reader = new FsReaderMock(@"Q:\Import\Root\System\Schema",
                isFileExists: fsPath => files.ContainsKey(fsPath),
                isDirectoryExists: fsPath => directories.Contains(fsPath),
                getFsDirectories: fsPath => GetDirectories(fsPath, directories),
                getFsFiles: fsPath => GetFiles(fsPath, files.Keys.ToArray()),
                fsContentIsFileExists: null,
                fsContentCreateStreamReader: fsPath => new StringReader(files[fsPath]),
                fsContentCreateFileStream: null);

            var readings = new Dictionary<string, IContent>();
            var actualRelativePaths = new List<string>();
//UNDONE://///
            // ACTION
            //while (await reader.ReadContentTypesAsync_DELETE())
            //{
            //    actualRelativePaths.Add(reader.RelativePath);
            //    readings.Add(reader.RelativePath, reader.Content);
            //}
            //while (await reader.ReadSettingsAsync_DELETE())
            //{
            //    actualRelativePaths.Add(reader.RelativePath);
            //    readings.Add(reader.RelativePath, reader.Content);
            //}
            //while (await reader.ReadAspectsAsync_DELETE())
            //{
            //    actualRelativePaths.Add(reader.RelativePath);
            //    readings.Add(reader.RelativePath, reader.Content);
            //}
            while (await reader.ReadAllAsync(Array.Empty<string>()))
            {
                actualRelativePaths.Add(reader.RelativePath);
                readings.Add(reader.RelativePath, reader.Content);
            }

            // ASSERT
            var contents = readings.ToArray();
            Assert.AreEqual(7, contents.Length);
            var expectedRelativePaths = new[]
            {
                "",
                "Aspects",
                "Aspects/Aspect1",
                "ContentTypes",
                "ContentTypes/GenericContent",
                "ContentTypes/GenericContent/Folder",
                "ContentTypes/GenericContent/File",
            };
            AssertSequencesAreEqual(expectedRelativePaths, actualRelativePaths);

        }

        [TestMethod]
        public async Task FsReader_ReadTree_Root_Skip()
        {
            var directories = new[]
            {
                @"Q:",
                @"Q:\Import",
                @"Q:\Import\Root",
                @"Q:\Import\Root\System\F2",
                @"Q:\Import\Root\System\F1",
                @"Q:\Import\Root\Content",
                @"Q:\Import\Root\System\Schema\Aspects",
                @"Q:\Import\Root\System\Schema\ContentTypes\GenericContent",
                @"Q:\Import\Root\System\Schema\ContentTypes\GenericContent\Folder",
                @"Q:\Import\Root\System\Schema\ContentTypes",
                @"Q:\Import\Root\System\Schema",
                @"Q:\Import\Root\System",
                @"Q:\Import\Root\System\Settings",
            };
            var files = new Dictionary<string, string>
            {
                {@"Q:\Import\Root\F2.Content", "{'ContentType':'Folder','ContentName':'F2','Fields':{}}"},
                {@"Q:\Import\Root\System\F3.Content", "{'ContentType':'Folder','ContentName':'F3','Fields':{}}"},
                {@"Q:\Import\Root.Content", "{'ContentType':'PortalRoot','ContentName':'Root','Fields':{}}"},
                {@"Q:\Import\Root\F1.Content", "{'ContentType':'Folder','ContentName':'F1','Fields':{}}"},
                {@"Q:\Import\Root\System\Schema\ContentTypes.Content", "{'ContentType':'SystemFolder','ContentName':'ContentTypes','Fields':{}}"},
                {@"Q:\Import\Root\System\Schema.Content", "{'ContentType':'SystemFolder','ContentName':'Schema','Fields':{}}"},
                {@"Q:\Import\Root\System.Content", "{'ContentType':'SystemFolder','ContentName':'System','Fields':{}}"},
                {@"Q:\Import\Root\System\Settings\Settings1.Content", "{'ContentType':'Settings','ContentName':'Setting1','Fields':{}}"},
                {@"Q:\Import\Root\System\Settings\Settings2.Content", "{'ContentType':'Settings','ContentName':'Setting2','Fields':{}}"},
                {@"Q:\Import\Root\System\Schema\ContentTypes\GenericContent.Content", "{'ContentType':'ContentType','ContentName':'GenericContent','Fields':{}}"},
                {@"Q:\Import\Root\System\Schema\Aspects\Aspect1.Content", "{'ContentType':'Aspect','ContentName':'Aspect1','Fields':{}}"},
                {@"Q:\Import\Root\System\Schema\ContentTypes\GenericContent\Folder.Content", "{'ContentType':'ContentType','ContentName':'Folder','Fields':{}}"},
                {@"Q:\Import\Root\System\Schema\ContentTypes\GenericContent\File.Content", "{'ContentType':'ContentType','ContentName':'File','Fields':{}}"},
            };

            var reader = new FsReaderMock(@"Q:\Import\Root",
                isFileExists: fsPath => files.ContainsKey(fsPath),
                isDirectoryExists: fsPath => directories.Contains(fsPath),
                getFsDirectories: fsPath => GetDirectories(fsPath, directories),
                getFsFiles: fsPath => GetFiles(fsPath, files.Keys.ToArray()),
                fsContentIsFileExists: null,
                fsContentCreateStreamReader: fsPath => new StringReader(files[fsPath]),
                fsContentCreateFileStream: null);

            var readings = new Dictionary<string, IContent>();
            var actualRelativePaths = new List<string>();

            // ACTION
            var skip = new[] {"System/Schema/ContentTypes", "System/Settings", "System/Schema/Aspects"};
            while (await reader.ReadAllAsync(skip))
            {
                actualRelativePaths.Add(reader.RelativePath);
                readings.Add(reader.RelativePath, reader.Content);
            }

            // ASSERT
            var contents = readings.ToArray();
            var expectedRelativePaths = new[]
            {
                "",
                "Content",
                "System",
                "System/F1",
                "System/F2",
                "System/Schema",
                "System/Schema/Aspects",
                "System/Schema/ContentTypes",
                "System/Settings",
                "System/F3",
                "F1", // "F1" follows the "System" because "F1" is only a metafile.
                "F2",
            };
            AssertSequencesAreEqual(expectedRelativePaths, actualRelativePaths);

        }
        [TestMethod]
        public async Task FsReader_ReadTree_System_Skip()
        {
            var directories = new[]
            {
                @"Q:",
                @"Q:\Import",
                @"Q:\Import\System\F2",
                @"Q:\Import\System\F1",
                @"Q:\Import\Content",
                @"Q:\Import\System\Schema\Aspects",
                @"Q:\Import\System\Schema\ContentTypes\GenericContent",
                @"Q:\Import\System\Schema\ContentTypes\GenericContent\Folder",
                @"Q:\Import\System\Schema\ContentTypes",
                @"Q:\Import\System\Schema",
                @"Q:\Import\System",
                @"Q:\Import\System\Settings",
            };
            var files = new Dictionary<string, string>
            {
                {@"Q:\Import\System\F3.Content", "{'ContentType':'Folder','ContentName':'F3','Fields':{}}"},
                {@"Q:\Import\System\Schema\ContentTypes.Content", "{'ContentType':'SystemFolder','ContentName':'ContentTypes','Fields':{}}"},
                {@"Q:\Import\System\Schema.Content", "{'ContentType':'SystemFolder','ContentName':'Schema','Fields':{}}"},
                {@"Q:\Import\System.Content", "{'ContentType':'SystemFolder','ContentName':'System','Fields':{}}"},
                {@"Q:\Import\System\Settings\Settings1.Content", "{'ContentType':'Settings','ContentName':'Setting1','Fields':{}}"},
                {@"Q:\Import\System\Settings\Settings2.Content", "{'ContentType':'Settings','ContentName':'Setting2','Fields':{}}"},
                {@"Q:\Import\System\Schema\ContentTypes\GenericContent.Content", "{'ContentType':'ContentType','ContentName':'GenericContent','Fields':{}}"},
                {@"Q:\Import\System\Schema\Aspects\Aspect1.Content", "{'ContentType':'Aspect','ContentName':'Aspect1','Fields':{}}"},
                {@"Q:\Import\System\Schema\ContentTypes\GenericContent\Folder.Content", "{'ContentType':'ContentType','ContentName':'Folder','Fields':{}}"},
                {@"Q:\Import\System\Schema\ContentTypes\GenericContent\File.Content", "{'ContentType':'ContentType','ContentName':'File','Fields':{}}"},
            };

            var reader = new FsReaderMock(@"Q:\Import\System",
                isFileExists: fsPath => files.ContainsKey(fsPath),
                isDirectoryExists: fsPath => directories.Contains(fsPath),
                getFsDirectories: fsPath => GetDirectories(fsPath, directories),
                getFsFiles: fsPath => GetFiles(fsPath, files.Keys.ToArray()),
                fsContentIsFileExists: null,
                fsContentCreateStreamReader: fsPath => new StringReader(files[fsPath]),
                fsContentCreateFileStream: null);

            var readings = new Dictionary<string, IContent>();
            var actualRelativePaths = new List<string>();

            // ACTION
            var skip = new[] { "Schema/ContentTypes", "Settings", "Schema/Aspects" };
            while (await reader.ReadAllAsync(skip))
            {
                actualRelativePaths.Add(reader.RelativePath);
                readings.Add(reader.RelativePath, reader.Content);
            }

            // ASSERT
            var contents = readings.ToArray();
            var expectedRelativePaths = new[]
            {
                "",
                "F1",
                "F2",
                "Schema",
                "Schema/Aspects",
                "Schema/ContentTypes",
                "Settings",
                "F3",
            };
            AssertSequencesAreEqual(expectedRelativePaths, actualRelativePaths);

        }
        [TestMethod]
        public async Task FsReader_ReadSubTree_Root_SystemSchemaContentTypes()
        {
            var directories = new[]
            {
                @"Q:",
                @"Q:\Import",
                @"Q:\Import\Root",
                @"Q:\Import\Root\System\F2",
                @"Q:\Import\Root\System\F1",
                @"Q:\Import\Root\Content",
                @"Q:\Import\Root\System\Schema\Aspects",
                @"Q:\Import\Root\System\Schema\ContentTypes\GenericContent",
                @"Q:\Import\Root\System\Schema\ContentTypes\GenericContent\Folder",
                @"Q:\Import\Root\System\Schema\ContentTypes",
                @"Q:\Import\Root\System\Schema",
                @"Q:\Import\Root\System",
                @"Q:\Import\Root\System\Settings",
            };
            var files = new Dictionary<string, string>
            {
                {@"Q:\Import\Root\F2.Content", "{'ContentType':'Folder','ContentName':'F2','Fields':{}}"},
                {@"Q:\Import\Root\System\F3.Content", "{'ContentType':'Folder','ContentName':'F3','Fields':{}}"},
                {@"Q:\Import\Root.Content", "{'ContentType':'PortalRoot','ContentName':'Root','Fields':{}}"},
                {@"Q:\Import\Root\F1.Content", "{'ContentType':'Folder','ContentName':'F1','Fields':{}}"},
                {@"Q:\Import\Root\System\Schema\ContentTypes.Content", "{'ContentType':'SystemFolder','ContentName':'ContentTypes','Fields':{}}"},
                {@"Q:\Import\Root\System\Schema.Content", "{'ContentType':'SystemFolder','ContentName':'Schema','Fields':{}}"},
                {@"Q:\Import\Root\System.Content", "{'ContentType':'SystemFolder','ContentName':'System','Fields':{}}"},
                {@"Q:\Import\Root\System\Settings\Settings1.Content", "{'ContentType':'Settings','ContentName':'Setting1','Fields':{}}"},
                {@"Q:\Import\Root\System\Settings\Settings2.Content", "{'ContentType':'Settings','ContentName':'Setting2','Fields':{}}"},
                {@"Q:\Import\Root\System\Schema\ContentTypes\GenericContent.Content", "{'ContentType':'ContentType','ContentName':'GenericContent','Fields':{}}"},
                {@"Q:\Import\Root\System\Schema\Aspects\Aspect1.Content", "{'ContentType':'Aspect','ContentName':'Aspect1','Fields':{}}"},
                {@"Q:\Import\Root\System\Schema\ContentTypes\GenericContent\Folder.Content", "{'ContentType':'ContentType','ContentName':'Folder','Fields':{}}"},
                {@"Q:\Import\Root\System\Schema\ContentTypes\GenericContent\File.Content", "{'ContentType':'ContentType','ContentName':'File','Fields':{}}"},
            };

            var reader = new FsReaderMock(@"Q:\Import\Root",
                isFileExists: fsPath => files.ContainsKey(fsPath),
                isDirectoryExists: fsPath => directories.Contains(fsPath),
                getFsDirectories: fsPath => GetDirectories(fsPath, directories),
                getFsFiles: fsPath => GetFiles(fsPath, files.Keys.ToArray()),
                fsContentIsFileExists: null,
                fsContentCreateStreamReader: fsPath => new StringReader(files[fsPath]),
                fsContentCreateFileStream: null);

            var readings = new Dictionary<string, IContent>();
            var actualRelativePaths = new List<string>();

            // ACTION
            //var skip = new[] { "System/Schema/ContentTypes", "System/Settings", "System/Schema/Aspects" };
            while (await reader.ReadSubTreeAsync("System/Schema/ContentTypes"))
            {
                actualRelativePaths.Add(reader.RelativePath);
                readings.Add(reader.RelativePath, reader.Content);
            }

            // ASSERT
            var contents = readings.ToArray();
            var expectedRelativePaths = new[]
            {
                "System/Schema/ContentTypes",
                "System/Schema/ContentTypes/GenericContent",
                "System/Schema/ContentTypes/GenericContent/Folder",
                "System/Schema/ContentTypes/GenericContent/File",
            };
            AssertSequencesAreEqual(expectedRelativePaths, actualRelativePaths);

        }
        [TestMethod]
        public async Task FsReader_ReadSubTree_Root_SystemSettings()
        {
            var directories = new[]
            {
                @"Q:",
                @"Q:\Import",
                @"Q:\Import\Root",
                @"Q:\Import\Root\System\F2",
                @"Q:\Import\Root\System\F1",
                @"Q:\Import\Root\Content",
                @"Q:\Import\Root\System\Schema\Aspects",
                @"Q:\Import\Root\System\Schema\ContentTypes\GenericContent",
                @"Q:\Import\Root\System\Schema\ContentTypes\GenericContent\Folder",
                @"Q:\Import\Root\System\Schema\ContentTypes",
                @"Q:\Import\Root\System\Schema",
                @"Q:\Import\Root\System",
                @"Q:\Import\Root\System\Settings",
            };
            var files = new Dictionary<string, string>
            {
                {@"Q:\Import\Root\F2.Content", "{'ContentType':'Folder','ContentName':'F2','Fields':{}}"},
                {@"Q:\Import\Root\System\F3.Content", "{'ContentType':'Folder','ContentName':'F3','Fields':{}}"},
                {@"Q:\Import\Root.Content", "{'ContentType':'PortalRoot','ContentName':'Root','Fields':{}}"},
                {@"Q:\Import\Root\F1.Content", "{'ContentType':'Folder','ContentName':'F1','Fields':{}}"},
                {@"Q:\Import\Root\System\Schema\ContentTypes.Content", "{'ContentType':'SystemFolder','ContentName':'ContentTypes','Fields':{}}"},
                {@"Q:\Import\Root\System\Schema.Content", "{'ContentType':'SystemFolder','ContentName':'Schema','Fields':{}}"},
                {@"Q:\Import\Root\System.Content", "{'ContentType':'SystemFolder','ContentName':'System','Fields':{}}"},
                {@"Q:\Import\Root\System\Settings\Settings1.Content", "{'ContentType':'Settings','ContentName':'Setting1','Fields':{}}"},
                {@"Q:\Import\Root\System\Settings\Settings2.Content", "{'ContentType':'Settings','ContentName':'Setting2','Fields':{}}"},
                {@"Q:\Import\Root\System\Schema\ContentTypes\GenericContent.Content", "{'ContentType':'ContentType','ContentName':'GenericContent','Fields':{}}"},
                {@"Q:\Import\Root\System\Schema\Aspects\Aspect1.Content", "{'ContentType':'Aspect','ContentName':'Aspect1','Fields':{}}"},
                {@"Q:\Import\Root\System\Schema\ContentTypes\GenericContent\Folder.Content", "{'ContentType':'ContentType','ContentName':'Folder','Fields':{}}"},
                {@"Q:\Import\Root\System\Schema\ContentTypes\GenericContent\File.Content", "{'ContentType':'ContentType','ContentName':'File','Fields':{}}"},
            };

            var reader = new FsReaderMock(@"Q:\Import\Root",
                isFileExists: fsPath => files.ContainsKey(fsPath),
                isDirectoryExists: fsPath => directories.Contains(fsPath),
                getFsDirectories: fsPath => GetDirectories(fsPath, directories),
                getFsFiles: fsPath => GetFiles(fsPath, files.Keys.ToArray()),
                fsContentIsFileExists: null,
                fsContentCreateStreamReader: fsPath => new StringReader(files[fsPath]),
                fsContentCreateFileStream: null);

            var readings = new Dictionary<string, IContent>();
            var actualRelativePaths = new List<string>();

            // ACTION
            //var skip = new[] { "System/Schema/ContentTypes", "System/Settings", "System/Schema/Aspects" };
            while (await reader.ReadSubTreeAsync("System/Settings"))
            {
                actualRelativePaths.Add(reader.RelativePath);
                readings.Add(reader.RelativePath, reader.Content);
            }

            // ASSERT
            var contents = readings.ToArray();
            var expectedRelativePaths = new[]
            {
                "System/Settings",
                "System/Settings/Settings1",
                "System/Settings/Settings2",
            };
            AssertSequencesAreEqual(expectedRelativePaths, actualRelativePaths);

        }
        [TestMethod]
        public async Task FsReader_ReadSubTree_System_SchemaContentTypes()
        {
            var directories = new[]
            {
                @"Q:",
                @"Q:\Import",
                @"Q:\Import\System\F2",
                @"Q:\Import\System\F1",
                @"Q:\Import\System\Schema\Aspects",
                @"Q:\Import\System\Schema\ContentTypes\GenericContent",
                @"Q:\Import\System\Schema\ContentTypes\GenericContent\Folder",
                @"Q:\Import\System\Schema\ContentTypes",
                @"Q:\Import\System\Schema",
                @"Q:\Import\System",
                @"Q:\Import\System\Settings",
            };
            var files = new Dictionary<string, string>
            {
                {@"Q:\Import\System\F3.Content", "{'ContentType':'Folder','ContentName':'F3','Fields':{}}"},
                {@"Q:\Import\System\Schema\ContentTypes.Content", "{'ContentType':'SystemFolder','ContentName':'ContentTypes','Fields':{}}"},
                {@"Q:\Import\System\Schema.Content", "{'ContentType':'SystemFolder','ContentName':'Schema','Fields':{}}"},
                {@"Q:\Import\System.Content", "{'ContentType':'SystemFolder','ContentName':'System','Fields':{}}"},
                {@"Q:\Import\System\Settings\Settings1.Content", "{'ContentType':'Settings','ContentName':'Setting1','Fields':{}}"},
                {@"Q:\Import\System\Settings\Settings2.Content", "{'ContentType':'Settings','ContentName':'Setting2','Fields':{}}"},
                {@"Q:\Import\System\Schema\ContentTypes\GenericContent.Content", "{'ContentType':'ContentType','ContentName':'GenericContent','Fields':{}}"},
                {@"Q:\Import\System\Schema\Aspects\Aspect1.Content", "{'ContentType':'Aspect','ContentName':'Aspect1','Fields':{}}"},
                {@"Q:\Import\System\Schema\ContentTypes\GenericContent\Folder.Content", "{'ContentType':'ContentType','ContentName':'Folder','Fields':{}}"},
                {@"Q:\Import\System\Schema\ContentTypes\GenericContent\File.Content", "{'ContentType':'ContentType','ContentName':'File','Fields':{}}"},
            };

            var reader = new FsReaderMock(@"Q:\Import\System",
                isFileExists: fsPath => files.ContainsKey(fsPath),
                isDirectoryExists: fsPath => directories.Contains(fsPath),
                getFsDirectories: fsPath => GetDirectories(fsPath, directories),
                getFsFiles: fsPath => GetFiles(fsPath, files.Keys.ToArray()),
                fsContentIsFileExists: null,
                fsContentCreateStreamReader: fsPath => new StringReader(files[fsPath]),
                fsContentCreateFileStream: null);

            var readings = new Dictionary<string, IContent>();
            var actualRelativePaths = new List<string>();

            // ACTION
            //var skip = new[] { "System/Schema/ContentTypes", "System/Settings", "System/Schema/Aspects" };
            while (await reader.ReadSubTreeAsync("Schema/ContentTypes"))
            {
                actualRelativePaths.Add(reader.RelativePath);
                readings.Add(reader.RelativePath, reader.Content);
            }

            // ASSERT
            var contents = readings.ToArray();
            var expectedRelativePaths = new[]
            {
                "Schema/ContentTypes",
                "Schema/ContentTypes/GenericContent",
                "Schema/ContentTypes/GenericContent/Folder",
                "Schema/ContentTypes/GenericContent/File",
            };
            AssertSequencesAreEqual(expectedRelativePaths, actualRelativePaths);

        }

        private void AssertSequencesAreEqual(IEnumerable<object> expected, IEnumerable<object> actual)
        {
            var exp = string.Join(", ", expected.Select(x => x.ToString()));
            var act = string.Join(", ", actual.Select(x => x.ToString()));
            Assert.AreEqual(exp, act);
        }

        /* ============================================================================ SELF TESTS */

        [TestMethod]
        public void FsReader_SelfTest_GetDirectories()
        {
            var directories = new[]
            {
                @"Q:",
                @"Q:\Import",
                @"Q:\Import\Root",
                @"Q:\Import\Root\F1",
                @"Q:\Import\Root\F1\F1",
                @"Q:\Import\Root\F1\F1\F1",
                @"Q:\Import\Root\F1\F1\F2",
                @"Q:\Import\Root\F1\F1\F3",
                @"Q:\Import\Root\F1\F1\F4",
                @"Q:\Import\Root\F1\F2",
                @"Q:\Import\Root\F1\F3",
                @"Q:\Import\Root\F2",
            };

            // TEST CASE 1
            var dirs = GetDirectories(@"Q:\Export", directories);
            Assert.AreEqual(0, dirs.Length);

            // TEST CASE 2
            dirs = GetDirectories(@"Q:\Import", directories);
            Assert.AreEqual(1, dirs.Length);
            Assert.AreEqual(@"Q:\Import\Root", dirs[0]);

            // TEST CASE 3
            dirs = GetDirectories(@"Q:\Import\Root", directories);
            Assert.AreEqual(2, dirs.Length);
            Assert.AreEqual(@"Q:\Import\Root\F1", dirs[0]);
            Assert.AreEqual(@"Q:\Import\Root\F2", dirs[1]);

            // TEST CASE 4
            dirs = GetDirectories(@"Q:\Import\Root\F1", directories);
            Assert.AreEqual(3, dirs.Length);
            Assert.AreEqual(@"Q:\Import\Root\F1\F1", dirs[0]);
            Assert.AreEqual(@"Q:\Import\Root\F1\F2", dirs[1]);
            Assert.AreEqual(@"Q:\Import\Root\F1\F3", dirs[2]);

            // TEST CASE 5
            dirs = GetDirectories(@"Q:\Import\Root\F1\F1", directories);
            Assert.AreEqual(4, dirs.Length);
            Assert.AreEqual(@"Q:\Import\Root\F1\F1\F1", dirs[0]);
            Assert.AreEqual(@"Q:\Import\Root\F1\F1\F2", dirs[1]);
            Assert.AreEqual(@"Q:\Import\Root\F1\F1\F3", dirs[2]);
            Assert.AreEqual(@"Q:\Import\Root\F1\F1\F4", dirs[3]);
        }
        private string[] GetDirectories(string fsPath, string[] directories)
        {
            var depth = fsPath.Split('\\').Length;
            var dirs = directories
                .Where(d => d.StartsWith(fsPath + "\\"))
                .Where(d => d.Split('\\').Length == depth + 1)
                .ToArray();
            return dirs;
        }

        [TestMethod]
        public void FsReader_SelfTest_GetFiles()
        {
            var files = new[]
            {
                @"Q:",
                @"Q:\Import",
                @"Q:\Import\Root.Content",
                @"Q:\Import\Root\F1.Content",
                @"Q:\Import\Root\F1\F1.Content",
                @"Q:\Import\Root\F1\F1\F1.Content",
                @"Q:\Import\Root\F1\F1\F2.Content",
                @"Q:\Import\Root\F1\F1\F3.Content",
                @"Q:\Import\Root\F1\F1\F4.Content",
                @"Q:\Import\Root\F1\F2.Content",
                @"Q:\Import\Root\F1\F3.Content",
                @"Q:\Import\Root\F2.Content",
            };

            // TEST CASE 1
            var f = GetFiles(@"Q:\Export", files);
            Assert.AreEqual(0, f.Length);

            // TEST CASE 2
            f = GetFiles(@"Q:\Import", files);
            Assert.AreEqual(1, f.Length);
            Assert.AreEqual(@"Q:\Import\Root.Content", f[0]);

            // TEST CASE 3
            f = GetFiles(@"Q:\Import\Root", files);
            Assert.AreEqual(2, f.Length);
            Assert.AreEqual(@"Q:\Import\Root\F1.Content", f[0]);
            Assert.AreEqual(@"Q:\Import\Root\F2.Content", f[1]);

            // TEST CASE 4
            f = GetFiles(@"Q:\Import\Root\F1", files);
            Assert.AreEqual(3, f.Length);
            Assert.AreEqual(@"Q:\Import\Root\F1\F1.Content", f[0]);
            Assert.AreEqual(@"Q:\Import\Root\F1\F2.Content", f[1]);
            Assert.AreEqual(@"Q:\Import\Root\F1\F3.Content", f[2]);

            // TEST CASE 5
            f = GetFiles(@"Q:\Import\Root\F1\F1", files);
            Assert.AreEqual(4, f.Length);
            Assert.AreEqual(@"Q:\Import\Root\F1\F1\F1.Content", f[0]);
            Assert.AreEqual(@"Q:\Import\Root\F1\F1\F2.Content", f[1]);
            Assert.AreEqual(@"Q:\Import\Root\F1\F1\F3.Content", f[2]);
            Assert.AreEqual(@"Q:\Import\Root\F1\F1\F4.Content", f[3]);
        }
        private string[] GetFiles(string fsPath, string[] files)
        {
            var depth = fsPath.Split('\\').Length;
            var f = files
                .Where(d => d.StartsWith(fsPath + "\\"))
                .Where(d => d.EndsWith(".Content"))
                .Where(d => d.Split('\\').Length == depth + 1)
                .ToArray();
            return f;
        }
    }
}
