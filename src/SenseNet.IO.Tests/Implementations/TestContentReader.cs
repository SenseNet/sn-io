using System.Collections.Generic;

namespace SenseNet.IO.Tests.Implementations
{
    public class TestContentReader : IContentReader
    {
        private readonly Dictionary<string, ContentNode> _tree;

        public int EstimatedCount => _tree?.Count ?? 0;


        public TestContentReader(Dictionary<string, ContentNode> tree)
        {
            _tree = tree;
        }


        public IEnumerable<IContent> Read(string path)
        {
            var contents = new List<ContentNode>();
            if (_tree != null)
                Walk(_tree[path], contents);
            return contents;
        }

        private void Walk(ContentNode content, List<ContentNode> contents)
        {
            contents.Add(content);
            foreach (var child in content.Children)
                Walk(child, contents);
        }
    }
}
