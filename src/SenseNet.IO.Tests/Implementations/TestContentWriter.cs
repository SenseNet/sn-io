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

        public TestContentWriter(Dictionary<string, ContentNode> initialTree, string containerPath = null, string rootName = null)
        {
            Tree = initialTree;
            ContainerPath = containerPath ?? "/";
            RootName = rootName;
        }

        public string ContainerPath { get; }
        public string RootName { get; }


        public Task<WriterState> WriteAsync(string path, IContent content, CancellationToken cancel = default)
        {
            var absolutePath = ContentPath.GetAbsolutePath(path, ContainerPath);
            var parentPath = ContentPath.GetParentPath(absolutePath);
            var contentNode = new ContentNode {Name = content.Name, Type = content.Type};
            //var parent = parentPath == "/" || string.IsNullOrEmpty(parentPath) ? null : Tree[parentPath];
            Tree.TryGetValue(parentPath, out var parent);

            contentNode.Parent = parent;
            if(parent != null)
            {
                var existing = parent.Children.FirstOrDefault(x => x.Name == content.Name);
                if (existing != null)
                {
                    parent.Children.Remove(existing);
                    contentNode.Children.AddRange(existing.Children);
                    existing.Children.Clear();
                    foreach (var child in contentNode.Children)
                        child.Parent = contentNode;
                }
                parent.Children.Add(contentNode);
            }

            Tree[absolutePath] = contentNode;

            return Task.FromResult(new WriterState{WriterPath = absolutePath, Action = WriterAction.Created});
        }
    }
}
