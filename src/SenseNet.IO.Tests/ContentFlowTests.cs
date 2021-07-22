using System;
using System.Collections.Generic;
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
            var flow = new ContentFlow(new TestContentReader(sourceTree), new TestContentWriter(targetTree));

            // ACTION
            var progress = new TestProgress();
            await flow.TransferAsync("/Root", "/Root", progress);

            // ASSERT
            Assert.AreEqual(sourceTree.Count, targetTree.Count);
            Assert.AreEqual(targetTree.Count, progress.Log.Count);
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
