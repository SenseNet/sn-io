﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SenseNet.IO.Tests.Implementations;

namespace SenseNet.IO.Tests
{
    [TestClass]
    public class ContentFlowTests2 : TestBase
    {
        private class ContentFlow2Mock : ContentFlow2
        {
            public List<string> Log { get; } = new List<string>();
            public List<TransferTask> TransferTasks { get; } = new List<TransferTask>();

            public ContentFlow2Mock(IContentReader reader, ISnRepositoryWriter writer) : base(reader, writer) { }
            protected override void WriteLog(string entry) { Log.Add(entry); }

            protected override void WriteTask(WriterState state)
            {
                TransferTasks.Add(new TransferTask
                {
                    ReaderPath = state.ReaderPath,
                    WriterPath = state.WriterPath,
                    BrokenReferences = state.BrokenReferences.ToArray(),
                    RetryPermissions = state.RetryPermissions
                });
            }
            protected override int LoadTaskCount() { return TransferTasks.Count; }
            protected override IEnumerable<TransferTask> LoadTasks() { return TransferTasks.ToArray(); }
        }

        private class ContentFlowMock : ContentFlow
        {
            public List<string> Log { get; } = new List<string>();
            private List<TransferTask> Tasks { get; } = new List<TransferTask>();
            public ContentFlowMock(IContentReader reader, IContentWriter writer) : base(reader, writer) { }
            protected override void WriteLog(string entry)
            {
                Log.Add(entry);
            }
            protected override void WriteTask(WriterState state)
            {
                Tasks.Add(new TransferTask
                {
                    ReaderPath = state.ReaderPath,
                    WriterPath = state.WriterPath,
                    BrokenReferences = state.BrokenReferences.ToArray(),
                    RetryPermissions = state.RetryPermissions
                });
            }
            protected override int LoadTaskCount()
            {
                return Tasks.Count;
            }
            protected override IEnumerable<TransferTask> LoadTasks()
            {
                foreach (var task in Tasks)
                {
                    yield return new TransferTask
                    {
                        ReaderPath = task.ReaderPath,
                        WriterPath = task.WriterPath,
                        BrokenReferences = task.BrokenReferences.ToArray(),
                        RetryPermissions = task.RetryPermissions
                    };
                }
            }
        }

        /* ----------------------------------------------------------------------- q:\io\Root */

