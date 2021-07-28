using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SenseNet.IO.Tests.Implementations;

namespace SenseNet.IO.Tests
{
    [TestClass]
    public class ContentFlowTests : TestBase
    {
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
            var flow = new ContentFlow<ContentNode>(new TestContentReader("/Root", sourceTree), new TestContentWriter(targetTree));

            // ACTION
            var progress = new TestProgress();
            await flow.TransferAsync(progress);

            // ASSERT
            Assert.AreEqual(sourceTree.Count, targetTree.Count);
            Assert.AreEqual(targetTree.Count, progress.Log.Count);
        }
        [TestMethod]
        public async Task Flow_SubTree()
        {
            var sourceTree = CreateTree(new[]
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
            var targetTree = CreateTree(new[]
            {
                "/Root",
                "/Root/Node-01",
            });
            var flow = new ContentFlow<ContentNode>(
                new TestCQReader("/Root/Node-01/Node-08", 4, sourceTree),
                new TestContentWriter(targetTree));

            // ACTION
            var progress = new TestProgress();
            await flow.TransferAsync(progress);

            // ASSERT
            Assert.AreEqual(11, targetTree.Count);
            Assert.AreEqual(9, progress.Log.Count);
        }
        [TestMethod]
        public async Task Flow_2()
        {
            var sourceTree = CreateTree(new[]
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
            var targetTree = new Dictionary<string, ContentNode>();
            var flow = new ContentFlow<ContentNode>(new TestCQReader("/Root", 4, sourceTree), new TestContentWriter(targetTree));

            // ACTION
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
            var flow = new ContentFlow<ContentNode>(new TestCQReader("/Root/aa", 4, sourceTree), new TestContentWriter(targetTree));

            // ACTION
            var progress = new TestProgress();
            await flow.TransferAsync(progress);

            // ASSERT
            Assert.AreEqual(3, targetTree.Count);
            Assert.AreEqual(2, progress.Log.Count);
        }

        private class TestProgress : IProgress<double>
        {
            public List<double> Log { get; } = new List<double>();

            public void Report(double value)
            {
                Log.Add(value);
            }
        }
    }
}
