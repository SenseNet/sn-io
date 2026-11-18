using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SenseNet.Extensions.DependencyInjection;
using SenseNet.IO.Implementations;
using SenseNet.IO.Tests.Implementations;

namespace SenseNet.IO.Tests
{
    [TestClass]
    [DoNotParallelize]
    public class RepositoryWriterHttpTests
    {
        private const string Container = "/Root/Content";
        private const string TargetName = "Új gyökér";
        private const string PlannedRoot = Container + "/" + TargetName;
        private const string ActualRoot = Container + "/canonical-site";
        private const string ActualFile = ActualRoot + "/news/2026-fall/cover.jpg";

        [TestMethod]
        [Timeout(30000)]
        public async Task Import_Http_FollowsReturnedPathsForDescendantsUploadsRetryAndFreshSecondFlow()
        {
            await using var endpoint = new SyntheticSenseNetEndpoint(new Dictionary<string, string>
            {
                [TargetName] = "canonical-site",
                ["Hírek"] = "news",
                ["Év ősz"] = "2026-fall",
                ["Címlap kép.jpg"] = "cover.jpg",
                ["Őrzők"] = "guardians"
            }) { ReferenceFields = new[] { "RelatedContent" } };
            using var package = CreatePackage();
            var rootMetadataBefore = File.ReadAllText(package.RootPath + ".Content");
            using var services = CreateServices(endpoint, package.RootPath, TargetName);
            using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var firstProgress = new ImmediateProgress();

            // Real FsReader -> real import flow -> RepositoryWriter -> SenseNet.Client 4.1.0
            // -> real loopback HTTP. Only the server is a synthetic contract fixture.
            var firstFlow = services.GetRequiredService<IImportFlowFactory>().Create();
            Assert.IsInstanceOfType<RepositoryWriter>(firstFlow.Writer);
            Assert.IsInstanceOfType<FsReader>(firstFlow.Reader);
            await firstFlow.TransferAsync(firstProgress, cancel.Token);

            CollectionAssert.AreEquivalent(new[]
            {
                "/Root", Container, ActualRoot, ActualRoot + "/news", ActualRoot + "/news/2026-fall",
                ActualFile, ActualRoot + "/guardians"
            }, endpoint.Paths);
            Assert.AreEqual(6, endpoint.Imports.Length, "Five nodes and one reference/permission retry.");
            Assert.AreEqual(0, firstProgress.States.Max(x => x.ErrorCount));
            CollectionAssert.AreEquivalent(new[]
            {
                ActualRoot, ActualRoot, ActualRoot + "/news", ActualRoot + "/news/2026-fall",
                ActualFile, ActualRoot + "/guardians"
            }, firstProgress.States.Select(x => x.State.WriterPath).ToArray());
            Assert.IsTrue(firstProgress.States.Last().UpdatingReferences);
            Assert.AreEqual(WriterAction.Updated, firstProgress.States.Last().State.Action);

            var rootRequests = endpoint.Imports.Where(x => x.ActualPath == ActualRoot).ToArray();
            Assert.AreEqual(PlannedRoot, rootRequests[0].Path);
            Assert.AreEqual(TargetName, rootRequests[0].Data.Value<string>("ContentName"));
            Assert.AreEqual(ActualRoot, rootRequests[1].Path);
            Assert.AreEqual("canonical-site", rootRequests[1].Data.Value<string>("ContentName"));
            var retryName = rootRequests[1].Data["Fields"].Value<string>("Name");
            Assert.IsTrue(retryName == null || retryName == "canonical-site",
                "A filtered retry may omit Name; an included Name must be canonical.");
            Assert.AreEqual(ActualFile, rootRequests[1].Data["Fields"]["RelatedContent"][0].Value<string>());
            Assert.AreEqual(ActualRoot + "/guardians",
                rootRequests[1].Data["Permissions"]["Entries"][0].Value<string>("Identity"));
            Assert.AreEqual("InTree:'" + PlannedRoot + "'",
                rootRequests[0].Data["Fields"].Value<string>("Query"));
            Assert.AreEqual("Keep this literal: " + PlannedRoot,
                rootRequests[0].Data["Fields"].Value<string>("Body"));
            Assert.IsNull(rootRequests[1].Data["Fields"]["Query"], "FsReader retry must omit ordinary fields.");
            Assert.IsNull(rootRequests[1].Data["Fields"]["Body"]);
            Assert.AreEqual("InTree:'" + PlannedRoot + "'", endpoint.GetNode(ActualRoot)["Fields"].Value<string>("Query"));
            Assert.AreEqual("Keep this literal: " + PlannedRoot, endpoint.GetNode(ActualRoot)["Fields"].Value<string>("Body"));

            var firstUploads = endpoint.Uploads;
            Assert.AreEqual(2, firstUploads.Length);
            CollectionAssert.AreEquivalent(new[] { "Binary", "ImageData" },
                firstUploads.Select(x => x.PropertyName).ToArray());
            foreach (var upload in firstUploads)
            {
                Assert.AreEqual(ActualRoot + "/news/2026-fall", upload.ParentPath);
                Assert.AreEqual("cover.jpg", upload.ContentName);
                Assert.AreEqual(upload.PropertyName == "Binary" ? "primary bytes" : "property bytes",
                    Encoding.UTF8.GetString(upload.Bytes));
            }

            var firstImportCount = endpoint.Imports.Length;
            var secondProgress = new ImmediateProgress();
            var secondFlow = services.GetRequiredService<IImportFlowFactory>().Create();
            Assert.AreNotSame(firstFlow.Writer, secondFlow.Writer, "The second run must relearn aliases.");
            await secondFlow.TransferAsync(secondProgress, cancel.Token);

            Assert.AreEqual(7, endpoint.Paths.Length, "A fresh flow must not create duplicate planned-name branches.");
            Assert.AreEqual(6, endpoint.Imports.Length - firstImportCount);
            Assert.IsTrue(endpoint.Imports.Skip(firstImportCount).All(x => x.Action == "updated"));
            Assert.AreEqual(0, secondProgress.States.Max(x => x.ErrorCount));
            Assert.AreEqual(4, endpoint.Uploads.Length);
            Assert.AreEqual(rootMetadataBefore, File.ReadAllText(package.RootPath + ".Content"),
                "Root rename and retry remapping must not alter package metadata.");
            Assert.AreEqual(0, endpoint.RejectedRequests.Length);
            Assert.AreEqual(0, endpoint.Errors.Length, string.Join("; ", endpoint.Errors.Select(x => x.Message)));
        }

