using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SenseNet.Client;
//using SenseNet.Client;
using SenseNet.IO.Implementations;
using SenseNet.IO.Tests.Implementations;

namespace SenseNet.IO.Tests
{
    [TestClass]
    public class RepositoryReaderTests : TestBase
    {
        private class RepositoryReaderMock : RepositoryReader
        {
            private StringComparison SC = StringComparison.OrdinalIgnoreCase;
            public static RepositoryReaderMock Create(Dictionary<string, ContentNode> sourceTree, string rootPath, string filter, int blockSize = 10)
            {
                return new RepositoryReaderMock(sourceTree, Options.Create(new RepositoryReaderArgs
                {
                    Url = "https://example.com",
                    Path = rootPath,
                    Filter = filter,
                    BlockSize = blockSize
                }));
            }

            private Dictionary<string, ContentNode> _sourceTree;
            public List<string> Queries { get; } = new List<string>();

            public RepositoryReaderMock(Dictionary<string, ContentNode> sourceTree,
                IOptions<RepositoryReaderArgs> args) : base(null, args, NullLogger<RepositoryReader>.Instance)
            {
                _sourceTree = sourceTree;
            }

            protected override Task<int> GetCountAsync()
            {
                return Task.FromResult(_sourceTree.Count);
            }
            protected override Task<IContent[]> QueryAsync(string queryText)
            {
                Queries.Add(queryText);

                var exclusion = new string[0];
                if (queryText.StartsWith("Path:"))
                {
                    var inTreeIndex = queryText.IndexOf(" (+InTree:", SC);
                    if (inTreeIndex < 0)
                    {

                    }
                    else
                    {
                        // Path:(/Root/System/Schema/ContentTypes /Root/System/Settings /Root/System/Schema/Aspects)
                        //   (+InTree:/Root -InTree:(/Root/System/Schema/ContentTypes /Root/System/Settings /Root/System/Schema/Aspects))
                        //   .AUTOFILTERS:OFF .SORT:Path .SKIP:0 .TOP:10000

                        exclusion = queryText
                            .Substring(5, inTreeIndex)
                            .Trim('(', ')')
                            .Split(' ')
                            .Select(x => x.Trim('\'') + "/")
                            .ToArray();
                    }
                }

                int p0, p1;
                string path;
                if (queryText.Contains("InTree:"))
                {
                    // InTree:'/Root/System/Schema/ContentTypes' .SORT:Path .TOP:10 .SKIP:0 .AUTOFILTERS:OFF
                    p0 = queryText.IndexOf("InTree:", SC) + 7;
                    p1 = queryText.IndexOf(" ", p0, SC);
                    path = queryText.Substring(p0, p1 - p0).Trim('\'');
                }
                else
                {
                    p0 = queryText.IndexOf("Path:", SC) + 5;
                    p1 = queryText.IndexOf(" ", p0, SC);
                    path = queryText.Substring(p0, p1 - p0).Trim('\'');
                }

                var skip = 0;
                if (queryText.Contains(".SKIP:"))
                {
                    p0 = queryText.IndexOf(".SKIP:", SC) + 6;
                    p1 = queryText.IndexOf(" ", p0, SC);
                    skip = int.Parse(queryText.Substring(p0, p1 - p0));
                }

                var top = 0;
                if (queryText.Contains(".TOP:"))
                {
                    p0 = queryText.IndexOf(".TOP:", SC) + 5;
                    p1 = queryText.IndexOf(" ", p0, SC);
                    top = p0 < 0 ? 0 : int.Parse(queryText.Substring(p0, p1 - p0));
                }

                IContent[] items;
                if (queryText.Contains("+(+TypeIs:File +InTree:(/Root/Content/Docs/F1/F2 /Root/Content/Docs/F1/F3))"))
                {
                    var r = _sourceTree
                        .Where(x => x.Key == path || x.Key.StartsWith(path))
                        .ToArray();
                    r = r
                        .Where(x => x.Value.Type == "File")
                        .ToArray();
                    r = r
                        .Where(x => x.Key.StartsWith("/Root/Content/Docs/F1/F2/") ||
                                                              x.Key.StartsWith("/Root/Content/Docs/F1/F3/"))
                        .ToArray();
                    r = r
                        .Where(x => exclusion.All(excluded => !x.Key.StartsWith(excluded, SC)))
                        .ToArray();
                    items = r
                        .OrderBy(x => x.Key)
                        .Skip(skip)
                        .Take(top)
                        .Select(x => x.Value)
                        .ToArray();
                    return Task.FromResult((IContent[]) items);
                }

                items = _sourceTree
                    .Where(x => x.Key == path || x.Key.StartsWith(path))
                    .Where(x => exclusion.All(excluded => !x.Key.StartsWith(excluded, SC)))
                    .OrderBy(x => x.Key)
                    .Skip(skip)
                    .Take(top)
                    .Select(x => x.Value)
                    .ToArray();

                return Task.FromResult((IContent[])items);
            }
            protected override Task<IContent> GetContentAsync(string path, string[] fields)
            {
                Queries.Add(path);
                return Task.FromResult((IContent)_sourceTree[path]);
            }
        }

