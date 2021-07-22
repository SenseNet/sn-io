using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SenseNet.IO.Tests.Implementations
{
    public class TestContentWriter : IContentWriter
    {
        public Dictionary<string, ContentNode> Tree { get; }

        public TestContentWriter(Dictionary<string, ContentNode> tree = null)
        {
            Tree = tree ?? new Dictionary<string, ContentNode>();
        }

        public void Write(string path, IContent content)
        {
            var contentNode = (ContentNode) content;
            var parentPath = Path.GetDirectoryName(path).Replace('\\', '/');
            var parent = parentPath == "/"
                ? null
                : Tree[parentPath];

            contentNode.Parent = parent;
            if(parent != null)
            {
                var existing = parent.Children.FirstOrDefault(x => x.Name == content.Name);
                if (existing != null)
                    parent.Children.Remove(existing);
                parent.Children.Add(contentNode);
            }

            Tree[path] = contentNode;
        }
    }
}
