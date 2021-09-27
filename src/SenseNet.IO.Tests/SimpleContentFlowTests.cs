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
    public class SimpleContentFlowTests : TestBase
    {
        private class SimpleContentFlowMock : SimpleContentFlow
        {
            public SimpleContentFlowMock(IContentReader reader, IContentWriter writer) : base(reader, writer, GetLogger<ContentFlow>()) { }
            protected override void WriteLog(string entry, LogLevel level = LogLevel.Trace) { }

            protected override void WriteTask(WriterState state) { }
            protected override int LoadTaskCount() { return 0; }
            protected override IEnumerable<TransferTask> LoadTasks() { return new TransferTask[0]; }
        }

        /* ============================================================================ SIMPLE TESTS */

        [TestMethod]
        public async Task Flow_1()
        {
            var sourceTree = CreateTree(new[]
            {
                "/Root",
                "/Root/Folder-1",
                "/Root/Folder-1/File-1",
                "/Root/Folder-1/File-2",
                "/Root/Folder-1/File-3",
                "/Root/Folder-2",
                "/Root/Folder-2/File-1",
                "/Root/Folder-2/File-2",
                "/Root/Folder-2/File-3",
            });
            var targetTree = new Dictionary<string, ContentNode>();

            // ACTION
            var flow = new SimpleContentFlowMock(
                new TestContentReader("/Root", sourceTree),
                new TestContentWriter(targetTree));
            var progress = new TestProgress();
            await flow.TransferAsync(progress);

            // ASSERT
            Assert.AreEqual(sourceTree.Count, targetTree.Count);
            Assert.AreEqual(targetTree.Count, progress.Log.Count);
        }
        [TestMethod]
        public async Task Flow_SubTree()
        {
            var sourceTree = CreateSimpleTree();
            var targetTree = CreateTree(new[]
            {
                "/Root",
                "/Root/Node-01",
            });

            // ACTION
            var flow = new SimpleContentFlowMock(
                new TestCQReader("/Root/Node-01/Node-08", 4, sourceTree),
                new TestContentWriter(targetTree));
            var progress = new TestProgress();
            await flow.TransferAsync(progress);

            // ASSERT
            Assert.AreEqual(11, targetTree.Count);
            Assert.AreEqual(9, progress.Log.Count);
        }
        [TestMethod]
        public async Task Flow_2()
        {
            var sourceTree = CreateSimpleTree();
            var targetTree = new Dictionary<string, ContentNode>();

            // ACTION
            var flow = new SimpleContentFlowMock(
                new TestCQReader("/Root", 4, sourceTree),
                new TestContentWriter(targetTree));
            var progress = new TestProgress();
            await flow.TransferAsync(progress);

            // ASSERT
            Assert.AreEqual(sourceTree.Count, targetTree.Count);
            Assert.AreEqual(targetTree.Count, progress.Log.Count);
        }
        [TestMethod]
        public async Task Flow_StartsWithNotEnough()
        {
            var sourceTree = CreateTree(new[]
            {
                "/Root",
                "/Root/aa",
                "/Root/aa/aaa",
                "/Root/aaa",
                "/Root/aaa/aaa",
            });
            var targetTree = CreateTree(new[]
            {
                "/Root",
            });

            // ACTION
            var flow = new SimpleContentFlowMock(
                new TestCQReader("/Root/aa", 5, sourceTree),
                new TestContentWriter(targetTree));
            var progress = new TestProgress();
            await flow.TransferAsync(progress);

            // ASSERT
            Assert.AreEqual(3, targetTree.Count);
            Assert.AreEqual(2, progress.Log.Count);
        }

        [TestMethod]
        public async Task Flow_SubTreeToAnother()
        {
            var sourceTree = CreateSimpleTree();
            var targetTree = CreateTree(new[]
            {
                "/Root",
                "/Root/Node-99",
            });

            // ACTION
            var flow = new SimpleContentFlowMock(
                new TestCQReader("/Root/Node-01/Node-02/Node-03", 4, sourceTree),
                new TestContentWriter(targetTree, "/Root/Node-99"));
            await flow.TransferAsync(new TestProgress());

            // ASSERT
            var actualPaths = targetTree.Keys.OrderBy(x => x).ToArray();
            Assert.AreEqual(5, actualPaths.Length);
            Assert.AreEqual("/Root", actualPaths[0]);
            Assert.AreEqual("/Root/Node-99", actualPaths[1]);
            Assert.AreEqual("/Root/Node-99/Node-03", actualPaths[2]);
            Assert.AreEqual("/Root/Node-99/Node-03/Node-04", actualPaths[3]);
            Assert.AreEqual("/Root/Node-99/Node-03/Node-05", actualPaths[4]);
        }
        [TestMethod]
        public async Task Flow_SubTreeToRenamed()
        {
            var sourceTree = CreateSimpleTree();
            var targetTree = CreateTree(new[]
            {
                "/Root",
                "/Root/Node-99",
            });

            // ACTION
            var flow = new SimpleContentFlowMock(
                new TestCQReader("/Root/Node-01/Node-02/Node-03", 4, sourceTree),
                new TestContentWriter(targetTree, "/Root/Node-99", "Renamed"));
            await flow.TransferAsync(new TestProgress());

            // ASSERT
            var actualPaths = targetTree.Keys.OrderBy(x => x).ToArray();
            Assert.AreEqual(5, actualPaths.Length);
            Assert.AreEqual("/Root", actualPaths[0]);
            Assert.AreEqual("/Root/Node-99", actualPaths[1]);
            Assert.AreEqual("/Root/Node-99/Renamed", actualPaths[2]);
            Assert.AreEqual("/Root/Node-99/Renamed/Node-04", actualPaths[3]);
            Assert.AreEqual("/Root/Node-99/Renamed/Node-05", actualPaths[4]);
        }

        /* ============================================================================ 4-PASS TESTS */

        [TestMethod]
        public async Task Flow_4_Root()
        {
            var sourceTree = Create4PassTree();
            var targetTree = new Dictionary<string, ContentNode>();

            // ACTION
            var flow = new SimpleContentFlowMock(
                new TestCQReader("/Root", 4, sourceTree),
                new TestContentWriter(targetTree));
            var progress = new TestProgress();
            await flow.TransferAsync(progress);

            // ASSERT
            var actual = string.Join("\r\n", progress.Paths);
            Assert.AreEqual(sourceTree.Count, targetTree.Count);
            var expected = @"
(apps)
Content
Content/Workspace-1
Content/Workspace-1/DocLib-1
Content/Workspace-1/DocLib-1/Folder-1
Content/Workspace-1/DocLib-1/Folder-1/File-1.xlsx
Content/Workspace-1/DocLib-1/Folder-1/File-2.docx
Content/Workspace-1/DocLib-1/Folder-2
Content/Workspace-2
IMS
System
System/Schema
System/Schema/Aspects
System/Schema/Aspects/Aspect-1
System/Schema/Aspects/Aspect-2
System/Schema/ContentTypes
System/Schema/ContentTypes/ContentType-1
System/Schema/ContentTypes/ContentType-1/ContentType-3
System/Schema/ContentTypes/ContentType-1/ContentType-4
System/Schema/ContentTypes/ContentType-1/ContentType-5
System/Schema/ContentTypes/ContentType-1/ContentType-5/ContentType-6
System/Schema/ContentTypes/ContentType-2
System/Settings
System/Settings/Settings-1.settings
System/Settings/Settings-2.settings
System/Settings/Settings-3.settings
";
            Assert.AreEqual(expected.TrimEnd(), actual);
        }
        [TestMethod]
        public async Task Flow_4_Root_Content()
        {
            var sourceTree = Create4PassTree();
            var targetTree = new Dictionary<string, ContentNode>();

            // ACTION
            var flow = new SimpleContentFlowMock(
                new TestCQReader("/Root/Content", 4, sourceTree),
                new TestContentWriter(targetTree));
            var progress = new TestProgress();
            await flow.TransferAsync(progress);

            // ASSERT
            Assert.AreEqual(8, targetTree.Count);
            var expected = @"
Workspace-1
Workspace-1/DocLib-1
Workspace-1/DocLib-1/Folder-1
Workspace-1/DocLib-1/Folder-1/File-1.xlsx
Workspace-1/DocLib-1/Folder-1/File-2.docx
Workspace-1/DocLib-1/Folder-2
Workspace-2
";
            var actual = string.Join("\r\n", progress.Paths);
            Assert.AreEqual(expected.TrimEnd(), actual);
        }
        [TestMethod]
        public async Task Flow_4_Root_System()
        {
            var sourceTree = Create4PassTree();
            var targetTree = new Dictionary<string, ContentNode>();

            // ACTION
            var flow = new SimpleContentFlowMock(
                new TestCQReader("/Root/System", 4, sourceTree),
                new TestContentWriter(targetTree));
            var progress = new TestProgress();
            await flow.TransferAsync(progress);

            // ASSERT
            var actual = string.Join("\r\n", progress.Paths);
            //Assert.AreEqual(sourceTree.Count, targetTree.Count);
            var expected = @"
Schema
Schema/Aspects
Schema/Aspects/Aspect-1
Schema/Aspects/Aspect-2
Schema/ContentTypes
Schema/ContentTypes/ContentType-1
Schema/ContentTypes/ContentType-1/ContentType-3
Schema/ContentTypes/ContentType-1/ContentType-4
Schema/ContentTypes/ContentType-1/ContentType-5
Schema/ContentTypes/ContentType-1/ContentType-5/ContentType-6
Schema/ContentTypes/ContentType-2
Settings
Settings/Settings-1.settings
Settings/Settings-2.settings
Settings/Settings-3.settings
";
            Assert.AreEqual(expected.TrimEnd(), actual);
        }
        [TestMethod]
        public async Task Flow_4_Root_System_Schema()
        {
            var sourceTree = Create4PassTree();
            var targetTree = new Dictionary<string, ContentNode>();

            // ACTION
            var flow = new SimpleContentFlowMock(
                new TestCQReader("/Root/System/Schema", 4, sourceTree),
                new TestContentWriter(targetTree));
            var progress = new TestProgress();
            await flow.TransferAsync(progress);

            // ASSERT
            var actual = string.Join("\r\n", progress.Paths);
            //Assert.AreEqual(sourceTree.Count, targetTree.Count);
            var expected = @"
Aspects
Aspects/Aspect-1
Aspects/Aspect-2
ContentTypes
ContentTypes/ContentType-1
ContentTypes/ContentType-1/ContentType-3
ContentTypes/ContentType-1/ContentType-4
ContentTypes/ContentType-1/ContentType-5
ContentTypes/ContentType-1/ContentType-5/ContentType-6
ContentTypes/ContentType-2
";
            Assert.AreEqual(expected.TrimEnd(), actual);
        }
        [TestMethod]
        public async Task Flow_4_Root_System_Settings()
        {
            var sourceTree = Create4PassTree();
            var targetTree = new Dictionary<string, ContentNode>();

            // ACTION
            var flow = new SimpleContentFlowMock(
                new TestCQReader("/Root/System/Settings", 4, sourceTree),
                new TestContentWriter(targetTree));
            var progress = new TestProgress();
            await flow.TransferAsync(progress);

            // ASSERT
            var actual = string.Join(",", progress.Paths);
            //Assert.AreEqual(sourceTree.Count, targetTree.Count);
            var expected = @",Settings-1.settings,Settings-2.settings,Settings-3.settings";
            Assert.AreEqual(expected.TrimEnd(), actual);
        }

        /* ============================================================================ TOOLS */

        private Dictionary<string, ContentNode> CreateSimpleTree()
        {
            return CreateTree(new[]
            {
                "/Root",
                "/Root/Node-01",
                "/Root/Node-01/Node-02",
                "/Root/Node-01/Node-02/Node-03",
                "/Root/Node-01/Node-02/Node-03/Node-04",
                "/Root/Node-01/Node-02/Node-03/Node-05",
                "/Root/Node-01/Node-02/Node-06",
                "/Root/Node-01/Node-02/Node-07",
                "/Root/Node-01/Node-08",
                "/Root/Node-01/Node-08/Node-09",
                "/Root/Node-01/Node-08/Node-10",
                "/Root/Node-01/Node-08/Node-10/Node-11",
                "/Root/Node-01/Node-08/Node-10/Node-12",
                "/Root/Node-01/Node-08/Node-10/Node-13",
                "/Root/Node-01/Node-08/Node-10/Node-14",
                "/Root/Node-01/Node-08/Node-10/Node-15",
                "/Root/Node-01/Node-08/Node-16",
                "/Root/Node-01/Node-17",
                "/Root/Node-01/Node-17/Node-18",
                "/Root/Node-01/Node-17/Node-19",
                "/Root/Node-01/Node-17/Node-20",
                "/Root/Node-01/Node-17/Node-20/Node-21",
                "/Root/Node-01/Node-17/Node-20/Node-22",
            });
        }

        private Dictionary<string, ContentNode> Create4PassTree()
        {
            return CreateTree(new[]
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
        }

        private class TestProgress : IProgress<TransferState>
        {
            public List<double> Log { get; } = new List<double>();
            public List<string> Paths { get; } = new List<string>();

            public void Report(TransferState value)
            {
                Log.Add(value.Percent);
                Paths.Add(value.State.ReaderPath);
            }
        }
    }
}