        /* ----------------------------------------------------------------------- q:\io\Root */

        [TestMethod]
        public async Task RepoReader_Root()
        {
            var sourceTree = CreateSourceTree(@"/");
            var targetTree = CreateTree(new[] { "/Root", "/Root/IMS" });
            var targetStates = new Dictionary<string, WriterState>();

            // ACTION
            var reader = RepositoryReaderMock.Create(sourceTree, "/Root", null, 99999);
            var writer = new TestRepositoryWriter(targetTree, targetStates);
            var flow = new SemanticContentFlow(reader, writer, GetLogger<ContentFlow>());
            var progress = new TestProgress();
            await flow.TransferAsync(progress);

            // ASSERT
            var expected = sourceTree.Keys
                //.Select(x => x.Substring("q:\\io".Length).Replace('\\', '/'))
                .OrderBy(x => x)
                .ToArray();
            var actual = targetTree.Keys
                .OrderBy(x => x)
                .ToArray();
            AssertSequencesAreEqual(expected, actual);

            expected = new[]
            {
                "InTree:'/Root/System/Schema/ContentTypes' .SORT:Path .TOP:99999 .SKIP:0 .AUTOFILTERS:OFF",
                "InTree:'/Root/System/Schema/ContentTypes' .SORT:Path .TOP:99999 .SKIP:99999 .AUTOFILTERS:OFF",
                "InTree:'/Root/System/Settings' .SORT:Path .TOP:99999 .SKIP:0 .AUTOFILTERS:OFF",
                "InTree:'/Root/System/Settings' .SORT:Path .TOP:99999 .SKIP:99999 .AUTOFILTERS:OFF",
                "InTree:'/Root/System/Schema/Aspects' .SORT:Path .TOP:99999 .SKIP:0 .AUTOFILTERS:OFF",
                "InTree:'/Root/System/Schema/Aspects' .SORT:Path .TOP:99999 .SKIP:99999 .AUTOFILTERS:OFF",
                "Path:('/Root/System/Schema/ContentTypes' '/Root/System/Settings' '/Root/System/Schema/Aspects') (+InTree:'/Root' -InTree:('/Root/System/Schema/ContentTypes' '/Root/System/Settings' '/Root/System/Schema/Aspects')) .SORT:Path .TOP:99999 .SKIP:0 .AUTOFILTERS:OFF",
                "Path:('/Root/System/Schema/ContentTypes' '/Root/System/Settings' '/Root/System/Schema/Aspects') (+InTree:'/Root' -InTree:('/Root/System/Schema/ContentTypes' '/Root/System/Settings' '/Root/System/Schema/Aspects')) .SORT:Path .TOP:99999 .SKIP:99999 .AUTOFILTERS:OFF",
            };
            actual = reader.Queries.ToArray();
            AssertSequencesAreEqual(expected, actual);
        }
        [TestMethod]
        public async Task RepoReader_RootSystem()
        {
            var sourceTree = CreateSourceTree("/");
            var targetTree = CreateTree(new[] { "/Root", "/Root/System" });
            var targetStates = new Dictionary<string, WriterState>();

            // ACTION
            var reader = RepositoryReaderMock.Create(sourceTree, "/Root/System", null, 99999);
            var writer = new TestRepositoryWriter(targetTree, targetStates, "/Root");
            var flow = new SemanticContentFlow(reader, writer, GetLogger<ContentFlow>());
            var progress = new TestProgress();
            await flow.TransferAsync(progress);

            // ASSERT
            var expected = sourceTree.Keys
                .Where(x => x == "/Root" ||
                           x == "/Root/System" ||
                           x.StartsWith("/Root/System/"))
                .OrderBy(x => x)
                .ToArray();
            var actual = targetTree.Keys
                .OrderBy(x => x)
                .ToArray();
            AssertSequencesAreEqual(expected, actual);

            expected = new[]
            {
                "InTree:'/Root/System/Schema/ContentTypes' .SORT:Path .TOP:99999 .SKIP:0 .AUTOFILTERS:OFF",
                "InTree:'/Root/System/Schema/ContentTypes' .SORT:Path .TOP:99999 .SKIP:99999 .AUTOFILTERS:OFF",
                "InTree:'/Root/System/Settings' .SORT:Path .TOP:99999 .SKIP:0 .AUTOFILTERS:OFF",
                "InTree:'/Root/System/Settings' .SORT:Path .TOP:99999 .SKIP:99999 .AUTOFILTERS:OFF",
                "InTree:'/Root/System/Schema/Aspects' .SORT:Path .TOP:99999 .SKIP:0 .AUTOFILTERS:OFF",
                "InTree:'/Root/System/Schema/Aspects' .SORT:Path .TOP:99999 .SKIP:99999 .AUTOFILTERS:OFF",
                "Path:('/Root/System/Schema/ContentTypes' '/Root/System/Settings' '/Root/System/Schema/Aspects') (+InTree:'/Root/System' -InTree:('/Root/System/Schema/ContentTypes' '/Root/System/Settings' '/Root/System/Schema/Aspects')) .SORT:Path .TOP:99999 .SKIP:0 .AUTOFILTERS:OFF",
                "Path:('/Root/System/Schema/ContentTypes' '/Root/System/Settings' '/Root/System/Schema/Aspects') (+InTree:'/Root/System' -InTree:('/Root/System/Schema/ContentTypes' '/Root/System/Settings' '/Root/System/Schema/Aspects')) .SORT:Path .TOP:99999 .SKIP:99999 .AUTOFILTERS:OFF",
            };
            actual = reader.Queries.ToArray();
            AssertSequencesAreEqual(expected, actual);
        }
        [TestMethod]
        public async Task RepoReader_RootSystemSchema()
        {
            var sourceTree = CreateSourceTree(@"/");
            var targetTree = CreateTree(new[] { "/Root" });
            var targetStates = new Dictionary<string, WriterState>();

            // ACTION
            var reader = RepositoryReaderMock.Create(sourceTree, "/Root/System/Schema", null, 99999);
            var writer = new TestRepositoryWriter(targetTree, targetStates, "/Root/System");
            var flow = new SemanticContentFlow(reader, writer, GetLogger<ContentFlow>());
            var progress = new TestProgress();
            await flow.TransferAsync(progress);

            // ASSERT
            var expected = sourceTree.Keys
                .Where(x => x == "/Root" ||
                            x == "/Root/System" ||
                            x == "/Root/System/Schema" ||
                            x.StartsWith("/Root/System/Schema/"))
                .OrderBy(x => x)
                .ToArray();
            var actual = targetTree.Keys
                .OrderBy(x => x)
                .ToArray();
            AssertSequencesAreEqual(expected, actual);

            expected = new[]
            {
                "InTree:'/Root/System/Schema/ContentTypes' .SORT:Path .TOP:99999 .SKIP:0 .AUTOFILTERS:OFF",
                "InTree:'/Root/System/Schema/ContentTypes' .SORT:Path .TOP:99999 .SKIP:99999 .AUTOFILTERS:OFF",
                "InTree:'/Root/System/Schema/Aspects' .SORT:Path .TOP:99999 .SKIP:0 .AUTOFILTERS:OFF",
                "InTree:'/Root/System/Schema/Aspects' .SORT:Path .TOP:99999 .SKIP:99999 .AUTOFILTERS:OFF",
                "Path:('/Root/System/Schema/ContentTypes' '/Root/System/Schema/Aspects') (+InTree:'/Root/System/Schema' -InTree:('/Root/System/Schema/ContentTypes' '/Root/System/Schema/Aspects')) .SORT:Path .TOP:99999 .SKIP:0 .AUTOFILTERS:OFF",
                "Path:('/Root/System/Schema/ContentTypes' '/Root/System/Schema/Aspects') (+InTree:'/Root/System/Schema' -InTree:('/Root/System/Schema/ContentTypes' '/Root/System/Schema/Aspects')) .SORT:Path .TOP:99999 .SKIP:99999 .AUTOFILTERS:OFF",
            };
            actual = reader.Queries.ToArray();
            AssertSequencesAreEqual(expected, actual);
        }
        [TestMethod]
        public async Task RepoReader_RootSystemSchemaContentTypes()
        {
            var sourceTree = CreateSourceTree("/");
            var targetTree = CreateTree(new[] { "/Root" });
            var targetStates = new Dictionary<string, WriterState>();

            // ACTION
            var reader = RepositoryReaderMock.Create(sourceTree, "/Root/System/Schema/ContentTypes", null, 99999);
            var writer = new TestRepositoryWriter(targetTree, targetStates, "/Root/System/Schema");
            var flow = new SemanticContentFlow(reader, writer, GetLogger<ContentFlow>());
            var progress = new TestProgress();
            await flow.TransferAsync(progress);

            // ASSERT
            var expected = sourceTree.Keys
                .Where(x => x == "/Root" ||
                            x == "/Root/System" ||
                            x == "/Root/System/Schema" ||
                            x == "/Root/System/Schema/ContentTypes" ||
                            x.StartsWith("/Root/System/Schema/ContentTypes/"))
                .OrderBy(x => x)
                .ToArray();
            var actual = targetTree.Keys
                .OrderBy(x => x)
                .ToArray();
            AssertSequencesAreEqual(expected, actual);

            expected = new[]
            {
                "InTree:'/Root/System/Schema/ContentTypes' .SORT:Path .TOP:99999 .SKIP:0 .AUTOFILTERS:OFF",
                "InTree:'/Root/System/Schema/ContentTypes' .SORT:Path .TOP:99999 .SKIP:99999 .AUTOFILTERS:OFF",
                "Path:'/Root/System/Schema/ContentTypes' .AUTOFILTERS:OFF",
            };
            actual = reader.Queries.ToArray();
            AssertSequencesAreEqual(expected, actual);
        }
        [TestMethod]
        public async Task RepoReader_RootSystemSchemaAspects()
        {
            var sourceTree = CreateSourceTree(@"/");
            var targetTree = CreateTree(new[] { "/Root" });
            var targetStates = new Dictionary<string, WriterState>();

            // ACTION
            var reader = RepositoryReaderMock.Create(sourceTree, "/Root/System/Schema/Aspects", null, 99999);
            var writer = new TestRepositoryWriter(targetTree, targetStates, "/Root/System/Schema");
            var flow = new SemanticContentFlow(reader, writer, GetLogger<ContentFlow>());
            var progress = new TestProgress();
            await flow.TransferAsync(progress);

            // ASSERT
            var expected = sourceTree.Keys
                .Where(x => x == "/Root" ||
                            x == "/Root/System" ||
                            x == "/Root/System/Schema" ||
                            x == "/Root/System/Schema/Aspects" ||
                            x.StartsWith("/Root/System/Schema/Aspects/"))
                .OrderBy(x => x)
                .ToArray();
            var actual = targetTree.Keys
                .OrderBy(x => x)
                .ToArray();
            AssertSequencesAreEqual(expected, actual);

            expected = new[]
            {
                "InTree:'/Root/System/Schema/Aspects' .SORT:Path .TOP:99999 .SKIP:0 .AUTOFILTERS:OFF",
                "InTree:'/Root/System/Schema/Aspects' .SORT:Path .TOP:99999 .SKIP:99999 .AUTOFILTERS:OFF",
                "Path:'/Root/System/Schema/Aspects' .AUTOFILTERS:OFF",
            };
            actual = reader.Queries.ToArray();
            AssertSequencesAreEqual(expected, actual);
        }
        [TestMethod]
        public async Task RepoReader_RootSystemSettings()
        {
            var sourceTree = CreateSourceTree(@"/");
            var targetTree = CreateTree(new[] { "/Root", "/Root/System" });
            var targetStates = new Dictionary<string, WriterState>();

            // ACTION
            var reader = RepositoryReaderMock.Create(sourceTree, "/Root/System/Settings", null, 99999);
            var writer = new TestRepositoryWriter(targetTree, targetStates, "/Root/System");
            var flow = new SemanticContentFlow(reader, writer, GetLogger<ContentFlow>());
            var progress = new TestProgress();
            await flow.TransferAsync(progress);

            // ASSERT
            var expected = sourceTree.Keys
                .Where(x => x == @"/Root" ||
                            x == @"/Root/System" ||
                            x == @"/Root/System/Settings" ||
                            x.StartsWith(@"/Root/System/Settings/"))
                .OrderBy(x => x)
                .ToArray();
            var actual = targetTree.Keys
                .OrderBy(x => x)
                .ToArray();
            AssertSequencesAreEqual(expected, actual);

            expected = new[]
            {
                "InTree:'/Root/System/Settings' .SORT:Path .TOP:99999 .SKIP:0 .AUTOFILTERS:OFF",
                "InTree:'/Root/System/Settings' .SORT:Path .TOP:99999 .SKIP:99999 .AUTOFILTERS:OFF",
                "Path:'/Root/System/Settings' .AUTOFILTERS:OFF",
            };
            actual = reader.Queries.ToArray();
            AssertSequencesAreEqual(expected, actual);
        }
        [TestMethod]
        public async Task RepoReader_RootContent()
        {
            var sourceTree = CreateSourceTree(@"/");
            var targetTree = CreateTree(new[] { "/Root" });
            var targetStates = new Dictionary<string, WriterState>();

            // ACTION
            var reader = RepositoryReaderMock.Create(sourceTree, "/Root/Content", null, 99999);
            var writer = new TestRepositoryWriter(targetTree, targetStates, "/Root");
            var flow = new SemanticContentFlow(reader, writer, GetLogger<ContentFlow>());
            var progress = new TestProgress();
            await flow.TransferAsync(progress);

            // ASSERT
            var expected = sourceTree.Keys
                .Where(x => x == "/Root" ||
                            x == "/Root/Content" ||
                            x.StartsWith("/Root/Content/"))
                .OrderBy(x => x)
                .ToArray();
            var actual = targetTree.Keys
                .OrderBy(x => x)
                .ToArray();
            AssertSequencesAreEqual(expected, actual);

            expected = new[]
            {
                "InTree:'/Root/Content' .SORT:Path .TOP:99999 .SKIP:0 .AUTOFILTERS:OFF",
                "InTree:'/Root/Content' .SORT:Path .TOP:99999 .SKIP:99999 .AUTOFILTERS:OFF",
            };
            actual = reader.Queries.ToArray();
            AssertSequencesAreEqual(expected, actual);
        }

