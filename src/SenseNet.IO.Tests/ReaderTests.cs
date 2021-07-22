using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SenseNet.IO.Tests.Implementations;

namespace SenseNet.IO.Tests
{
    [TestClass]
    public class ReaderTests : TestBase
    {
        [TestMethod]
        public void Reader_1()
        {
            var tree = CreateTree(new[]
            {
                "/Root",
                "/Root/Folder-1",
                "/Root/Folder-1/File-1",
                "/Root/Folder-2",
            });

            // ACTION
            var reader = new TestContentReader(tree);
            var result = reader.Read("/Root/Folder-1");

            // ASSERT
            var contents = result.ToArray();
            Assert.AreEqual(2, contents.Length);
            Assert.AreEqual("Folder-1", contents[0].Name);
            Assert.AreEqual(2, (int)contents[0]["Id"]);
            Assert.AreEqual("File-1", contents[1].Name);
            Assert.AreEqual(3, (int)contents[1]["Id"]);
        }

    }
}
