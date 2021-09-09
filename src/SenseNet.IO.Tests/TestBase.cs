using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SenseNet.IO.Tests.Implementations;

namespace SenseNet.IO.Tests
{
    public class TestBase
    {
        public Dictionary<string, ContentNode> CreateTree(string[] paths)
        {
            var separator = paths[0].Contains('\\') ? '\\' : '/';

            var contents = new Dictionary<string, ContentNode>();

            var id = 0;
            foreach (var path in paths)
            {
                var name = path.Split(separator)[^1];
                var type = GetContentTypeFromName(name);
                var content = new ContentNode {Path = path, Name = name, Type = type, ["Id"] = ++id};
                content["F1"] = "f1";
                content["F2"] = "f2";
                content["F3"] = "f3";
                content.Permissions = new PermissionInfo();
                contents.Add(path, content);

                var parentPath = path.Substring(0, path.Length - name.Length - 1);
                if (parentPath.Length == 0)
                    continue;

                if(contents.TryGetValue(parentPath, out var parent))
                {
                    parent.Children.Add(content);
                    content.Parent = parent;
                }
            }

            return contents;
        }

        private string GetContentTypeFromName(string name)
        {
            switch (name)
            {
                case "Root": return "PortalRoot";
                case "(apps)": return "SystemFolder";
                case "Content": return "Folder";
                case "IMS": return "SystemFolder";
                case "System": return "SystemFolder";
                case "Settings": return "SystemFolder";
                case "Schema": return "SystemFolder";
                case "Aspects": return "SystemFolder";
                case "ContentTypes": return "SystemFolder";
                default: return name.Split('-')[0];
            }
        }

        protected void AssertSequencesAreEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual)
        {
            var e = string.Join(", ", expected.Select(x => x.ToString()));
            var a = string.Join(", ", actual.Select(x => x.ToString()));
            Assert.AreEqual(e, a);
        }

    }
}
