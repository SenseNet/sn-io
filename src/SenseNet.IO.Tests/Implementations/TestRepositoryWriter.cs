using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SenseNet.IO.Tests.Implementations
{
    public class TestRepositoryWriter : ISnRepositoryWriter
    {
        public Dictionary<string, ContentNode> Tree { get; }
        private readonly Dictionary<string, WriterState> _states;

        public string ContainerPath { get; }
        public string RootName { get; }

        public TestRepositoryWriter(Dictionary<string, ContentNode> initialTree, Dictionary<string, WriterState> states, string containerPath = null, string rootName = null)
        {
            Tree = initialTree;
            ContainerPath = containerPath ?? "/";
            RootName = rootName;
            _states = states;
        }

        public Task<WriterState> WriteAsync(string path, IContent content, CancellationToken cancel = default)
        {
            var absolutePath = ContentPath.GetAbsolutePath(path, ContainerPath);
            var parentPath = ContentPath.GetParentPath(absolutePath);
            var contentNode = new ContentNode { Name = content.Name, Type = content.Type };
            CopyFieldsAndPermissions(content, contentNode);
            var parent = parentPath == "/" || string.IsNullOrEmpty(parentPath) ? null : Tree[parentPath];

            contentNode.Parent = parent;
            if (parent != null)
            {
                var existing = parent.Children.FirstOrDefault(x => x.Name == content.Name);
                if (existing != null)
                    parent.Children.Remove(existing);
                parent.Children.Add(contentNode);
            }

            var action = Tree.ContainsKey(absolutePath) ? WriterAction.Updated : WriterAction.Created;

            Tree[absolutePath] = contentNode;

            // return mock value once if exists
            if (_states.TryGetValue(absolutePath, out var mockState))
            {
                _states.Remove(absolutePath);
                return Task.FromResult(mockState);
            }
            var state = new WriterState {WriterPath = absolutePath, Action = action};
            return Task.FromResult(state);
        }

        private void CopyFieldsAndPermissions(IContent source, ContentNode target)
        {
            foreach (var fieldName in source.FieldNames)
                target[fieldName] = source[fieldName];
            var permText = JsonConvert.SerializeObject(source.Permissions);
            target.Permissions = JsonConvert.DeserializeObject<PermissionInfo>(permText);
        }
    }
}