        [TestMethod]
        [Timeout(30000)]
        public async Task Import_Http_CreateOnlyCompletesDeferredFieldsOnNodesCreatedBySameWriter()
        {
            await using var endpoint = new SyntheticSenseNetEndpoint(new Dictionary<string, string>
            {
                [TargetName] = "canonical-site", ["Hírek"] = "news", ["Év ősz"] = "2026-fall",
                ["Címlap kép.jpg"] = "cover.jpg", ["Őrzők"] = "guardians"
            }) { ReferenceFields = new[] { "RelatedContent" } };
            using var package = CreatePackage();
            using var services = CreateServices(endpoint, package.RootPath, TargetName, createOnly: true);
            using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var progress = new ImmediateProgress();

            await services.GetRequiredService<IImportFlowFactory>().Create().TransferAsync(progress, cancel.Token);

            Assert.AreEqual(6, endpoint.Imports.Length, "CreateOnly must allow this writer's deferred root update.");
            Assert.AreEqual(7, endpoint.Paths.Length);
            Assert.AreEqual(0, progress.States.Max(x => x.ErrorCount));
            Assert.AreEqual(ActualRoot, progress.States.Last().State.WriterPath);
            Assert.AreEqual(WriterAction.Updated, progress.States.Last().State.Action);
            Assert.IsTrue(progress.States.Last().UpdatingReferences);
            Assert.AreEqual(ActualFile, endpoint.GetNode(ActualRoot)["Fields"]["RelatedContent"][0].Value<string>());
            Assert.AreEqual(0, endpoint.RejectedRequests.Length);
            Assert.AreEqual(0, endpoint.Errors.Length, string.Join("; ", endpoint.Errors.Select(x => x.Message)));
        }

