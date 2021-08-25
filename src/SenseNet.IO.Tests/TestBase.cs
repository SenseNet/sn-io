using System.Collections.Generic;
using SenseNet.IO.Tests.Implementations;

namespace SenseNet.IO.Tests
{
    public class TestBase
    {
        public Dictionary<string, ContentNode> CreateTree(string[] paths)
        {
            var contents = new Dictionary<string, ContentNode>();

            var id = 0;
            foreach (var path in paths)
            {
                var name = path.Split('/')[^1];
                var type = GetContentTypeFromName(name);
                var content = new ContentNode {Path = path, Name = name, Type = type, ["Id"] = ++id};
                contents.Add(path, content);

                var parentPath = path.Substring(0, path.Length - name.Length - 1);
                if (parentPath.Length == 0)
                    continue;

                var parent = contents[parentPath];
                parent.Children.Add(content);
                content.Parent = parent;
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
    }
}