        /* =========================================================================================== UPDATE REFERENCES TESTS */

        [TestMethod]
        public async Task RepoReader_UpdateReferences()
        {
            var sourceTree = CreateSourceTree(@"/");
            var targetTree = CreateTree(new[] { "/Root", "/Root/IMS" });
            var targetStates = new Dictionary<string, WriterState>
            {
                {
                    "/Root/System/Schema/ContentTypes/ContentType-1/ContentType-4",
                    new WriterState
                        {Action = WriterAction.Creating, BrokenReferences = new string[0], RetryPermissions = true}
                },
                {
                    "/Root/System/Settings/Settings-2.settings",
                    new WriterState
                        {Action = WriterAction.Creating, BrokenReferences = new[] {"F2", "F3"}, RetryPermissions = false}
                },
                {
                    "/Root/Content/Workspace-1/DocLib-1",
                    new WriterState
                        {Action = WriterAction.Creating, BrokenReferences = new[] {"F1"}, RetryPermissions = false}
                },
                {
                    "/Root/IMS/BuiltIn/Portal/Group-3",
                    new WriterState
                        {Action = WriterAction.Creating, BrokenReferences = new[] {"F1", "F3"}, RetryPermissions = true}
                }
            };

            // ACTION
            var reader = RepositoryReaderMock.Create(sourceTree, "/Root", null, 99999);
            var writer = new TestRepositoryWriter(targetTree, targetStates);
            var flow = new SemanticContentFlow(reader, writer, GetLogger<ContentFlow>());
            var progress = new TestProgress();
            await flow.TransferAsync(progress);

            // ASSERT
            var expected = sourceTree.Keys
                .OrderBy(x => x)
                .ToArray();
            var actual = targetTree.Keys
                .OrderBy(x => x)
                .ToArray();
            AssertSequencesAreEqual(expected, actual);

            expected = new[]
            {
                "InTree:'/Root/System/Schema/ContentTypes' .SORT:Path .TOP:99999 .SKIP:0 .AUTOFILTERS:OFF",
                "InTree:'/Root/System/Schema/ContentTypes' .SORT:Path .TOP:99999 .SKIP:99999 .AUTOFILTERS:OFF",
                "InTree:'/Root/System/Settings' .SORT:Path .TOP:99999 .SKIP:0 .AUTOFILTERS:OFF",
                "InTree:'/Root/System/Settings' .SORT:Path .TOP:99999 .SKIP:99999 .AUTOFILTERS:OFF",
                "InTree:'/Root/System/Schema/Aspects' .SORT:Path .TOP:99999 .SKIP:0 .AUTOFILTERS:OFF",
                "InTree:'/Root/System/Schema/Aspects' .SORT:Path .TOP:99999 .SKIP:99999 .AUTOFILTERS:OFF",
                "Path:('/Root/System/Schema/ContentTypes' '/Root/System/Settings' '/Root/System/Schema/Aspects') (+InTree:'/Root' -InTree:('/Root/System/Schema/ContentTypes' '/Root/System/Settings' '/Root/System/Schema/Aspects')) .SORT:Path .TOP:99999 .SKIP:0 .AUTOFILTERS:OFF",
                "Path:('/Root/System/Schema/ContentTypes' '/Root/System/Settings' '/Root/System/Schema/Aspects') (+InTree:'/Root' -InTree:('/Root/System/Schema/ContentTypes' '/Root/System/Settings' '/Root/System/Schema/Aspects')) .SORT:Path .TOP:99999 .SKIP:99999 .AUTOFILTERS:OFF",
                "/Root/System/Schema/ContentTypes/ContentType-1/ContentType-4",
                "/Root/System/Settings/Settings-2.settings",
                "/Root/Content/Workspace-1/DocLib-1",
                "/Root/IMS/BuiltIn/Portal/Group-3",
            };
            actual = reader.Queries.ToArray();
            AssertSequencesAreEqual(expected, actual);
        }

