using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SenseNet.IO.Implementations;
using SenseNet.IO.Tests.Implementations;

namespace SenseNet.IO.Tests
{
    [TestClass]
    public class SemanticContentFlowTests : TestBase
    {
        private class SemanticContentFlowMock : SemanticContentFlow
        {
            public List<string> Log { get; } = new List<string>();
            public List<TransferTask> TransferTasks { get; } = new List<TransferTask>();

            public SemanticContentFlowMock(IContentReader reader, ISnRepositoryWriter writer) : base(reader, writer, GetLogger<ContentFlow>()) { }
            protected override void WriteLog(string entry, LogLevel level = LogLevel.Trace) { Log.Add(entry); }

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

        private class SimpleContentFlowMock : SimpleContentFlow
        {
            public List<string> Log { get; } = new List<string>();
            public List<TransferTask> TransferTasks { get; } = new List<TransferTask>();

            public SimpleContentFlowMock(IContentReader reader, IContentWriter writer) : base(reader, writer, GetLogger<ContentFlow>()) { }
            protected override void WriteLog(string entry, LogLevel level = LogLevel.Trace) { Log.Add(entry); }

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

        /* ----------------------------------------------------------------------- q:\io\Root */

        [TestMethod]
        public async Task ContentFlow5_Root()
        {
            var sourceTree = CreateSourceTree(@"\");
            var targetTree = CreateTree(new[] { "/Root", "/Root/IMS" });
            var targetStates = new Dictionary<string, WriterState>();

            // ACTION
            var reader = new TestContentReader(@"q:\io\Root", sourceTree);
            var writer = new TestRepositoryWriter(targetTree, targetStates);
            var flow = new SemanticContentFlowMock(reader, writer);
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
            AssertLogsAreEqual(expected, actual);
        }
        [TestMethod]
        public async Task ContentFlow5_RootSystem()
        {
            var sourceTree = CreateSourceTree(@"\");
            var targetTree = CreateTree(new[] {"/Root", "/Root/System"});
            var targetStates = new Dictionary<string, WriterState>();

            // ACTION
            var reader = new TestContentReader(@"q:\io\Root\System", sourceTree);
            var writer = new TestRepositoryWriter(targetTree, targetStates, "/Root");
            var flow = new SemanticContentFlowMock(reader, writer);
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
            AssertLogsAreEqual(expected, actual);
        }
        [TestMethod]
        public async Task ContentFlow5_RootSystemSchema()
        {
            var sourceTree = CreateSourceTree(@"\");
            var targetTree = CreateTree(new[] { "/Root" });
            var targetStates = new Dictionary<string, WriterState>();

            // ACTION
            var reader = new TestContentReader(@"q:\io\Root\System\Schema", sourceTree);
            var writer = new TestRepositoryWriter(targetTree, targetStates, "/Root/System");
            var flow = new SemanticContentFlowMock(reader, writer);
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
            AssertLogsAreEqual(expected, actual);
        }
        [TestMethod]
        public async Task ContentFlow5_RootSystemSchemaContentTypes()
        {
            var sourceTree = CreateSourceTree(@"\");
            var targetTree = CreateTree(new[] { "/Root" });
            var targetStates = new Dictionary<string, WriterState>();

            // ACTION
            var reader = new TestContentReader(@"q:\io\Root\System\Schema\ContentTypes", sourceTree);
            var writer = new TestRepositoryWriter(targetTree, targetStates, "/Root/System/Schema");
            var flow = new SemanticContentFlowMock(reader, writer);
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
            AssertLogsAreEqual(expected, actual);
        }
        [TestMethod]
        public async Task ContentFlow5_RootSystemSchemaAspects()
        {
            var sourceTree = CreateSourceTree(@"\");
            var targetTree = CreateTree(new[] { "/Root" });
            var targetStates = new Dictionary<string, WriterState>();

            // ACTION
            var reader = new TestContentReader(@"q:\io\Root\System\Schema\Aspects", sourceTree);
            var writer = new TestRepositoryWriter(targetTree, targetStates, "/Root/System/Schema");
            var flow = new SemanticContentFlowMock(reader, writer);
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
            AssertLogsAreEqual(expected, actual);
        }
        [TestMethod]
        public async Task ContentFlow5_RootSystemSettings()
        {
            var sourceTree = CreateSourceTree(@"\");
            var targetTree = CreateTree(new[] { "/Root", "/Root/System" });
            var targetStates = new Dictionary<string, WriterState>();

            // ACTION
            var reader = new TestContentReader(@"q:\io\Root\System\Settings", sourceTree);
            var writer = new TestRepositoryWriter(targetTree, targetStates, "/Root/System");
            var flow = new SemanticContentFlowMock(reader, writer);
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
            AssertLogsAreEqual(expected, actual);
        }
        [TestMethod]
        public async Task ContentFlow5_RootContent()
        {
            var sourceTree = CreateSourceTree(@"\");
            var targetTree = CreateTree(new[] { "/Root" });
            var targetStates = new Dictionary<string, WriterState>();

            // ACTION
            var reader = new TestContentReader(@"q:\io\Root\Content", sourceTree);
            var writer = new TestRepositoryWriter(targetTree, targetStates, "/Root");
            var flow = new SemanticContentFlowMock(reader, writer);
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
            AssertLogsAreEqual(expected, actual);
        }

        /* ----------------------------------------------------------------------- q:\io\System */

        [TestMethod]
        public async Task ContentFlow5_System()
        {
            var sourceTree = CreateSourceTree(@"\Root\System");
            var targetTree = CreateTree(new[] { "/Root", "/Root/System" });
            var targetStates = new Dictionary<string, WriterState>();

            // ACTION
            var reader = new TestContentReader(@"q:\io\System", sourceTree);
            var writer = new TestRepositoryWriter(targetTree, targetStates, "/Root");
            var flow = new SemanticContentFlowMock(reader, writer);
            var progress = new TestProgress();
            await flow.TransferAsync(progress);

            // ASSERT
            var initial = new[] {"/Root"};
            var expected = sourceTree.Keys
                .Select(x => x.Substring("q:\\io".Length).Replace('\\', '/'))
                .Select(x => "/Root" + x)
                .Union(initial)
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
            AssertLogsAreEqual(expected, actual);
        }
        [TestMethod]
        public async Task ContentFlow5_SystemSchema()
        {
            var sourceTree = CreateSourceTree(@"\Root\System");
            var targetTree = CreateTree(new[] { "/Root" });
            var targetStates = new Dictionary<string, WriterState>();

            // ACTION
            var reader = new TestContentReader(@"q:\io\System\Schema", sourceTree);
            var writer = new TestRepositoryWriter(targetTree, targetStates, "/Root/System");
            var flow = new SemanticContentFlowMock(reader, writer);
            var progress = new TestProgress();
            await flow.TransferAsync(progress);

            // ASSERT
            var initial = new[] { "/Root" };
            var expected = sourceTree.Keys
                .Where(x => x == @"q:\io\System" ||
                            x == @"q:\io\System\Schema" ||
                            x.StartsWith(@"q:\io\System\Schema\"))
                .Select(x => x.Substring("q:\\io".Length).Replace('\\', '/'))
                .Select(x => "/Root" + x)
                .Union(initial)
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
            AssertLogsAreEqual(expected, actual);
        }
        [TestMethod]
        public async Task ContentFlow5_SystemSchemaContentTypes()
        {
            var sourceTree = CreateSourceTree(@"\Root\System");
            var targetTree = CreateTree(new[] { "/Root" });
            var targetStates = new Dictionary<string, WriterState>();

            // ACTION
            var reader = new TestContentReader(@"q:\io\System\Schema\ContentTypes", sourceTree);
            var writer = new TestRepositoryWriter(targetTree, targetStates, "/Root/System/Schema");
            var flow = new SemanticContentFlowMock(reader, writer);
            var progress = new TestProgress();
            await flow.TransferAsync(progress);

            // ASSERT
            var initial = new[] { "/Root" };
            var expected = sourceTree.Keys
                .Where(x => x == @"q:\io\System" ||
                            x == @"q:\io\System\Schema" ||
                            x == @"q:\io\System\Schema\ContentTypes" ||
                            x.StartsWith(@"q:\io\System\Schema\ContentTypes\"))
                .Select(x => x.Substring("q:\\io".Length).Replace('\\', '/'))
                .Select(x => "/Root" + x)
                .Union(initial)
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
            AssertLogsAreEqual(expected, actual);
        }
        [TestMethod]
        public async Task ContentFlow5_SystemSchemaAspects()
        {
            var sourceTree = CreateSourceTree(@"\Root\System");
            var targetTree = CreateTree(new[] { "/Root" });
            var targetStates = new Dictionary<string, WriterState>();

            // ACTION
            var reader = new TestContentReader(@"q:\io\System\Schema\Aspects", sourceTree);
            var writer = new TestRepositoryWriter(targetTree, targetStates, "/Root/System/Schema");
            var flow = new SemanticContentFlowMock(reader, writer);
            var progress = new TestProgress();
            await flow.TransferAsync(progress);

            // ASSERT
            var initial = new[] { "/Root" };
            var expected = sourceTree.Keys
                .Where(x => x == @"q:\io\System" ||
                            x == @"q:\io\System\Schema" ||
                            x == @"q:\io\System\Schema\Aspects" ||
                            x.StartsWith(@"q:\io\System\Schema\Aspects\"))
                .Select(x => x.Substring("q:\\io".Length).Replace('\\', '/'))
                .Select(x => "/Root" + x)
                .Union(initial)
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
            AssertLogsAreEqual(expected, actual);
        }
        [TestMethod]
        public async Task ContentFlow5_SystemSettings()
        {
            var sourceTree = CreateSourceTree(@"\Root\System");
            var targetTree = CreateTree(new[] { "/Root" });
            var targetStates = new Dictionary<string, WriterState>();

            // ACTION
            var reader = new TestContentReader(@"q:\io\System\Settings", sourceTree);
            var writer = new TestRepositoryWriter(targetTree, targetStates, "/Root/System");
            var flow = new SemanticContentFlowMock(reader, writer);
            var progress = new TestProgress();
            await flow.TransferAsync(progress);

            // ASSERT
            var initial = new[] { "/Root" };
            var expected = sourceTree.Keys
                .Where(x => x == @"q:\io\System" ||
                            x == @"q:\io\System\Settings" ||
                            x.StartsWith(@"q:\io\System\Settings\"))
                .Select(x => x.Substring("q:\\io".Length).Replace('\\', '/'))
                .Select(x => "/Root" + x)
                .Union(initial)
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
            AssertLogsAreEqual(expected, actual);
        }

        /* ----------------------------------------------------------------------- q:\io\Schema */

        [TestMethod]
        public async Task ContentFlow5_Schema()
        {
            var sourceTree = CreateSourceTree(@"\Root\System\Schema");
            var targetTree = CreateTree(new[] { "/Root", "/Root/System", "/Root/System/Schema" });
            var targetStates = new Dictionary<string, WriterState>();

            // ACTION
            var reader = new TestContentReader(@"q:\io\Schema", sourceTree);
            var writer = new TestRepositoryWriter(targetTree, targetStates, "/Root/System");
            var flow = new SemanticContentFlowMock(reader, writer);
            var progress = new TestProgress();
            await flow.TransferAsync(progress);

            // ASSERT
            var initial = new[] { "/Root", "/Root/System" };
            var expected = sourceTree.Keys
                .Select(x => x.Substring("q:\\io".Length).Replace('\\', '/'))
                .Select(x => "/Root/System" + x)
                .Union(initial)
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
            AssertLogsAreEqual(expected, actual);
        }
        [TestMethod]
        public async Task ContentFlow5_SchemaContentTypes()
        {
            var sourceTree = CreateSourceTree(@"\Root\System\Schema");
            var targetTree = CreateTree(new[] { "/Root", "/Root/System", "/Root/System/Schema" });
            var targetStates = new Dictionary<string, WriterState>();

            // ACTION
            var reader = new TestContentReader(@"q:\io\Schema\ContentTypes", sourceTree);
            var writer = new TestRepositoryWriter(targetTree, targetStates, "/Root/System/Schema");
            var flow = new SemanticContentFlowMock(reader, writer);
            var progress = new TestProgress();
            await flow.TransferAsync(progress);

            // ASSERT
            var initial = new[] { "/Root", "/Root/System" };
            var expected = sourceTree.Keys
                .Where(x => x == @"q:\io\Schema" ||
                            x == @"q:\io\Schema\ContentTypes" ||
                            x.StartsWith(@"q:\io\Schema\ContentTypes\"))
                .Select(x => x.Substring("q:\\io".Length).Replace('\\', '/'))
                .Select(x => "/Root/System" + x)
                .Union(initial)
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
            AssertLogsAreEqual(expected, actual);
        }
        [TestMethod]
        public async Task ContentFlow5_SchemaAspects()
        {
            var sourceTree = CreateSourceTree(@"\Root\System\Schema");
            var targetTree = CreateTree(new[] { "/Root", "/Root/System", "/Root/System/Schema" });
            var targetStates = new Dictionary<string, WriterState>();

            // ACTION
            var reader = new TestContentReader(@"q:\io\Schema\Aspects", sourceTree);
            var writer = new TestRepositoryWriter(targetTree, targetStates, "/Root/System/Schema");
            var flow = new SemanticContentFlowMock(reader, writer);
            var progress = new TestProgress();
            await flow.TransferAsync(progress);

            // ASSERT
            var initial = new[] { "/Root", "/Root/System" };
            var expected = sourceTree.Keys
                .Where(x => x == @"q:\io\Schema" ||
                            x == @"q:\io\Schema\Aspects" ||
                            x.StartsWith(@"q:\io\Schema\Aspects\"))
                .Select(x => x.Substring("q:\\io".Length).Replace('\\', '/'))
                .Select(x => "/Root/System" + x)
                .Union(initial)
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
            AssertLogsAreEqual(expected, actual);
        }

        /* ----------------------------------------------------------------------- q:\io\ContentTypes */

        [TestMethod]
        public async Task ContentFlow5_ContentTypes()
        {
            var sourceTree = CreateSourceTree(@"\Root\System\Schema\ContentTypes");
            var targetTree = CreateTree(new[] { "/Root", "/Root/System", "/Root/System/Schema" });
            var targetStates = new Dictionary<string, WriterState>();

            // ACTION
            var reader = new TestContentReader(@"q:\io\ContentTypes", sourceTree);
            var writer = new TestRepositoryWriter(targetTree, targetStates, "/Root/System/Schema");
            var flow = new SemanticContentFlowMock(reader, writer);
            var progress = new TestProgress();
            await flow.TransferAsync(progress);

            // ASSERT
            var initial = new[] { "/Root", "/Root/System", "/Root/System/Schema" };
            var expected = sourceTree.Keys
                .Select(x => x.Substring("q:\\io".Length).Replace('\\', '/'))
                .Select(x => "/Root/System/Schema" + x)
                .Union(initial)
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
            AssertLogsAreEqual(expected, actual);
        }

        /* ----------------------------------------------------------------------- q:\io\Aspects */

        [TestMethod]
        public async Task ContentFlow5_Aspects()
        {
            var sourceTree = CreateSourceTree(@"\Root\System\Schema\Aspects");
            var targetTree = CreateTree(new[] { "/Root", "/Root/System", "/Root/System/Schema" });
            var targetStates = new Dictionary<string, WriterState>();

            // ACTION
            var reader = new TestContentReader(@"q:\io\Aspects", sourceTree);
            var writer = new TestRepositoryWriter(targetTree, targetStates, "/Root/System/Schema");
            var flow = new SemanticContentFlowMock(reader, writer);
            var progress = new TestProgress();
            await flow.TransferAsync(progress);

            // ASSERT
            var initial = new[] { "/Root", "/Root/System", "/Root/System/Schema" };
            var expected = sourceTree.Keys
                .Select(x => x.Substring("q:\\io".Length).Replace('\\', '/'))
                .Select(x => "/Root/System/Schema" + x)
                .Union(initial)
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
            AssertLogsAreEqual(expected, actual);
        }

        /* ----------------------------------------------------------------------- q:\io\Settings */

        [TestMethod]
        public async Task ContentFlow5_Settings()
        {
            var sourceTree = CreateSourceTree(@"\Root\System\Settings");
            var targetTree = CreateTree(new[] { "/Root", "/Root/System" });
            var targetStates = new Dictionary<string, WriterState>();

            // ACTION
            var reader = new TestContentReader(@"q:\io\Settings", sourceTree);
            var writer = new TestRepositoryWriter(targetTree, targetStates, "/Root/System");
            var flow = new SemanticContentFlowMock(reader, writer);
            var progress = new TestProgress();
            await flow.TransferAsync(progress);

            // ASSERT
            var initial = new[] { "/Root", "/Root/System" };
            var expected = sourceTree.Keys
                .Select(x => x.Substring("q:\\io".Length).Replace('\\', '/'))
                .Select(x => "/Root/System" + x)
                .Union(initial)
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
            AssertLogsAreEqual(expected, actual);
        }

        /* ----------------------------------------------------------------------- q:\io\OldSettings */

        [TestMethod]
        public async Task ContentFlow5_Settings_RENAMED()
        {
            var sourceTree = CreateSourceTree(@"\Root\System\Settings");
            sourceTree = ReplacePaths(sourceTree, @"io\Settings", @"io\OldSettings");
            var targetTree = CreateTree(new[] { "/Root", "/Root/System" });
            var targetStates = new Dictionary<string, WriterState>();

            // ACTION
            var reader = new TestContentReader(@"q:\io\OldSettings", sourceTree);
            var writer = new TestRepositoryWriter(targetTree, targetStates, "/Root/System", "Settings");
            var flow = new SemanticContentFlowMock(reader, writer);
            var progress = new TestProgress();
            await flow.TransferAsync(progress);

            // ASSERT
            var expected = new[]
            {
                "/Root",
                "/Root/System",
                "/Root/System/Settings",
                "/Root/System/Settings/Settings-1.settings",
                "/Root/System/Settings/Settings-2.settings",
                "/Root/System/Settings/Settings-3.settings",
            };
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
            AssertLogsAreEqual(expected, actual);
        }

        /* =========================================================================================== UPDATE REFERENCES TESTS */

        [TestMethod]
        public async Task ContentFlow5_UpdateReferences()
        {
            var sourceTree = CreateSourceTree(@"\");
            var targetTree = CreateTree(new[] { "/Root", "/Root/IMS" });
            var targetStates = new Dictionary<string, WriterState>
            {
                {
                    "/Root/System/Schema/ContentTypes/ContentType-1/ContentType-4",
                    new WriterState
                        {Action = WriterAction.Creating, BrokenReferences = new string[0], RetryPermissions = true}
                },
                {
                    "/Root/System/Settings/Settings-2.settings",
                    new WriterState
                        {Action = WriterAction.Creating, BrokenReferences = new[] {"F2", "F3"}, RetryPermissions = false}
                },
                {
                    "/Root/Content/Workspace-1/DocLib-1",
                    new WriterState
                        {Action = WriterAction.Creating, BrokenReferences = new[] {"F1"}, RetryPermissions = false}
                },
                {
                    "/Root/IMS/BuiltIn/Portal/Group-3",
                    new WriterState
                        {Action = WriterAction.Creating, BrokenReferences = new[] {"F1", "F3"}, RetryPermissions = true}
                }
            };

            // ACTION
            var reader = new TestContentReader(@"q:\io\Root", sourceTree);
            var writer = new TestRepositoryWriter(targetTree, targetStates);
            var flow = new SemanticContentFlowMock(reader, writer);
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
                "Creating /Root/System/Schema/ContentTypes/ContentType-1/ContentType-4",
                "Created  /Root/System/Schema/ContentTypes/ContentType-1/ContentType-5",
                "Created  /Root/System/Schema/ContentTypes/ContentType-1/ContentType-5/ContentType-6",
                "Created  /Root/System/Schema/ContentTypes/ContentType-2",
                "------------ TRANSFER SETTINGS ------------",
                "Created  /Root/System/Settings/Settings-1.settings",
                "Creating /Root/System/Settings/Settings-2.settings",
                "Created  /Root/System/Settings/Settings-3.settings",
                "------------ TRANSFER ASPECT DEFINITIONS ------------",
                "Created  /Root/System/Schema/Aspects/Aspect-1",
                "Created  /Root/System/Schema/Aspects/Aspect-2",
                "------------ TRANSFER CONTENTS ------------",
                "Updated  /Root",
                "Created  /Root/(apps)",
                "Created  /Root/Content",
                "Created  /Root/Content/Workspace-1",
                "Creating /Root/Content/Workspace-1/DocLib-1",
                "Created  /Root/Content/Workspace-1/DocLib-1/Folder-1",
                "Created  /Root/Content/Workspace-1/DocLib-1/Folder-1/File-1.xlsx",
                "Created  /Root/Content/Workspace-1/DocLib-1/Folder-1/File-2.docx",
                "Created  /Root/Content/Workspace-1/DocLib-1/Folder-2",
                "Created  /Root/Content/Workspace-2",
                "Updated  /Root/IMS",
                "Created  /Root/IMS/BuiltIn",
                "Created  /Root/IMS/BuiltIn/Portal",
                "Creating /Root/IMS/BuiltIn/Portal/Group-3",
                "Created  /Root/IMS/BuiltIn/Portal/User-3",
                "Created  /Root/IMS/Public",
                "Created  /Root/IMS/Public/Group-4",
                "Created  /Root/IMS/Public/User-4",
                "Updated  /Root/System",
                "Updated  /Root/System/Schema",
                "Updated  /Root/System/Schema/Aspects",
                "Updated  /Root/System/Schema/ContentTypes",
                "Updated  /Root/System/Settings",
                "------------ UPDATE REFERENCES ------------",
                "Updated  /Root/System/Schema/ContentTypes/ContentType-1/ContentType-4",
                "Updated  /Root/System/Settings/Settings-2.settings",
                "Updated  /Root/Content/Workspace-1/DocLib-1",
                "Updated  /Root/IMS/BuiltIn/Portal/Group-3",
            };
            actual = flow.Log.ToArray();
            AssertLogsAreEqual(expected, actual);
        }

        /* =========================================================================================== CUTOFF TESTS */

        [TestMethod]
        public async Task ContentFlow5_Root_Error_Create_Cutoff()
        {
            var sourceTree = CreateSourceTree(@"\");
            var targetTree = CreateTree(new[] { "/Root" });
            var targetStates = new Dictionary<string, WriterState>();

            // ACTION
            var reader = new TestContentReader(@"q:\io\Root", sourceTree);
            var writer = new TestRepositoryWriter(targetTree, targetStates,
                badContentPaths: new[] {"/Root/Content/Workspace-1/DocLib-1", "/Root/IMS/Public" });
            var flow = new SemanticContentFlowMock(reader, writer);
            var progress = new TestProgress();
            await flow.TransferAsync(progress);

            // ASSERT
            var expected = new[]
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
                "Failed   /Root/Content/Workspace-1/DocLib-1\r\n         ErrorMessage1",
                "Skip subtree: reader: q:/io/Root/Content/Workspace-1/DocLib-1",
                "Skip subtree: writer: /Root/Content/Workspace-1/DocLib-1",
                "Created  /Root/Content/Workspace-2",
                "Created  /Root/IMS",
                "Created  /Root/IMS/BuiltIn",
                "Created  /Root/IMS/BuiltIn/Portal",
                "Created  /Root/IMS/BuiltIn/Portal/Group-3",
                "Created  /Root/IMS/BuiltIn/Portal/User-3",
                "Failed   /Root/IMS/Public\r\n         ErrorMessage1",
                "Skip subtree: reader: q:/io/Root/IMS/Public",
                "Skip subtree: writer: /Root/IMS/Public",
                "Updated  /Root/System",
                "Updated  /Root/System/Schema",
                "Updated  /Root/System/Schema/Aspects",
                "Updated  /Root/System/Schema/ContentTypes",
                "Updated  /Root/System/Settings"
            };
            var actual = flow.Log.ToArray();
            AssertLogsAreEqual(expected, actual);
        }
        [TestMethod]
        public async Task ContentFlow5_Root_Error_Update_NoCutoff()
        {
            var sourceTree = CreateSourceTree(@"\");
            var targetTree = CreateTree(new[]
            {
                "/Root",
                "/Root/(apps)",
                "/Root/Content",
                "/Root/Content/Workspace-1",
                "/Root/Content/Workspace-1/DocLib-1",
                "/Root/Content/Workspace-1/DocLib-1/Folder-1",
                "/Root/Content/Workspace-1/DocLib-1/Folder-1/File-1.xlsx",
                "/Root/Content/Workspace-1/DocLib-1/Folder-1/File-2.docx",
                "/Root/Content/Workspace-1/DocLib-1/Folder-2",
                "/Root/Content/Workspace-2",
                "/Root/IMS",
                "/Root/IMS/BuiltIn",
                "/Root/IMS/BuiltIn/Portal",
                "/Root/IMS/BuiltIn/Portal/Group-3",
                "/Root/IMS/BuiltIn/Portal/User-3",
                "/Root/IMS/Public",
                "/Root/IMS/Public/User-4",
                "/Root/IMS/Public/Group-4",
                "/Root/System",
                "/Root/System/Settings",
                "/Root/System/Settings/Settings-1.settings",
                "/Root/System/Settings/Settings-2.settings",
                "/Root/System/Settings/Settings-3.settings",
                "/Root/System/Schema",
                "/Root/System/Schema/Aspects",
                "/Root/System/Schema/Aspects/Aspect-1",
                "/Root/System/Schema/Aspects/Aspect-2",
                "/Root/System/Schema/ContentTypes",
                "/Root/System/Schema/ContentTypes/ContentType-1",
                "/Root/System/Schema/ContentTypes/ContentType-1/ContentType-3",
                "/Root/System/Schema/ContentTypes/ContentType-1/ContentType-4",
                "/Root/System/Schema/ContentTypes/ContentType-1/ContentType-5",
                "/Root/System/Schema/ContentTypes/ContentType-1/ContentType-5/ContentType-6",
                "/Root/System/Schema/ContentTypes/ContentType-2",
            });
            var targetStates = new Dictionary<string, WriterState>();

            // ACTION
            var reader = new TestContentReader(@"q:\io\Root", sourceTree);
            var writer = new TestRepositoryWriter(targetTree, targetStates,
                badContentPaths: new[] { "/Root/Content/Workspace-1/DocLib-1", "/Root/IMS/Public" });
            var flow = new SemanticContentFlowMock(reader, writer);
            var progress = new TestProgress();
            await flow.TransferAsync(progress);

            // ASSERT
            var expected = new[]
            {
                "------------ TRANSFER CONTENT TYPES ------------",
                "Updated  /Root/System/Schema/ContentTypes/ContentType-1",
                "Updated  /Root/System/Schema/ContentTypes/ContentType-1/ContentType-3",
                "Updated  /Root/System/Schema/ContentTypes/ContentType-1/ContentType-4",
                "Updated  /Root/System/Schema/ContentTypes/ContentType-1/ContentType-5",
                "Updated  /Root/System/Schema/ContentTypes/ContentType-1/ContentType-5/ContentType-6",
                "Updated  /Root/System/Schema/ContentTypes/ContentType-2",
                "------------ TRANSFER SETTINGS ------------",
                "Updated  /Root/System/Settings/Settings-1.settings",
                "Updated  /Root/System/Settings/Settings-2.settings",
                "Updated  /Root/System/Settings/Settings-3.settings",
                "------------ TRANSFER ASPECT DEFINITIONS ------------",
                "Updated  /Root/System/Schema/Aspects/Aspect-1",
                "Updated  /Root/System/Schema/Aspects/Aspect-2",
                "------------ TRANSFER CONTENTS ------------",
                "Updated  /Root",
                "Updated  /Root/(apps)",
                "Updated  /Root/Content",
                "Updated  /Root/Content/Workspace-1",
                "Failed   /Root/Content/Workspace-1/DocLib-1\r\n         ErrorMessage1",
                "Updated  /Root/Content/Workspace-1/DocLib-1/Folder-1",
                "Updated  /Root/Content/Workspace-1/DocLib-1/Folder-1/File-1.xlsx",
                "Updated  /Root/Content/Workspace-1/DocLib-1/Folder-1/File-2.docx",
                "Updated  /Root/Content/Workspace-1/DocLib-1/Folder-2",
                "Updated  /Root/Content/Workspace-2",
                "Updated  /Root/IMS",
                "Updated  /Root/IMS/BuiltIn",
                "Updated  /Root/IMS/BuiltIn/Portal",
                "Updated  /Root/IMS/BuiltIn/Portal/Group-3",
                "Updated  /Root/IMS/BuiltIn/Portal/User-3",
                "Failed   /Root/IMS/Public\r\n         ErrorMessage1",
                "Updated  /Root/IMS/Public/Group-4",
                "Updated  /Root/IMS/Public/User-4",
                "Updated  /Root/System",
                "Updated  /Root/System/Schema",
                "Updated  /Root/System/Schema/Aspects",
                "Updated  /Root/System/Schema/ContentTypes",
                "Updated  /Root/System/Settings"
            };
            var actual = flow.Log.ToArray();
            AssertLogsAreEqual(expected, actual);
        }

        [TestMethod]
        public async Task ContentFlow5_RootContent_Error_Create_Cutoff()
        {
            var sourceTree = CreateSourceTree(@"\");
            var targetTree = CreateTree(new[] { "/Root" });
            var targetStates = new Dictionary<string, WriterState>();

            // ACTION
            var reader = new TestContentReader(@"q:\io\Root\Content", sourceTree);
            var writer = new TestRepositoryWriter(targetTree, targetStates, "/Root",
                badContentPaths: new[] { "/Root/Content/Workspace-1/DocLib-1", "/Root/IMS/Public" });
            var flow = new SimpleContentFlowMock(reader, writer);
            var progress = new TestProgress();
            await flow.TransferAsync(progress);

            // ASSERT
            var expected = new[]
            {
                "------------ TRANSFER CONTENTS ------------",
                "Created  Content",
                "Created  Content/Workspace-1",
                "Failed   Content/Workspace-1/DocLib-1\r\n         ErrorMessage1",
                "Skip subtree: reader: q:/io/Root/Content/Workspace-1/DocLib-1",
                "Skip subtree: writer: Content/Workspace-1/DocLib-1",
                "Created  Content/Workspace-2",
            };
            var actual = flow.Log.ToArray();
            AssertLogsAreEqual(expected, actual);
        }
        [TestMethod]
        public async Task ContentFlow5_RootContent_Error_Update_NoCutoff()
        {
            var sourceTree = CreateSourceTree(@"\");
            var targetTree = CreateTree(new[]
            {
                "/Root",
                "/Root/(apps)",
                "/Root/Content",
                "/Root/Content/Workspace-1",
                "/Root/Content/Workspace-1/DocLib-1",
                "/Root/Content/Workspace-1/DocLib-1/Folder-1",
                "/Root/Content/Workspace-1/DocLib-1/Folder-1/File-1.xlsx",
                "/Root/Content/Workspace-1/DocLib-1/Folder-1/File-2.docx",
                "/Root/Content/Workspace-1/DocLib-1/Folder-2",
                "/Root/Content/Workspace-2",
                "/Root/IMS",
                "/Root/IMS/BuiltIn",
                "/Root/IMS/BuiltIn/Portal",
                "/Root/IMS/BuiltIn/Portal/Group-3",
                "/Root/IMS/BuiltIn/Portal/User-3",
                "/Root/IMS/Public",
                "/Root/IMS/Public/User-4",
                "/Root/IMS/Public/Group-4",
                "/Root/System",
                "/Root/System/Settings",
                "/Root/System/Settings/Settings-1.settings",
                "/Root/System/Settings/Settings-2.settings",
                "/Root/System/Settings/Settings-3.settings",
                "/Root/System/Schema",
                "/Root/System/Schema/Aspects",
                "/Root/System/Schema/Aspects/Aspect-1",
                "/Root/System/Schema/Aspects/Aspect-2",
                "/Root/System/Schema/ContentTypes",
                "/Root/System/Schema/ContentTypes/ContentType-1",
                "/Root/System/Schema/ContentTypes/ContentType-1/ContentType-3",
                "/Root/System/Schema/ContentTypes/ContentType-1/ContentType-4",
                "/Root/System/Schema/ContentTypes/ContentType-1/ContentType-5",
                "/Root/System/Schema/ContentTypes/ContentType-1/ContentType-5/ContentType-6",
                "/Root/System/Schema/ContentTypes/ContentType-2",
            });
            var targetStates = new Dictionary<string, WriterState>();

            // ACTION
            var reader = new TestContentReader(@"q:\io\Root\Content", sourceTree);
            var writer = new TestRepositoryWriter(targetTree, targetStates, "/Root",
                badContentPaths: new[] { "/Root/Content/Workspace-1/DocLib-1", "/Root/IMS/Public" });
            var flow = new SimpleContentFlowMock(reader, writer);
            var progress = new TestProgress();
            await flow.TransferAsync(progress);

            // ASSERT
            var expected = new[]
            {
                "------------ TRANSFER CONTENTS ------------",
                "Updated  Content",
                "Updated  Content/Workspace-1",
                "Failed   Content/Workspace-1/DocLib-1\r\n         ErrorMessage1",
                "Updated  Content/Workspace-1/DocLib-1/Folder-1",
                "Updated  Content/Workspace-1/DocLib-1/Folder-1/File-1.xlsx",
                "Updated  Content/Workspace-1/DocLib-1/Folder-1/File-2.docx",
                "Updated  Content/Workspace-1/DocLib-1/Folder-2",
                "Updated  Content/Workspace-2",
            };
            var actual = flow.Log.ToArray();
            AssertLogsAreEqual(expected, actual);
        }


        /* =========================================================================================== TOOLS */

        private Dictionary<string, ContentNode> CreateSourceTree(string subtree)
        {
            var name = subtree.Substring(subtree.LastIndexOf("\\", StringComparison.OrdinalIgnoreCase) + 1);
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
                }.Where(x => x.StartsWith(subtree)).ToArray();
                var paths1 = paths.Select(x => x.Substring(subtree.Length)).ToArray();
                var paths2=paths1.Select(x => @"q:\io\" + name + x).ToArray();

            return CreateTree(paths2);
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

        private Dictionary<string, ContentNode> ReplacePaths(Dictionary<string, ContentNode> source, string from, string to)
        {
            var target = new Dictionary<string, ContentNode>();
            foreach (var item in source)
            {
                var path = item.Key.Replace(from, to);
                var content = item.Value;
                content.Path = path;
                target.Add(path, content);
            }

            return target;
        }

    }
}