        [TestMethod]
        [Timeout(20000)]
        public async Task Writer_Http_CreateOnlyUsesLearnedAliasAndExistingCanonicalPath()
        {
            await using var endpoint = new SyntheticSenseNetEndpoint(new Dictionary<string, string>
            {
                ["Hírek"] = "news"
            });
            using var services = CreateServices(endpoint, null, null, createOnly: true);
            var writer = services.GetRequiredService<ISnRepositoryWriter>();
            using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var content = new TestImportContent("Hírek");

            var created = await writer.WriteAsync("Hírek", content, cancel.Token);
            var skipped = await writer.WriteAsync("Hírek", content, cancel.Token);

            Assert.AreEqual(WriterAction.Created, created.Action);
            Assert.AreEqual(Container + "/news", created.WriterPath);
            Assert.AreEqual(WriterAction.Skipped, skipped.Action);
            Assert.AreEqual(Container + "/news", skipped.WriterPath);
            Assert.AreEqual(1, endpoint.Imports.Length);
            CollectionAssert.Contains(endpoint.ExistenceChecks, Container + "/news");

            // A new writer can safely skip an explicitly canonical path. Discovering an
            // unknown normalized alias before any server write is outside this fixture.
            var freshWriter = services.GetRequiredService<ISnRepositoryWriter>();
            var existing = await freshWriter.WriteAsync("news", new TestImportContent("news"), cancel.Token);
            Assert.AreEqual(WriterAction.Skipped, existing.Action);
            Assert.AreEqual(Container + "/news", existing.WriterPath);
            Assert.AreEqual(1, endpoint.Imports.Length);
            Assert.AreEqual(0, endpoint.RejectedRequests.Length);
            Assert.AreEqual(0, endpoint.Errors.Length, string.Join("; ", endpoint.Errors.Select(x => x.Message)));
        }

        [TestMethod]
        [Timeout(20000)]
        public async Task Writer_Http_ResponseNameFallbackTargetsAttachmentAtReturnedName()
        {
            await using var endpoint = new SyntheticSenseNetEndpoint(new Dictionary<string, string>
            {
                ["Kép ősz.jpg"] = "unpredicted-server-name.jpg"
            }) { IncludeResponsePath = false };
            using var services = CreateServices(endpoint, null, null);
            using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var writer = services.GetRequiredService<ISnRepositoryWriter>();
            var state = await writer.WriteAsync("Kép ősz.jpg",
                new TestImportContent("Kép ősz.jpg", withAttachment: true), cancel.Token);

            Assert.AreEqual(WriterAction.Created, state.Action,
                string.Join("; ", state.Messages.Concat(endpoint.RejectedRequests).Concat(endpoint.Errors.Select(x => x.Message))));
            Assert.AreEqual(Container + "/unpredicted-server-name.jpg", state.WriterPath);
            Assert.AreEqual(1, endpoint.Uploads.Length);
            Assert.AreEqual(Container, endpoint.Uploads[0].ParentPath);
            Assert.AreEqual("unpredicted-server-name.jpg", endpoint.Uploads[0].ContentName);
            Assert.AreEqual(0, endpoint.RejectedRequests.Length);
            Assert.AreEqual(0, endpoint.Errors.Length, string.Join("; ", endpoint.Errors.Select(x => x.Message)));
        }

        [TestMethod]
        [Timeout(20000)]
        public async Task Writer_Http_AttachmentFailureRetainsReturnedCanonicalPath()
        {
            await using var endpoint = new SyntheticSenseNetEndpoint(new Dictionary<string, string>
            {
                ["Kép ősz.jpg"] = "cover.jpg"
            }) { RejectUploadProperty = "Binary" };
            using var services = CreateServices(endpoint, null, null);
            using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var writer = services.GetRequiredService<ISnRepositoryWriter>();
            var state = await writer.WriteAsync("Kép ősz.jpg",
                new TestImportContent("Kép ősz.jpg", withAttachment: true), cancel.Token);

            Assert.AreEqual(WriterAction.Failed, state.Action);
            Assert.AreEqual(Container + "/cover.jpg", state.WriterPath,
                string.Join("; ", state.Messages.Concat(endpoint.RejectedRequests).Concat(endpoint.Errors.Select(x => x.Message))));
            Assert.AreEqual(1, endpoint.Imports.Length);
            Assert.AreEqual(1, endpoint.RejectedRequests.Length);
            Assert.AreEqual(0, endpoint.Uploads.Length);
            Assert.AreEqual(0, endpoint.Errors.Length, string.Join("; ", endpoint.Errors.Select(x => x.Message)));

            Assert.IsFalse(await writer.ShouldSkipSubtreeAsync(state.WriterPath, cancel.Token));
            endpoint.RejectUploadProperty = null;
            var retried = await writer.WriteAsync("Kép ősz.jpg",
                new TestImportContent("Kép ősz.jpg", withAttachment: true), cancel.Token);
            Assert.AreEqual(WriterAction.Updated, retried.Action);
            Assert.AreEqual(Container + "/cover.jpg", retried.WriterPath);
            Assert.AreEqual(Container + "/cover.jpg", endpoint.Imports.Last().Path);
            Assert.AreEqual("cover.jpg", endpoint.Imports.Last().Data.Value<string>("ContentName"));
            Assert.AreEqual(1, endpoint.Uploads.Length);
        }

