using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SenseNet.IO.Tests
{
    [TestClass]
    public class ContentPathTests
    {
        [TestMethod]
        public void Path_Absolute()
        {
            Assert.AreEqual("/Root", ContentPath.GetAbsolutePath("/Root", null));
            Assert.AreEqual("/Root", ContentPath.GetAbsolutePath("/Root", string.Empty));
            Assert.AreEqual("/Root/Item", ContentPath.GetAbsolutePath("/Root/Item", "Root"));
            Assert.AreEqual("/Item", ContentPath.GetAbsolutePath("/Item", "/Totally/Irrelevant/If/Relative/Is/Absolute"));

            Assert.AreEqual("/Item", ContentPath.GetAbsolutePath("Item", null));
            Assert.AreEqual("/Item", ContentPath.GetAbsolutePath("Item", string.Empty));
            Assert.AreEqual("/Item", ContentPath.GetAbsolutePath("Item", "/"));
            Assert.AreEqual("Root/Item", ContentPath.GetAbsolutePath("Item", "Root"));
            Assert.AreEqual("/Root/Item", ContentPath.GetAbsolutePath("Item", "/Root"));
            Assert.AreEqual("/Root/Item", ContentPath.GetAbsolutePath("Item", "/Root/"));
            Assert.AreEqual("/Root/Item", ContentPath.GetAbsolutePath("Item", "/Root///"));
        }

        [TestMethod]
        public void Path_Relative()
        {
            Assert.AreEqual("/Root/Item", ContentPath.GetRelativePath("/Root/Item", null));
            Assert.AreEqual("/Root/Item", ContentPath.GetRelativePath("/Root/Item", string.Empty));
            Assert.AreEqual("/Root/Item", ContentPath.GetRelativePath("/Root/Item", "/"));
            Assert.AreEqual("Item", ContentPath.GetRelativePath("/Root/Item", "/Root"));
        }

        [TestMethod]
        public void Path_GetParentPath()
        {
            Assert.AreEqual("", ContentPath.GetParentPath(null));
            Assert.AreEqual("", ContentPath.GetParentPath(""));
            Assert.AreEqual("", ContentPath.GetParentPath("/"));
            Assert.AreEqual("", ContentPath.GetParentPath("/Root"));
            Assert.AreEqual("/Root", ContentPath.GetParentPath("/Root/Item"));

            Assert.AreEqual("", ContentPath.GetParentPath("Root"));
            Assert.AreEqual("Root", ContentPath.GetParentPath("Root/Item"));
        }
    }
}
