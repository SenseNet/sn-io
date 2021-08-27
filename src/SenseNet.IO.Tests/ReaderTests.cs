using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SenseNet.IO.Tests.Implementations;

namespace SenseNet.IO.Tests
{
    [TestClass]
    public class ReaderTests : TestBase
    {
        [TestMethod]
        public async Task Reader_1()
        {
            var tree = CreateTree(new[]
            {
                "/Root",
                "/Root/Folder-1",
                "/Root/Folder-1/File-1",
                "/Root/Folder-2",
            });

            // ACTION
            var reader = new TestContentReader("/Root/Folder-1", tree);
            var result = new List<string>();
            while (await reader.ReadAllAsync())
                result.Add(reader.Content.Path);

            // ASSERT
            var paths = result.ToArray();
            Assert.AreEqual(2, paths.Length);
            Assert.AreEqual("/Root/Folder-1", paths[0]);
            Assert.AreEqual("/Root/Folder-1/File-1", paths[1]);
        }

    }
}
