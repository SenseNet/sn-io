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

        public TestContentWriter(Dictionary<string, ContentNode> tree = null)
        {
            Tree = tree ?? new Dictionary<string, ContentNode>();
        }

        public Task WriteAsync(IContent content, CancellationToken cancel = default)
        {
            var contentNode = (ContentNode) content;
            var parentPath = Path.GetDirectoryName(content.Path)?.Replace('\\', '/') ?? string.Empty;
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

            Tree[content.Path] = contentNode;

            return Task.CompletedTask;
            ;
        }
    }
}