        /* =========================================================================================== FILTER TESTS */

        [TestMethod]
        public async Task RepoReader_Query_Preparation()
        {
            var sourceTree = CreateTree(new[] {"/Root"});

            // ACTION
            var filter = "+TypeIs:File .TOP:10 .SORT:Name .SORT:Index +Index:>1 .AUTOFILTERS:ON";
            var reader = RepositoryReaderMock.Create(sourceTree, "/Root/Content", filter, 5);

            await reader.InitializeAsync();

            // ASSERT
            Assert.AreEqual("+TypeIs:File +Index:>1", reader.Filter);
        }

        [TestMethod]
        public async Task RepoReader_Query()
        {
            var sourceTree = CreateTree(new []
            {
                "/Root",
                "/Root/Content",
                "/Root/Content/Docs",
                "/Root/Content/Docs/F1",
                "/Root/Content/Docs/F1/F2",
                "/Root/Content/Docs/F1/F2/File-1.txt",
                "/Root/Content/Docs/F1/F2/File-2.txt",
                "/Root/Content/Docs/F1/F2/File-3.txt",
                "/Root/Content/Docs/F1/F2/Folder-5",
                "/Root/Content/Docs/F1/F3",
                "/Root/Content/Docs/F1/F3/File-4.txt",
                "/Root/Content/Docs/F1/F3/File-5.txt",
                "/Root/Content/Docs/F1/F3/File-6.txt",
                "/Root/Content/Docs/F1/F3/Folder-6",
                "/Root/Content/Docs/F1/F4",
                "/Root/Content/Docs/F1/F4/File-7.txt",
                "/Root/Content/Docs/F1/F4/File-8.txt",
                "/Root/Content/Docs/F1/F4/File-9.txt",
                "/Root/Content/Docs/F1/F4/Folder7",
                "/Root/Content/Memos",
                "/Root/Content/Memos/Memo-1",
                "/Root/Content/Memos/Memo-2",
                "/Root/Content/Memos/Memo-3",
                "/Root/Content/Tasks",
                "/Root/Content/Tasks/Task-1",
                "/Root/Content/Tasks/Task-2",
                "/Root/Content/Tasks/Task-3",
            });
            var targetTree = CreateTree(new[]
            {
                "/Root",
                "/Root/Content",
                "/Root/Content/Docs",
                "/Root/Content/Docs/F1",
                "/Root/Content/Docs/F1/F2",
                "/Root/Content/Docs/F1/F3",
            });
            var targetStates = new Dictionary<string, WriterState>();

            // ACTION
            var filter = "+TypeIs:File +InTree:(/Root/Content/Docs/F1/F2 /Root/Content/Docs/F1/F3)";
            var reader = RepositoryReaderMock.Create(sourceTree, "/Root/Content", filter, 5);
            var writer = new TestRepositoryWriter(targetTree, targetStates, "/Root");
            var flow = new SemanticContentFlow(reader, writer, GetLogger<ContentFlow>());
            var progress = new TestProgress();
            await flow.TransferAsync(progress);

            // ASSERT
            var expected = new[]
            {
                "/Root",
                "/Root/Content",
                "/Root/Content/Docs",
                "/Root/Content/Docs/F1",
                "/Root/Content/Docs/F1/F2",
                "/Root/Content/Docs/F1/F2/File-1.txt",
                "/Root/Content/Docs/F1/F2/File-2.txt",
                "/Root/Content/Docs/F1/F2/File-3.txt",
                "/Root/Content/Docs/F1/F3",
                "/Root/Content/Docs/F1/F3/File-4.txt",
                "/Root/Content/Docs/F1/F3/File-5.txt",
                "/Root/Content/Docs/F1/F3/File-6.txt",
            };

            var actual = targetTree.Keys
                .OrderBy(x => x)
                .ToArray();
            AssertSequencesAreEqual(expected, actual);

            expected = new[]
            {
                "+InTree:'/Root/Content' +(+TypeIs:File +InTree:(/Root/Content/Docs/F1/F2 /Root/Content/Docs/F1/F3)) .SORT:Path .TOP:5 .SKIP:0 .AUTOFILTERS:OFF",
                "+InTree:'/Root/Content' +(+TypeIs:File +InTree:(/Root/Content/Docs/F1/F2 /Root/Content/Docs/F1/F3)) .SORT:Path .TOP:5 .SKIP:5 .AUTOFILTERS:OFF",
                "+InTree:'/Root/Content' +(+TypeIs:File +InTree:(/Root/Content/Docs/F1/F2 /Root/Content/Docs/F1/F3)) .SORT:Path .TOP:5 .SKIP:10 .AUTOFILTERS:OFF",
            };
            actual = reader.Queries.ToArray();
            AssertSequencesAreEqual(expected, actual);
        }