        [TestMethod]
        [Timeout(10000)]
        public async Task Writer_Http_ContentTypeUsesPrimaryUploadReturnedPathForOtherBinariesAndImport()
        {
            const string contentTypeContainer = "/Root/System/Schema/ContentTypes";
            const string sourceName = "Új cikk";
            const string actualName = "server-article";
            var actualPath = contentTypeContainer + "/" + actualName;
            await using var endpoint = new SyntheticSenseNetEndpoint(new Dictionary<string, string>
            {
                [sourceName] = actualName
            });
            endpoint.Seed("/Root/System");
            endpoint.Seed("/Root/System/Schema");
            endpoint.Seed(contentTypeContainer);
            using var services = CreateServices(endpoint, null, null);
            using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var content = new TestImportContent(sourceName, withAttachment: true,
                contentType: "ContentType", secondaryProperty: "Thumbnail");

            var state = await services.GetRequiredService<ISnRepositoryWriter>()
                .WriteAsync(contentTypeContainer + "/" + sourceName, content, cancel.Token);

            Assert.AreEqual(WriterAction.Updated, state.Action, string.Join("; ", state.Messages));
            Assert.AreEqual(actualPath, state.WriterPath);
            CollectionAssert.AreEqual(new[] { "Upload:Binary", "Upload:Thumbnail", "Import" }, endpoint.Operations);
            Assert.AreEqual(2, endpoint.Uploads.Length);
            Assert.AreEqual(contentTypeContainer, endpoint.Uploads[0].ParentPath);
            Assert.AreEqual(sourceName, endpoint.Uploads[0].ContentName);
            Assert.AreEqual("ContentType", endpoint.Uploads[0].ContentType);
            Assert.AreEqual(contentTypeContainer, endpoint.Uploads[1].ParentPath);
            Assert.AreEqual(actualName, endpoint.Uploads[1].ContentName);
            Assert.AreEqual("Thumbnail", endpoint.Uploads[1].PropertyName);
            Assert.AreEqual(1, endpoint.Imports.Length);
            Assert.AreEqual(actualPath, endpoint.Imports[0].Path);
            Assert.AreEqual(actualName, endpoint.Imports[0].Data.Value<string>("ContentName"));
            Assert.AreEqual(actualName, endpoint.Imports[0].Data["Fields"].Value<string>("Name"));
            Assert.AreEqual(sourceName, content.Name, "Following server identity must not rename source metadata.");
            CollectionAssert.DoesNotContain(endpoint.Paths, contentTypeContainer + "/" + sourceName);
            Assert.AreEqual(0, endpoint.RejectedRequests.Length);
            Assert.AreEqual(0, endpoint.Errors.Length, string.Join("; ", endpoint.Errors.Select(x => x.Message)));
        }

