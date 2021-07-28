using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SenseNet.IO.Tests.Implementations
{
    public class TestContentWriter : IContentWriter
    {
        private readonly string _rootPath;
        public Dictionary<string, ContentNode> Tree { get; }

        public TestContentWriter(Dictionary<string, ContentNode> tree, string rootPath = null)
        {
            Tree = tree;
            _rootPath = rootPath ?? string.Empty;
        }

        public Task WriteAsync(string relativePath, IContent content, CancellationToken cancel = default)
        {
            var path = ContentPath.GetAbsolutePath(relativePath, _rootPath);
            var contentNode = (ContentNode) content;
            var parentPath = ContentPath.GetParentPath(path);
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
