using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SenseNet.IO.Tests.Implementations;

namespace SenseNet.IO.Tests
{
    [TestClass]
    public class WriterTests : TestBase
    {
        [TestMethod]
        public async Task Writer_1()
        {
            var writer = new TestContentWriter(new Dictionary<string, ContentNode>());

            // ACTION
            await writer.WriteAsync("/Root", new ContentNode { Name = "Root", Type = "Root" });
            await writer.WriteAsync("/Root/Folder-1", new ContentNode { Name = "Folder-1", Type = "Folder" });
            await writer.WriteAsync("/Root/Folder-1/File-1", new ContentNode { Name = "File-1", Type = "File" });
            await writer.WriteAsync("/Root/Folder-2", new ContentNode { Name = "Folder-2", Type = "Folder" });

            // ASSERT
            var tree = writer.Tree;
            var root = tree["/Root"];
            var folder1 = tree["/Root/Folder-1"];
            var file1 = tree["/Root/Folder-1/File-1"];
            var folder2 = tree["/Root/Folder-2"];
            Assert.AreEqual(null, root.Parent);
            Assert.AreEqual(root, folder1.Parent);
            Assert.AreEqual(folder1, file1.Parent);
            Assert.AreEqual(root, folder2.Parent);
        }
    }
}