        [TestMethod]
        [Timeout(10000)]
        public async Task SyntheticEndpoint_HttpRejectsUnmappedParent()
        {
            await using var endpoint = new SyntheticSenseNetEndpoint();
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var request = new
            {
                path = Container + "/Hírek/child",
                data = new { ContentName = "child", ContentType = "Folder", Fields = new { } }
            };
            using var response = await client.PostAsync(endpoint.Url + "/OData.svc/('Root')/Import",
                new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json"));

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.AreEqual(1, endpoint.RejectedRequests.Length);
            Assert.AreEqual(0, endpoint.Imports.Length);
            Assert.AreEqual(2, endpoint.Paths.Length);
            Assert.AreEqual(0, endpoint.Errors.Length, string.Join("; ", endpoint.Errors.Select(x => x.Message)));
        }

        private static ServiceProvider CreateServices(SyntheticSenseNetEndpoint endpoint, string sourcePath,
            string rootName, bool createOnly = false) => new ServiceCollection()
            .AddLogging()
            .AddSenseNetIO(configureFilesystemReader: options => options.Path = sourcePath,
                configureRepositoryWriter: options =>
                {
                    options.Url = endpoint.Url;
                    options.Path = Container;
                    options.Name = rootName;
                    options.CreateOnly = createOnly;
                })
            .ConfigureSenseNetRepository("target", options => options.Url = endpoint.Url)
            .BuildServiceProvider();

        private sealed class ImmediateProgress : IProgress<TransferState>
        {
            public List<TransferState> States { get; } = new();
            public void Report(TransferState value) => States.Add(value);
        }

        private sealed class TestImportContent : IContent
        {
            private readonly bool _withAttachment;
            private readonly string _type;
            private readonly string _secondaryProperty;
            private readonly Dictionary<string, object> _fields = new();
            public TestImportContent(string name, bool withAttachment = false,
                string contentType = null, string secondaryProperty = null)
            {
                Name = name;
                _withAttachment = withAttachment;
                _type = contentType;
                _secondaryProperty = secondaryProperty;
                _fields["Name"] = name;
            }
            public string[] FieldNames => _fields.Keys.ToArray();
            public object this[string fieldName] { get => _fields[fieldName]; set => _fields[fieldName] = value; }
            public string Name { get; set; }
            public string Path => Name;
            public bool CutOff { get; set; }
            public string Type => _type ?? (_withAttachment ? "File" : "Folder");
            public PermissionInfo Permissions { get; set; }
            public bool IsFolder => !_withAttachment;
            public bool HasData => true;
            public Task<Attachment[]> GetAttachmentsAsync(CancellationToken cancel)
            {
                var attachments = new List<Attachment>();
                if (_withAttachment)
                    attachments.Add(new Attachment
                {
                    FieldName = "Binary", FileName = Name,
                    Stream = new MemoryStream(Encoding.UTF8.GetBytes("attachment bytes"))
                });
                if (_secondaryProperty != null)
                    attachments.Add(new Attachment
                    {
                        FieldName = _secondaryProperty, FileName = Name + "." + _secondaryProperty,
                        Stream = new MemoryStream(Encoding.UTF8.GetBytes("property bytes"))
                    });
                return Task.FromResult(attachments.ToArray());
            }
        }

        private static ImportPackage CreatePackage()
        {
            var package = new ImportPackage("Forrás gyökér");
            package.Add("", "Folder", new JObject
            {
                ["Name"] = "Forrás gyökér",
                ["RelatedContent"] = new JArray(PlannedRoot + "/Hírek/Év ősz/Címlap kép.jpg"),
                ["Query"] = "InTree:'" + PlannedRoot + "'",
                ["Body"] = "Keep this literal: " + PlannedRoot
            }, new PermissionInfo
            {
                IsInherited = false,
                Entries = new[] { new AceInfo
                {
                    Identity = PlannedRoot + "/Őrzők", LocalOnly = true,
                    Permissions = new Dictionary<string, string> { ["See"] = "allow" }
                } }
            });
            package.Add("Hírek", "Folder");
            package.Add("Hírek/Év ősz", "Folder");
            package.Add("Őrzők", "Group");
            package.Add("Hírek/Év ősz/Címlap kép.jpg", "File", new JObject
            {
                ["Binary"] = new JObject { ["Attachment"] = "Címlap kép.jpg" },
                ["ImageData"] = new JObject { ["Attachment"] = "Címlap kép.jpg.ImageData" }
            });
            File.WriteAllText(Path.Combine(package.RootPath, "Hírek", "Év ősz", "Címlap kép.jpg"),
                "primary bytes", new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(package.RootPath, "Hírek", "Év ősz", "Címlap kép.jpg.ImageData"),
                "property bytes", new UTF8Encoding(false));
            return package;
        }

        private sealed class ImportPackage : IDisposable
        {
            private readonly string _directory = Path.Combine(Path.GetTempPath(), "snio-http-test-" + Guid.NewGuid().ToString("N"));
            public string RootPath { get; }
            public ImportPackage(string rootName)
            {
                RootPath = Path.Combine(_directory, rootName);
                Directory.CreateDirectory(RootPath);
            }
            public void Add(string relativePath, string type, JObject fields = null, PermissionInfo permissions = null)
            {
                var path = relativePath.Length == 0 ? RootPath : Path.Combine(RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(type == "File" ? Path.GetDirectoryName(path) : path);
                File.WriteAllText(path + ".Content", JsonConvert.SerializeObject(new
                {
                    ContentName = Path.GetFileName(path), ContentType = type,
                    Fields = fields ?? new JObject(), Permissions = permissions
                }, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }), new UTF8Encoding(false));
            }
            public void Dispose()
            {
                // FsContent's existing metadata readers are finalizable but not disposed.
                // Collect them before removing this real-filesystem package on Windows.
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Directory.Delete(_directory, recursive: true);
            }
        }
    }
}