        /* =========================================================================================== TOOLS */

        private Dictionary<string, ContentNode> CreateSourceTree(string subtree)
        {
            var name = subtree.Substring(subtree.LastIndexOf("/", StringComparison.OrdinalIgnoreCase) + 1);
            var paths = new[]
                {
                    @"/Root",
                    @"/Root/(apps)",
                    @"/Root/Content",
                    @"/Root/Content/Workspace-1",
                    @"/Root/Content/Workspace-1/DocLib-1",
                    @"/Root/Content/Workspace-1/DocLib-1/Folder-1",
                    @"/Root/Content/Workspace-1/DocLib-1/Folder-1/File-1.xlsx",
                    @"/Root/Content/Workspace-1/DocLib-1/Folder-1/File-2.docx",
                    @"/Root/Content/Workspace-1/DocLib-1/Folder-2",
                    @"/Root/Content/Workspace-2",
                    @"/Root/IMS",
                    @"/Root/IMS/BuiltIn",
                    @"/Root/IMS/BuiltIn/Portal",
                    @"/Root/IMS/BuiltIn/Portal/User-3",
                    @"/Root/IMS/BuiltIn/Portal/Group-3",
                    @"/Root/IMS/Public",
                    @"/Root/IMS/Public/User-4",
                    @"/Root/IMS/Public/Group-4",
                    @"/Root/System",
                    @"/Root/System/Settings",
                    @"/Root/System/Settings/Settings-1.settings",
                    @"/Root/System/Settings/Settings-2.settings",
                    @"/Root/System/Settings/Settings-3.settings",
                    @"/Root/System/Schema",
                    @"/Root/System/Schema/Aspects",
                    @"/Root/System/Schema/Aspects/Aspect-1",
                    @"/Root/System/Schema/Aspects/Aspect-2",
                    @"/Root/System/Schema/ContentTypes",
                    @"/Root/System/Schema/ContentTypes/ContentType-1",
                    @"/Root/System/Schema/ContentTypes/ContentType-1/ContentType-3",
                    @"/Root/System/Schema/ContentTypes/ContentType-1/ContentType-4",
                    @"/Root/System/Schema/ContentTypes/ContentType-1/ContentType-5",
                    @"/Root/System/Schema/ContentTypes/ContentType-1/ContentType-5/ContentType-6",
                    @"/Root/System/Schema/ContentTypes/ContentType-2",
                }.Where(x => x.StartsWith(subtree)).ToArray();
            var paths1 = paths.Select(x => x.Substring(subtree.Length-1)).ToArray();
            var paths2 = paths1.Select(x => @"q:\io\" + name + x).ToArray();

            return CreateTree(paths1);
        }
        private Dictionary<string, ContentNode> CreateInitialTargetTree()
        {
            return CreateTree(new[]
            {
                "/Root",
                "/Root/IMS",
                "/Root/IMS/BuiltIn",
                "/Root/IMS/BuiltIn/Portal",
                "/Root/IMS/BuiltIn/Portal/User-1",
                "/Root/IMS/BuiltIn/Portal/User-2",
                "/Root/IMS/BuiltIn/Portal/Group-1",
                "/Root/IMS/BuiltIn/Portal/Group-2",
                "/Root/System",
                "/Root/System/Schema",
                "/Root/System/Schema/ContentTypes",
                "/Root/System/Settings",
            });
        }
        private class TestProgress : IProgress<TransferState>
        {
            public List<double> Log { get; } = new List<double>();
            public List<string> Paths { get; } = new List<string>();
            public void Report(TransferState value)
            {
                Log.Add(value.Percent);
                Paths.Add(value.State.WriterPath);
            }
        }

        private Dictionary<string, ContentNode> ReplacePaths(Dictionary<string, ContentNode> source, string from, string to)
        {
            var target = new Dictionary<string, ContentNode>();
            foreach (var item in source)
            {
                var path = item.Key.Replace(from, to);
                var content = item.Value;
                content.Path = path;
                target.Add(path, content);
            }

            return target;
        }

    }
}
