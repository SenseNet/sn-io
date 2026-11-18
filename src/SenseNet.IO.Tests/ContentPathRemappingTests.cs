using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using SenseNet.IO.Implementations;
using SenseNet.IO.Tests.Implementations;

namespace SenseNet.IO.Tests
{
    [TestClass]
    public class ContentPathRemappingTests
    {
        [TestMethod]
        public void AliasesUseLongestCompleteAncestorAndDoNotRewriteOtherRoots()
        {
            var paths = new ContentPathMap();
            paths.Record("/Root/Content/Új gyökér", "/Root/Content/canonical");
            paths.Record("/Root/Content/Új gyökér/Hírek", "/Root/Content/canonical/news");
            paths.Record("/Root/Content/Új gyökér/Hírek/Ősz", "/Root/Content/canonical/news/autumn");
            Assert.AreEqual("/Root/Content/canonical/news/autumn/Kép.jpg",
                paths.Resolve("/Root/Content/Új gyökér/Hírek/Ősz/Kép.jpg"));
            Assert.AreEqual("/Root/Content/canonical/Hírek-extra/Kép.jpg",
                paths.Resolve("/Root/Content/Új gyökér/Hírek-extra/Kép.jpg"));
            Assert.AreEqual("/Root/Content/Új gyökér-extra/Hírek",
                paths.Resolve("/Root/Content/Új gyökér-extra/Hírek"));
            Assert.AreEqual("/Root/Content/old-site/Hírek", paths.Resolve("/Root/Content/old-site/Hírek"));
        }

        [TestMethod]
        public void OnlyServerIdentifiedReferencesAndPermissionIdentitiesAreRemapped()
        {
            const string original = "/Root/Content/Hírek/Ősz";
            const string actual = "/Root/Content/news/autumn";
            var paths = new ContentPathMap();
            paths.Record(original, actual);
            var source = new ContentNode { Name = "Hírek", Type = "Folder" };
            source["Name"] = "Hírek";
            source["RelatedContent"] = new JArray(original, 42, "/Root/Content/other/Hírek");
            source["ImageRef"] = original;
            source["Query"] = "InTree:'" + original + "'";
            source["Body"] = original;
            source["Choices"] = new JArray(original);
            source["Structured"] = new JObject { ["path"] = original };
            source.Permissions = new PermissionInfo { Entries = new[] { new AceInfo
            {
                Identity = original,
                Permissions = new Dictionary<string, string> { ["See"] = "Allow" }
            } } };
            var request = new ImportContent(source, "news", paths,
                new[] { "RelatedContent", "ImageRef", "Structured" }, true, true);
            Assert.AreEqual(actual, ((JArray)request["RelatedContent"])[0].Value<string>());
            Assert.AreEqual(42, ((JArray)request["RelatedContent"])[1].Value<int>());
            Assert.AreEqual("/Root/Content/other/Hírek", ((JArray)request["RelatedContent"])[2].Value<string>());
            Assert.AreEqual(actual, request["ImageRef"]);
            Assert.AreEqual(source["Query"], request["Query"]);
            Assert.AreEqual(original, request["Body"]);
            Assert.AreSame(source["Choices"], request["Choices"]);
            Assert.AreSame(source["Structured"], request["Structured"]);
            Assert.AreEqual(actual, request.Permissions.Entries[0].Identity);
            Assert.AreEqual("news", request["Name"]);
            Assert.AreEqual("Hírek", source.Name);
            Assert.AreEqual(original, source.Permissions.Entries[0].Identity);
            Assert.AreEqual(original, ((JArray)source["RelatedContent"])[0].Value<string>());
        }
    }
}
