using System.IO;
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
            Assert.AreEqual(null, ContentPath.GetParentPath(null));
            Assert.AreEqual(null, ContentPath.GetParentPath(""));
            Assert.AreEqual(null, ContentPath.GetParentPath("/"));
            Assert.AreEqual("/", ContentPath.GetParentPath("/Root"));
            Assert.AreEqual("/Root", ContentPath.GetParentPath("/Root/Item"));

            Assert.AreEqual("", ContentPath.GetParentPath("Root"));
            Assert.AreEqual("Root", ContentPath.GetParentPath("Root/Item"));
        }

        [TestMethod]
        public void Path_GetName()
        {
            Assert.AreEqual("", ContentPath.GetName(""));
            Assert.AreEqual("", ContentPath.GetName("/"));
            Assert.AreEqual("Root", ContentPath.GetName("Root"));
            Assert.AreEqual("Item", ContentPath.GetName("Root/Item"));
            Assert.AreEqual("Root", ContentPath.GetName("/Root"));
            Assert.AreEqual("Item", ContentPath.GetName("/Root/Item"));
        }
        [TestMethod]
        public void Path_Combine()
        {
            Assert.AreEqual("/C", ContentPath.Combine("/A", "/B", "/C"));
            Assert.AreEqual("/B/C", ContentPath.Combine("/A", "/B", "C"));
            Assert.AreEqual("/A/B/C", ContentPath.Combine("/A", "B", "C"));
            Assert.AreEqual("A/B/C", ContentPath.Combine("A", "B", "C"));

            Assert.AreEqual("/C/", ContentPath.Combine("/A/", "/B/", "/C/"));
            Assert.AreEqual("/B/C/", ContentPath.Combine("/A/", "/B/", "C/"));
            Assert.AreEqual("/A/B/C/", ContentPath.Combine("/A/", "B/", "C/"));
            Assert.AreEqual("A/B/C/", ContentPath.Combine("A/", "B/", "C/"));

            Assert.AreEqual("A/B", ContentPath.Combine("A", "B", ""));
            Assert.AreEqual("A/C", ContentPath.Combine("A", "", "C"));
            Assert.AreEqual("B/C", ContentPath.Combine("", "B", "C"));
            Assert.AreEqual("", ContentPath.Combine("", "", ""));
        }
    }
}