        [TestMethod]
        public async Task ContentFlow2_Root()
        {
            var sourceTree = CreateSourceTree(@"\");
            var targetTree = CreateTree(new[] { "/Root", "/Root/IMS" });
            var targetStates = new Dictionary<string, WriterState>();

            // ACTION
            var reader = new TestContentReader(@"q:\io\Root", sourceTree);
            var writer = new TestRepositoryWriter(targetTree, targetStates);
            var flow = new ContentFlow2Mock(reader, writer);
            var progress = new TestProgress();
            await flow.TransferAsync(progress);

            // ASSERT
            var expected = sourceTree.Keys
                .Select(x=>x.Substring("q:\\io".Length).Replace('\\', '/'))
                .OrderBy(x => x)
                .ToArray();
            var actual = targetTree.Keys
                .OrderBy(x => x)
                .ToArray();
            AssertSequencesAreEqual(expected, actual);

            expected = new[]
            {
                "------------ TRANSFER CONTENT TYPES ------------",
                "Created  /Root/System/Schema/ContentTypes/ContentType-1",
                "Created  /Root/System/Schema/ContentTypes/ContentType-1/ContentType-3",
                "Created  /Root/System/Schema/ContentTypes/ContentType-1/ContentType-4",
                "Created  /Root/System/Schema/ContentTypes/ContentType-1/ContentType-5",
                "Created  /Root/System/Schema/ContentTypes/ContentType-1/ContentType-5/ContentType-6",
                "Created  /Root/System/Schema/ContentTypes/ContentType-2",
                "------------ TRANSFER SETTINGS ------------",
                "Created  /Root/System/Settings/Settings-1.settings",
                "Created  /Root/System/Settings/Settings-2.settings",
                "Created  /Root/System/Settings/Settings-3.settings",
                "------------ TRANSFER ASPECT DEFINITIONS ------------",
                "Created  /Root/System/Schema/Aspects/Aspect-1",
                "Created  /Root/System/Schema/Aspects/Aspect-2",
                "------------ TRANSFER CONTENTS ------------",
                "Updated  /Root",
                "Created  /Root/(apps)",
                "Created  /Root/Content",
                "Created  /Root/Content/Workspace-1",
                "Created  /Root/Content/Workspace-1/DocLib-1",
                "Created  /Root/Content/Workspace-1/DocLib-1/Folder-1",
                "Created  /Root/Content/Workspace-1/DocLib-1/Folder-1/File-1.xlsx",
                "Created  /Root/Content/Workspace-1/DocLib-1/Folder-1/File-2.docx",
                "Created  /Root/Content/Workspace-1/DocLib-1/Folder-2",
                "Created  /Root/Content/Workspace-2",
                "Updated  /Root/IMS",
                "Created  /Root/IMS/BuiltIn",
                "Created  /Root/IMS/BuiltIn/Portal",
                "Created  /Root/IMS/BuiltIn/Portal/Group-3",
                "Created  /Root/IMS/BuiltIn/Portal/User-3",
                "Created  /Root/IMS/Public",
                "Created  /Root/IMS/Public/Group-4",
                "Created  /Root/IMS/Public/User-4",
                "Updated  /Root/System",
                "Updated  /Root/System/Schema",
                "Updated  /Root/System/Schema/Aspects",
                "Updated  /Root/System/Schema/ContentTypes",
                "Updated  /Root/System/Settings",
            };
            actual = flow.Log.ToArray();
            AssertSequencesAreEqual(expected, actual);
        }
        [TestMethod]
        public async Task ContentFlow2_RootSystem()
        {
            var sourceTree = CreateSourceTree(@"\");
            var targetTree = CreateTree(new[] {"/Root", "/Root/System"});
            var targetStates = new Dictionary<string, WriterState>();

            // ACTION
            var reader = new TestContentReader(@"q:\io\Root\System", sourceTree);
            var writer = new TestRepositoryWriter(targetTree, targetStates, "/Root");
            var flow = new ContentFlow2Mock(reader, writer);
            var progress = new TestProgress();
            await flow.TransferAsync(progress);

            // ASSERT
            var expected = sourceTree.Keys
                .Where(x=> x == @"q:\io\Root" ||
                           x == @"q:\io\Root\System" ||
                           x.StartsWith(@"q:\io\Root\System\"))
                .Select(x => x.Substring(@"q:\io".Length).Replace('\\', '/'))
                .OrderBy(x => x)
                .ToArray();
            var actual = targetTree.Keys
                .OrderBy(x => x)
                .ToArray();
            AssertSequencesAreEqual(expected, actual);

            expected = new[]
            {
                "------------ TRANSFER CONTENT TYPES ------------",
                "Created  /Root/System/Schema/ContentTypes/ContentType-1",
                "Created  /Root/System/Schema/ContentTypes/ContentType-1/ContentType-3",
                "Created  /Root/System/Schema/ContentTypes/ContentType-1/ContentType-4",
                "Created  /Root/System/Schema/ContentTypes/ContentType-1/ContentType-5",
                "Created  /Root/System/Schema/ContentTypes/ContentType-1/ContentType-5/ContentType-6",
                "Created  /Root/System/Schema/ContentTypes/ContentType-2",
                "------------ TRANSFER SETTINGS ------------",
                "Created  /Root/System/Settings/Settings-1.settings",
                "Created  /Root/System/Settings/Settings-2.settings",
                "Created  /Root/System/Settings/Settings-3.settings",
                "------------ TRANSFER ASPECT DEFINITIONS ------------",
                "Created  /Root/System/Schema/Aspects/Aspect-1",
                "Created  /Root/System/Schema/Aspects/Aspect-2",
                "------------ TRANSFER CONTENTS ------------",
                "Updated  /Root/System",
                "Updated  /Root/System/Schema",
                "Updated  /Root/System/Schema/Aspects",
                "Updated  /Root/System/Schema/ContentTypes",
                "Updated  /Root/System/Settings",
            };
            actual = flow.Log.ToArray();
            AssertSequencesAreEqual(expected, actual);
        }
        [TestMethod]
        public async Task ContentFlow2_RootSystemSchema()
        {
            var sourceTree = CreateSourceTree(@"\");
            var targetTree = CreateTree(new[] { "/Root" });
            var targetStates = new Dictionary<string, WriterState>();

            // ACTION
            var reader = new TestContentReader(@"q:\io\Root\System\Schema", sourceTree);
            var writer = new TestRepositoryWriter(targetTree, targetStates, "/Root/System");
            var flow = new ContentFlow2Mock(reader, writer);
            var progress = new TestProgress();
            await flow.TransferAsync(progress);

            // ASSERT
            var expected = sourceTree.Keys
                .Where(x => x == @"q:\io\Root" ||
                            x == @"q:\io\Root\System" ||
                            x == @"q:\io\Root\System\Schema" ||
                            x.StartsWith(@"q:\io\Root\System\Schema\"))
                .Select(x => x.Substring(@"q:\io".Length).Replace('\\', '/'))
                .OrderBy(x => x)
                .ToArray();
            var actual = targetTree.Keys
                .OrderBy(x => x)
                .ToArray();
            AssertSequencesAreEqual(expected, actual);

            expected = new[]
            {
                "------------ TRANSFER CONTENT TYPES ------------",
                "Created  /Root/System/Schema/ContentTypes/ContentType-1",
                "Created  /Root/System/Schema/ContentTypes/ContentType-1/ContentType-3",
                "Created  /Root/System/Schema/ContentTypes/ContentType-1/ContentType-4",
                "Created  /Root/System/Schema/ContentTypes/ContentType-1/ContentType-5",
                "Created  /Root/System/Schema/ContentTypes/ContentType-1/ContentType-5/ContentType-6",
                "Created  /Root/System/Schema/ContentTypes/ContentType-2",
                "------------ TRANSFER ASPECT DEFINITIONS ------------",
                "Created  /Root/System/Schema/Aspects/Aspect-1",
                "Created  /Root/System/Schema/Aspects/Aspect-2",
                "------------ TRANSFER CONTENTS ------------",
                "Updated  /Root/System/Schema",
                "Updated  /Root/System/Schema/Aspects",
                "Updated  /Root/System/Schema/ContentTypes",
            };
            actual = flow.Log.ToArray();
            AssertSequencesAreEqual(expected, actual);
        }
        [TestMethod]
        public async Task ContentFlow2_RootSystemSchemaContentTypes()
        {
            var sourceTree = CreateSourceTree(@"\");
            var targetTree = CreateTree(new[] { "/Root" });
            var targetStates = new Dictionary<string, WriterState>();

            // ACTION
            var reader = new TestContentReader(@"q:\io\Root\System\Schema\ContentTypes", sourceTree);
            var writer = new TestRepositoryWriter(targetTree, targetStates, "/Root/System/Schema");
            var flow = new ContentFlow2Mock(reader, writer);
            var progress = new TestProgress();
            await flow.TransferAsync(progress);

            // ASSERT
            var expected = sourceTree.Keys
                .Where(x => x == @"q:\io\Root" ||
                            x == @"q:\io\Root\System" ||
                            x == @"q:\io\Root\System\Schema" ||
                            x == @"q:\io\Root\System\Schema\ContentTypes" ||
                            x.StartsWith(@"q:\io\Root\System\Schema\ContentTypes\"))
                .Select(x => x.Substring("q:\\io".Length).Replace('\\', '/'))
                .OrderBy(x => x)
                .ToArray();
            var actual = targetTree.Keys
                .OrderBy(x => x)
                .ToArray();
            AssertSequencesAreEqual(expected, actual);

            expected = new[]
            {
                "------------ TRANSFER CONTENT TYPES ------------",
                "Created  /Root/System/Schema/ContentTypes/ContentType-1",
                "Created  /Root/System/Schema/ContentTypes/ContentType-1/ContentType-3",
                "Created  /Root/System/Schema/ContentTypes/ContentType-1/ContentType-4",
                "Created  /Root/System/Schema/ContentTypes/ContentType-1/ContentType-5",
                "Created  /Root/System/Schema/ContentTypes/ContentType-1/ContentType-5/ContentType-6",
                "Created  /Root/System/Schema/ContentTypes/ContentType-2",
                "------------ TRANSFER CONTENTS ------------",
                "Updated  /Root/System/Schema/ContentTypes",
            };
            actual = flow.Log.ToArray();
            AssertSequencesAreEqual(expected, actual);
        }
        [TestMethod]
        public async Task ContentFlow2_RootSystemSchemaAspects()
        {
            var sourceTree = CreateSourceTree(@"\");
            var targetTree = CreateTree(new[] { "/Root" });
            var targetStates = new Dictionary<string, WriterState>();

            // ACTION
            var reader = new TestContentReader(@"q:\io\Root\System\Schema\Aspects", sourceTree);
            var writer = new TestRepositoryWriter(targetTree, targetStates, "/Root/System/Schema");
            var flow = new ContentFlow2Mock(reader, writer);
            var progress = new TestProgress();
            await flow.TransferAsync(progress);

            // ASSERT
            var expected = sourceTree.Keys
                .Where(x => x == @"q:\io\Root" ||
                            x == @"q:\io\Root\System" ||
                            x == @"q:\io\Root\System\Schema" ||
                            x == @"q:\io\Root\System\Schema\Aspects" ||
                            x.StartsWith(@"q:\io\Root\System\Schema\Aspects\"))
                .Select(x => x.Substring("q:\\io".Length).Replace('\\', '/'))
                .OrderBy(x => x)
                .ToArray();
            var actual = targetTree.Keys
                .OrderBy(x => x)
                .ToArray();
            AssertSequencesAreEqual(expected, actual);

            expected = new[]
            {
                "------------ TRANSFER ASPECT DEFINITIONS ------------",
                "Created  /Root/System/Schema/Aspects/Aspect-1",
                "Created  /Root/System/Schema/Aspects/Aspect-2",
                "------------ TRANSFER CONTENTS ------------",
                "Updated  /Root/System/Schema/Aspects",
            };
            actual = flow.Log.ToArray();
            AssertSequencesAreEqual(expected, actual);
        }
        [TestMethod]
        public async Task ContentFlow2_RootSystemSettings()
        {
            var sourceTree = CreateSourceTree(@"\");
            var targetTree = CreateTree(new[] { "/Root", "/Root/System" });
            var targetStates = new Dictionary<string, WriterState>();

            // ACTION
            var reader = new TestContentReader(@"q:\io\Root\System\Settings", sourceTree);
            var writer = new TestRepositoryWriter(targetTree, targetStates, "/Root/System");
            var flow = new ContentFlow2Mock(reader, writer);
            var progress = new TestProgress();
            await flow.TransferAsync(progress);

            // ASSERT
            var expected = sourceTree.Keys
                .Where(x => x == @"q:\io\Root" ||
                            x == @"q:\io\Root\System" ||
                            x == @"q:\io\Root\System\Settings" ||
                            x.StartsWith(@"q:\io\Root\System\Settings\"))
                .Select(x => x.Substring("q:\\io".Length).Replace('\\', '/'))
                .OrderBy(x => x)
                .ToArray();
            var actual = targetTree.Keys
                .OrderBy(x => x)
                .ToArray();
            AssertSequencesAreEqual(expected, actual);

            expected = new[]
            {
                "------------ TRANSFER SETTINGS ------------",
                "Created  /Root/System/Settings/Settings-1.settings",
                "Created  /Root/System/Settings/Settings-2.settings",
                "Created  /Root/System/Settings/Settings-3.settings",
                "------------ TRANSFER CONTENTS ------------",
                "Updated  /Root/System/Settings",
            };
            actual = flow.Log.ToArray();
            AssertSequencesAreEqual(expected, actual);
        }
        [TestMethod]
        public async Task ContentFlow2_RootContent()
        {
            var sourceTree = CreateSourceTree(@"\");
            var targetTree = CreateTree(new[] { "/Root" });
            var targetStates = new Dictionary<string, WriterState>();

            // ACTION
            var reader = new TestContentReader(@"q:\io\Root\Content", sourceTree);
            var writer = new TestRepositoryWriter(targetTree, targetStates, "/Root");
            var flow = new ContentFlow2Mock(reader, writer);
            var progress = new TestProgress();
            await flow.TransferAsync(progress);

            // ASSERT
            var expected = sourceTree.Keys
                .Where(x => x == @"q:\io\Root" ||
                            x == @"q:\io\Root\Content" ||
                            x.StartsWith(@"q:\io\Root\Content\"))
                .Select(x => x.Substring("q:\\io".Length).Replace('\\', '/'))
                .OrderBy(x => x)
                .ToArray();
            var actual = targetTree.Keys
                .OrderBy(x => x)
                .ToArray();
            AssertSequencesAreEqual(expected, actual);

            expected = new[]
            {
                "------------ TRANSFER CONTENTS ------------",
                "Created  /Root/Content",
                "Created  /Root/Content/Workspace-1",
                "Created  /Root/Content/Workspace-1/DocLib-1",
                "Created  /Root/Content/Workspace-1/DocLib-1/Folder-1",
                "Created  /Root/Content/Workspace-1/DocLib-1/Folder-1/File-1.xlsx",
                "Created  /Root/Content/Workspace-1/DocLib-1/Folder-1/File-2.docx",
                "Created  /Root/Content/Workspace-1/DocLib-1/Folder-2",
                "Created  /Root/Content/Workspace-2",
            };
            actual = flow.Log.ToArray();
            AssertSequencesAreEqual(expected, actual);
        }

        /* ----------------------------------------------------------------------- q:\io\System */

        [TestMethod]
        public async Task ContentFlow2_System()
        {
            var sourceTree = CreateSourceTree(@"\Root\System");
            var targetTree = CreateTree(new[] { "/Root", "/Root/System" });
            var targetStates = new Dictionary<string, WriterState>();

            // ACTION
            var reader = new TestContentReader(@"q:\io\System", sourceTree);
            var writer = new TestRepositoryWriter(targetTree, targetStates, "/Root");
            var flow = new ContentFlow2Mock(reader, writer);
            var progress = new TestProgress();
            await flow.TransferAsync(progress);

            // ASSERT
            var expected = sourceTree.Keys
                .Select(x => x.Substring("q:\\io".Length).Replace('\\', '/'))
                .OrderBy(x => x)
                .ToArray();
            var actual = targetTree.Keys
                .OrderBy(x => x)
                .ToArray();
            AssertSequencesAreEqual(expected, actual);

            expected = new[]
            {
                "------------ TRANSFER CONTENT TYPES ------------",
                "Created  /Root/System/Schema/ContentTypes/ContentType-1",
                "Created  /Root/System/Schema/ContentTypes/ContentType-1/ContentType-3",
                "Created  /Root/System/Schema/ContentTypes/ContentType-1/ContentType-4",
                "Created  /Root/System/Schema/ContentTypes/ContentType-1/ContentType-5",
                "Created  /Root/System/Schema/ContentTypes/ContentType-1/ContentType-5/ContentType-6",
                "Created  /Root/System/Schema/ContentTypes/ContentType-2",
                "------------ TRANSFER SETTINGS ------------",
                "Created  /Root/System/Settings/Settings-1.settings",
                "Created  /Root/System/Settings/Settings-2.settings",
                "Created  /Root/System/Settings/Settings-3.settings",
                "------------ TRANSFER ASPECT DEFINITIONS ------------",
                "Created  /Root/System/Schema/Aspects/Aspect-1",
                "Created  /Root/System/Schema/Aspects/Aspect-2",
                "------------ TRANSFER CONTENTS ------------",
                "Updated  /Root/System",
                "Updated  /Root/System/Schema",
                "Updated  /Root/System/Schema/Aspects",
                "Updated  /Root/System/Schema/ContentTypes",
                "Updated  /Root/System/Settings",
            };
            actual = flow.Log.ToArray();
            AssertSequencesAreEqual(expected, actual);
        }
        [TestMethod]
        public async Task ContentFlow2_SystemSchema()
        {
            var sourceTree = CreateSourceTree(@"\");
            var targetTree = CreateTree(new[] { "/Root" });
            var targetStates = new Dictionary<string, WriterState>();

            // ACTION
            var reader = new TestContentReader(@"q:\io\System\Schema", sourceTree);
            var writer = new TestRepositoryWriter(targetTree, targetStates, "/Root/System");
            var flow = new ContentFlow2Mock(reader, writer);
            var progress = new TestProgress();
            await flow.TransferAsync(progress);

            // ASSERT
            var expected = sourceTree.Keys
                .Where(x => x == @"q:\io\System" ||
                            x == @"q:\io\System\Schema" ||
                            x.StartsWith(@"q:\io\System\Schema\"))
                .Select(x => x.Substring(@"q:\io".Length).Replace('\\', '/'))
                .OrderBy(x => x)
                .ToArray();
            var actual = targetTree.Keys
                .OrderBy(x => x)
                .ToArray();
            AssertSequencesAreEqual(expected, actual);

            expected = new[]
            {
                "------------ TRANSFER CONTENT TYPES ------------",
                "Created  /Root/System/Schema/ContentTypes/ContentType-1",
                "Created  /Root/System/Schema/ContentTypes/ContentType-1/ContentType-3",
                "Created  /Root/System/Schema/ContentTypes/ContentType-1/ContentType-4",
                "Created  /Root/System/Schema/ContentTypes/ContentType-1/ContentType-5",
                "Created  /Root/System/Schema/ContentTypes/ContentType-1/ContentType-5/ContentType-6",
                "Created  /Root/System/Schema/ContentTypes/ContentType-2",
                "------------ TRANSFER ASPECT DEFINITIONS ------------",
                "Created  /Root/System/Schema/Aspects/Aspect-1",
                "Created  /Root/System/Schema/Aspects/Aspect-2",
                "------------ TRANSFER CONTENTS ------------",
                "Updated  /Root/System/Schema",
                "Updated  /Root/System/Schema/Aspects",
                "Updated  /Root/System/Schema/ContentTypes",
            };
            actual = flow.Log.ToArray();
            AssertSequencesAreEqual(expected, actual);
        }
        [TestMethod]
        public async Task ContentFlow2_SystemSchemaContentTypes()
        {
            var sourceTree = CreateSourceTree(@"\");
            var targetTree = CreateTree(new[] { "/Root" });
            var targetStates = new Dictionary<string, WriterState>();

            // ACTION
            var reader = new TestContentReader(@"q:\io\System\Schema\ContentTypes", sourceTree);
            var writer = new TestRepositoryWriter(targetTree, targetStates, "/Root/System/Schema");
            var flow = new ContentFlow2Mock(reader, writer);
            var progress = new TestProgress();
            await flow.TransferAsync(progress);

            // ASSERT
            var expected = sourceTree.Keys
                .Where(x => x == @"q:\io\System" ||
                            x == @"q:\io\System\Schema" ||
                            x == @"q:\io\System\Schema\ContentTypes" ||
                            x.StartsWith(@"q:\io\System\ContentTypes\"))
                .Select(x => x.Substring("q:\\io".Length).Replace('\\', '/'))
                .OrderBy(x => x)
                .ToArray();
            var actual = targetTree.Keys
                .OrderBy(x => x)
                .ToArray();
            AssertSequencesAreEqual(expected, actual);

            expected = new[]
            {
                "------------ TRANSFER CONTENT TYPES ------------",
                "Created  /Root/System/Schema/ContentTypes/ContentType-1",
                "Created  /Root/System/Schema/ContentTypes/ContentType-1/ContentType-3",
                "Created  /Root/System/Schema/ContentTypes/ContentType-1/ContentType-4",
                "Created  /Root/System/Schema/ContentTypes/ContentType-1/ContentType-5",
                "Created  /Root/System/Schema/ContentTypes/ContentType-1/ContentType-5/ContentType-6",
                "Created  /Root/System/Schema/ContentTypes/ContentType-2",
                "------------ TRANSFER CONTENTS ------------",
                "Updated  /Root/System/Schema/ContentTypes",
            };
            actual = flow.Log.ToArray();
            AssertSequencesAreEqual(expected, actual);
        }
        [TestMethod]
        public async Task ContentFlow2_SystemSchemaAspects()
        {
            var sourceTree = CreateSourceTree(@"\");
            var targetTree = CreateTree(new[] { "/Root" });
            var targetStates = new Dictionary<string, WriterState>();

            // ACTION
            var reader = new TestContentReader(@"q:\io\System\Schema\Aspects", sourceTree);
            var writer = new TestRepositoryWriter(targetTree, targetStates, "/Root/System/Schema");
            var flow = new ContentFlow2Mock(reader, writer);
            var progress = new TestProgress();
            await flow.TransferAsync(progress);

            // ASSERT
            var expected = sourceTree.Keys
                .Where(x => x == @"q:\io\System" ||
                            x == @"q:\io\System\Schema" ||
                            x == @"q:\io\System\Schema\Aspects" ||
                            x.StartsWith(@"q:\io\Root\System\Aspects\"))
                .Select(x => x.Substring("q:\\io".Length).Replace('\\', '/'))
                .OrderBy(x => x)
                .ToArray();
            var actual = targetTree.Keys
                .OrderBy(x => x)
                .ToArray();
            AssertSequencesAreEqual(expected, actual);

            expected = new[]
            {
                "------------ TRANSFER ASPECT DEFINITIONS ------------",
                "Created  /Root/System/Schema/Aspects/Aspect-1",
                "Created  /Root/System/Schema/Aspects/Aspect-2",
                "------------ TRANSFER CONTENTS ------------",
                "Updated  /Root/System/Schema/Aspects",
            };
            actual = flow.Log.ToArray();
            AssertSequencesAreEqual(expected, actual);
        }
        [TestMethod]
        public async Task ContentFlow2_SystemSettings()
        {
            var sourceTree = CreateSourceTree(@"\");
            var targetTree = CreateTree(new[] { "/Root" });
            var targetStates = new Dictionary<string, WriterState>();

            // ACTION
            var reader = new TestContentReader(@"q:\io\System\Settings", sourceTree);
            var writer = new TestRepositoryWriter(targetTree, targetStates, "/Root/System");
            var flow = new ContentFlow2Mock(reader, writer);
            var progress = new TestProgress();
            await flow.TransferAsync(progress);

            // ASSERT
            var expected = sourceTree.Keys
                .Where(x => x == @"q:\io\System" ||
                            x == @"q:\io\System\Settings" ||
                            x.StartsWith(@"q:\io\Setting\"))
                .Select(x => x.Substring("q:\\io".Length).Replace('\\', '/'))
                .OrderBy(x => x)
                .ToArray();
            var actual = targetTree.Keys
                .OrderBy(x => x)
                .ToArray();
            AssertSequencesAreEqual(expected, actual);

            expected = new[]
            {
                "------------ TRANSFER SETTINGS ------------",
                "Created  /Root/System/Settings/Settings-1.settings",
                "Created  /Root/System/Settings/Settings-2.settings",
                "Created  /Root/System/Settings/Settings-3.settings",
                "------------ TRANSFER CONTENTS ------------",
                "Updated  /Root/System/Settings",
            };
            actual = flow.Log.ToArray();
            AssertSequencesAreEqual(expected, actual);
        }

        /* ----------------------------------------------------------------------- q:\io\System */
        /* ----------------------------------------------------------------------- q:\io\Settings */
        /* ----------------------------------------------------------------------- q:\io\Schema */
        /* ----------------------------------------------------------------------- q:\io\ContentTypes */
        /* ----------------------------------------------------------------------- q:\io\Aspects */

        // Assert.Fail("######################## not implemented #########################");

        /* =========================================================================================== TOOLS */

        private Dictionary<string, ContentNode> CreateSourceTree(string subtree)
        {
            var paths = new[]
                {
                    @"\Root",
                    @"\Root\(apps)",
                    @"\Root\Content",
                    @"\Root\Content\Workspace-1",
                    @"\Root\Content\Workspace-1\DocLib-1",
                    @"\Root\Content\Workspace-1\DocLib-1\Folder-1",
                    @"\Root\Content\Workspace-1\DocLib-1\Folder-1\File-1.xlsx",
                    @"\Root\Content\Workspace-1\DocLib-1\Folder-1\File-2.docx",
                    @"\Root\Content\Workspace-1\DocLib-1\Folder-2",
                    @"\Root\Content\Workspace-2",
                    @"\Root\IMS",
                    @"\Root\IMS\BuiltIn",
                    @"\Root\IMS\BuiltIn\Portal",
                    @"\Root\IMS\BuiltIn\Portal\User-3",
                    @"\Root\IMS\BuiltIn\Portal\Group-3",
                    @"\Root\IMS\Public",
                    @"\Root\IMS\Public\User-4",
                    @"\Root\IMS\Public\Group-4",
                    @"\Root\System",
                    @"\Root\System\Settings",
                    @"\Root\System\Settings\Settings-1.settings",
                    @"\Root\System\Settings\Settings-2.settings",
                    @"\Root\System\Settings\Settings-3.settings",
                    @"\Root\System\Schema",
                    @"\Root\System\Schema\Aspects",
                    @"\Root\System\Schema\Aspects\Aspect-1",
                    @"\Root\System\Schema\Aspects\Aspect-2",
                    @"\Root\System\Schema\ContentTypes",
                    @"\Root\System\Schema\ContentTypes\ContentType-1",
                    @"\Root\System\Schema\ContentTypes\ContentType-1\ContentType-3",
                    @"\Root\System\Schema\ContentTypes\ContentType-1\ContentType-4",
                    @"\Root\System\Schema\ContentTypes\ContentType-1\ContentType-5",
                    @"\Root\System\Schema\ContentTypes\ContentType-1\ContentType-5\ContentType-6",
                    @"\Root\System\Schema\ContentTypes\ContentType-2",
                }.Where(x => x.StartsWith(subtree))
                .Select(x => @"q:\io" + x)
                .ToArray();

            return CreateTree(paths);
        }
        private Dictionary<string, ContentNode> CreateInitialTargetTree()
        {
            return CreateTree(new[]
            {
                "/Root",
                "/Root/IMS",
                "/Root/IMS/BuiltIn",
                "/Root/IMS/BuiltIn/Portal",
                "/Root/IMS/BuiltIn/Portal/User-1",
                "/Root/IMS/BuiltIn/Portal/User-2",
                "/Root/IMS/BuiltIn/Portal/Group-1",
                "/Root/IMS/BuiltIn/Portal/Group-2",
                "/Root/System",
                "/Root/System/Schema",
                "/Root/System/Schema/ContentTypes",
                "/Root/System/Settings",
            });
        }
        private class TestProgress : IProgress<TransferState>
        {
            public List<double> Log { get; } = new List<double>();
            public List<string> Paths { get; } = new List<string>();
            public void Report(TransferState value)
            {
                Log.Add(value.Percent);
                Paths.Add(value.State.WriterPath);
            }
        }

    }
}
