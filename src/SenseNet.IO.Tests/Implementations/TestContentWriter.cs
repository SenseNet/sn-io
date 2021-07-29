using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SenseNet.IO.Tests.Implementations
{
    public class TestContentWriter : IContentWriter
    {
        public Dictionary<string, ContentNode> Tree { get; }

        public TestContentWriter(Dictionary<string, ContentNode> tree, string containerPath = null, string rootName = null)
        {
            Tree = tree;
            ContainerPath = containerPath ?? string.Empty;
            RootName = rootName;
        }

        public string ContainerPath { get; }
        public string RootName { get; }

        public Task WriteAsync(string relativePath, IContent content, CancellationToken cancel = default)
        {
            var path = ContentPath.GetAbsolutePath(relativePath, ContainerPath);
            var parentPath = ContentPath.GetParentPath(path);
            var contentNode = (ContentNode) content;
            var parent = parentPath == "/" || parentPath == string.Empty
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

            return Task.CompletedTask;
        }
    }
}
