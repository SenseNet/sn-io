using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SenseNet.IO.Implementations;

namespace SenseNet.IO.Tests.Implementations
{
    public class TestRepositoryWriter : ISnRepositoryWriter
    {
        public Dictionary<string, ContentNode> Tree { get; }
        private readonly Dictionary<string, WriterState> _states;
        private string[] _badContentPaths;

        public string ContainerPath { get; }
        public string RootName { get; }

        public RepositoryWriterArgs WriterOptions => throw new NotImplementedException();

        public TestRepositoryWriter(Dictionary<string, ContentNode> initialTree, Dictionary<string, WriterState> states, string containerPath = null, string rootName = null, string[] badContentPaths = null)
        {
            Tree = initialTree;
            ContainerPath = containerPath ?? "/";
            RootName = rootName;
            _states = states;
            _badContentPaths = badContentPaths;
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public Task<WriterState> WriteAsync(string path, IContent content, CancellationToken cancel = default)
        {
            var absolutePath = ContentPath.GetAbsolutePath(path, ContainerPath);
            if (_badContentPaths != null && _badContentPaths.Contains(absolutePath))
            {
                return Task.FromResult(new WriterState
                {
                    WriterPath = absolutePath,
                    Action = WriterAction.Failed,
                    Messages = new[] {"ErrorMessage1"}
                });
            }
            var parentPath = ContentPath.GetParentPath(absolutePath);
            var contentNode = new ContentNode { Name = content.Name, Type = content.Type };
            CopyFieldsAndPermissions(content, contentNode);
            ContentNode parent = null;
            if (parentPath != "/" && !string.IsNullOrEmpty(parentPath))
                if (!Tree.TryGetValue(parentPath, out parent))
                    return Task.FromResult(new WriterState
                    {
                        WriterPath = absolutePath,
                        Action = WriterAction.MissingParent,
                        Messages = new[] { $"Cannot create content {ContentPath.GetName(absolutePath)}, " +
                                           $"parent not found: {parentPath}" }
                    });

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

        public Task<bool> ShouldSkipSubtreeAsync(string path, CancellationToken cancel = default)
        {
            var rootPath = ContainerPath == "/" ? "/Root" : ContainerPath;
            var absolutePath = ContentPath.GetAbsolutePath(path, rootPath);
            return Task.FromResult(!Tree.ContainsKey(absolutePath));
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
