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
            ContainerPath = containerPath ?? "/";
            RootName = rootName;
        }

        public string ContainerPath { get; }
        public string RootName { get; }

        public Task<ImportResponse> WriteAsync(string path, IContent content, CancellationToken cancel = default)
        {
            var absolutePath = ContentPath.GetAbsolutePath(path, ContainerPath);
            var parentPath = ContentPath.GetParentPath(absolutePath);
            var contentNode = new ContentNode {Name = content.Name, Type = content.Type};
            var parent = parentPath == "/" || string.IsNullOrEmpty(parentPath) ? null : Tree[parentPath];

            contentNode.Parent = parent;
            if(parent != null)
            {
                var existing = parent.Children.FirstOrDefault(x => x.Name == content.Name);
                if (existing != null)
                    parent.Children.Remove(existing);
                parent.Children.Add(contentNode);
            }


            Tree[absolutePath] = contentNode;

            return Task.FromResult(new ImportResponse{WriterPath = absolutePath, Action = ImporterAction.Create});
        }
    }
}
