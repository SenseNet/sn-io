using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SenseNet.IO.Tests.Implementations;

namespace SenseNet.IO.Tests
{
    [TestClass]
    public class WriterTests : TestBase
    {
        [TestMethod]
        public void Writer_1()
        {
            var writer = new TestContentWriter(new Dictionary<string, ContentNode>());

            // ACTION
            writer.Write("/Root", new ContentNode { Name = "Root", Type = "Root" });
            writer.Write("/Root/Folder-1", new ContentNode { Name = "Folder-1", Type = "Folder" });
            writer.Write("/Root/Folder-1/File-1", new ContentNode { Name = "File-1", Type = "File" });
            writer.Write("/Root/Folder-2", new ContentNode { Name = "Folder-2", Type = "Folder" });

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
