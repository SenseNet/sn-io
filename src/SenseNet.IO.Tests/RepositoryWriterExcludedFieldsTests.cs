using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SenseNet.Extensions.DependencyInjection;
using SenseNet.IO.CLI;
using SenseNet.IO.Implementations;
using SenseNet.IO.Tests.Implementations;

namespace SenseNet.IO.Tests
{
    [TestClass]
    [DoNotParallelize]
    public class RepositoryWriterExcludedFieldsTests
    {
        private const string Container = "/Root/Content";
        private const string RootName = "Új gyökér";
        private const string ActualRoot = Container + "/canonical-site";

        [DataTestMethod]
        [DataRow("default")]
        [DataRow("null")]
        [DataRow("empty")]
        [Timeout(10000)]
        public async Task ExcludedFields_Http_DefaultNullAndEmptyPreserveMetadataAndUploads(string mode)
        {
            await using var endpoint = new SyntheticSenseNetEndpoint();
            using var services = Services(endpoint, options =>
            {
                if (mode == "null") options.ExcludedFields = null;
                if (mode == "empty") options.ExcludedFields = Array.Empty<string>();
            });
            var content = ImageContent("Article");
            using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            var state = await services.GetRequiredService<ISynchronizeFlowFactory>().Create().Writer
                .WriteAsync("Article", content, cancel.Token);

            Assert.AreEqual(WriterAction.Created, state.Action, string.Join("; ", state.Messages));
            var fields = endpoint.Imports.Single().Data["Fields"];
            Assert.IsTrue(JToken.DeepEquals(JToken.FromObject(content["Image"]), fields["Image"]));
            Assert.AreEqual((string)content["ImageRef"], fields.Value<string>("ImageRef"));
            Assert.AreEqual((string)content["Url"], fields.Value<string>("Url"));
            Assert.AreEqual((string)content["Body"], fields.Value<string>("Body"));
            Assert.AreEqual("Article", fields.Value<string>("Name"));
            Assert.AreEqual("ImageData", endpoint.Uploads.Single().PropertyName);
            Assert.AreEqual("image bytes", Encoding.UTF8.GetString(endpoint.Uploads.Single().Bytes));
            Assert.AreEqual(0, endpoint.RejectedRequests.Length);
            Assert.AreEqual(0, endpoint.Errors.Length);
        }

        [TestMethod]
        [Timeout(20000)]
        public async Task ExcludedFields_Http_ImageOnlyPreservesReferenceRetryAndImageDataOnFreshSecondImport()
        {
            await using var endpoint = new SyntheticSenseNetEndpoint(new Dictionary<string, string>
            {
                [RootName] = "canonical-site", ["Kép ősz.jpg"] = "cover.jpg"
            }) { ReferenceFields = new[] { "ImageRef" }, RejectCompositeImageOnUpdate = true };
            using var package = new ImagePackage();
            var sourceBefore = File.ReadAllText(package.RootPath + ".Content");
            using var services = Services(endpoint, options =>
            {
                options.ExcludedFields = new[] { "iMaGe" };
                options.Name = RootName;
            }, package.RootPath);
            using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(12));
            var first = services.GetRequiredService<IImportFlowFactory>().Create();
            await first.TransferAsync(null, cancel.Token);
            var firstRequestCount = endpoint.Imports.Length;
            var second = services.GetRequiredService<IImportFlowFactory>().Create();
            await second.TransferAsync(null, cancel.Token);

            Assert.AreNotSame(first.Writer, second.Writer);
            Assert.AreEqual(3, firstRequestCount, "Root, child, then root ImageRef retry.");
            Assert.AreEqual(6, endpoint.Imports.Length);
            Assert.AreEqual(4, endpoint.Paths.Length, "Both imports must use the same canonical destinations.");
            Assert.IsTrue(endpoint.Imports.Skip(firstRequestCount).All(x => x.Action == "updated"));
            Assert.IsTrue(endpoint.Imports.All(x => x.Data["Fields"]["Image"] == null));
            var rootRequests = endpoint.Imports.Where(x => x.ActualPath == ActualRoot).ToArray();
            Assert.AreEqual(4, rootRequests.Length);
            foreach (var retry in new[] { rootRequests[1], rootRequests[3] })
            {
                Assert.AreEqual(ActualRoot, retry.Path);
                Assert.AreEqual("canonical-site", retry.Data.Value<string>("ContentName"));
                Assert.AreEqual(ActualRoot + "/cover.jpg", retry.Data["Fields"].Value<string>("ImageRef"));
            }
            Assert.AreEqual(2, endpoint.Uploads.Length);
            Assert.IsTrue(endpoint.Uploads.All(x => x.PropertyName == "ImageData" &&
                x.ParentPath == Container && x.ContentName == "canonical-site"));
            var stored = endpoint.GetNode(ActualRoot);
            Assert.AreEqual("https://example.invalid/image?path=" + RootName, stored["Fields"].Value<string>("Url"));
            Assert.AreEqual("Literal Image and " + Container + "/" + RootName, stored["Fields"].Value<string>("Body"));
            Assert.AreEqual(sourceBefore, File.ReadAllText(package.RootPath + ".Content"));
            Assert.AreEqual(0, endpoint.RejectedRequests.Length);
            Assert.AreEqual(0, endpoint.Errors.Length);
        }

        [TestMethod]
        [Timeout(10000)]
        public async Task ExcludedFields_Http_ExplicitBinaryPropertyIsNotUploadedAndItsStreamIsDisposed()
        {
            await using var endpoint = new SyntheticSenseNetEndpoint();
            using var services = Services(endpoint, options =>
                options.ExcludedFields = new[] { "IMAGEdata", "Name", "ContentName", "ContentType", "Permissions" });
            var content = ImageContent("Article");
            content.AddAttachment("Binary", "primary bytes");
            content.Permissions = new PermissionInfo { IsInherited = true, Entries = Array.Empty<AceInfo>() };
            using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            var state = await services.GetRequiredService<ISynchronizeFlowFactory>().Create().Writer
                .WriteAsync("Article", content, cancel.Token);

            Assert.AreEqual(WriterAction.Created, state.Action, string.Join("; ", state.Messages));
            Assert.IsTrue(content.OpenedStreams["ImageData"].WasDisposed);
            Assert.IsTrue(content.OpenedStreams["Binary"].WasDisposed);
            Assert.AreEqual("Binary", endpoint.Uploads.Single().PropertyName);
            var data = endpoint.Imports.Single().Data;
            Assert.IsNull(data["Fields"]["ImageData"]);
            Assert.IsNull(data["Fields"]["Name"]);
            Assert.AreEqual("Article", data.Value<string>("ContentName"));
            Assert.AreEqual("Article", data.Value<string>("ContentType"));
            Assert.IsTrue(data["Permissions"].Value<bool>("IsInherited"));
            Assert.IsNotNull(data["Fields"]["Image"], "Excluding ImageData alone must preserve Image.");
            Assert.IsNotNull(data["Fields"]["ImageRef"]);
            Assert.AreEqual(0, endpoint.RejectedRequests.Length);
            Assert.AreEqual(0, endpoint.Errors.Length);
        }

        [TestMethod]
        public void ExcludedFields_OptionsCloneFactoriesAndCliJsonBindingRemainIndependent()
        {
            var args = new RepositoryWriterArgs { ExcludedFields = new[] { "Image" } };
            var writer = new RepositoryWriter(null, Options.Create(args), NullLogger<RepositoryWriter>.Instance);
            args.ExcludedFields[0] = "Body";
            CollectionAssert.AreEqual(new[] { "Image" }, writer.Args.ExcludedFields);

            using var services = new ServiceCollection().AddLogging().AddSenseNetIO(
                configureRepositoryWriter: options => options.ExcludedFields = new[] { "Image" }).BuildServiceProvider();
            var import = services.GetRequiredService<IImportFlowFactory>().Create();
            var sync = services.GetRequiredService<ISynchronizeFlowFactory>().Create();
            ((RepositoryWriter)import.Writer).Args.ExcludedFields[0] = "Body";
            CollectionAssert.AreEqual(new[] { "Image" }, ((RepositoryWriter)sync.Writer).Args.ExcludedFields);
            CollectionAssert.AreEqual(new[] { "Image" }, ((RepositoryWriter)services.GetRequiredService<IImportFlowFactory>()
                .Create().Writer).Args.ExcludedFields);

            // Build the actual CLI host and inspect its bound writer; never initialize a
            // repository reader or execute a transfer in this configuration-only check.
            var createHost = typeof(Program).GetMethod("CreateHost", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(createHost);
            foreach (var verb in new[] { "IMPORT", "SYNC" })
            {
                foreach (var commandLineOverride in new[] { false, true })
                {
                    using var json = new MemoryStream(Encoding.UTF8.GetBytes("{" +
                        "\"repositoryReader\":{\"url\":\"http://127.0.0.1:1\",\"path\":\"/Root/Content\"}," +
                        "\"repositoryWriter\":{\"url\":\"http://127.0.0.1:1\",\"path\":\"/Root/Content\",\"excludedFields\":[\"Image\",\"ImageData\"]}," +
                        "\"fsReader\":{\"path\":\".\"}}"));
                    var commandLine = commandLineOverride
                        ? new[] { verb, "--repositoryWriter:ExcludedFields:0=Body" } : new[] { verb };
                    using var host = (IHost)createHost.Invoke(null, new object[] { commandLine, json });
                    var configured = (RepositoryWriter)host.Services.GetRequiredService<IContentWriter>();
                    CollectionAssert.AreEqual(new[] { commandLineOverride ? "Body" : "Image", "ImageData" },
                        configured.Args.ExcludedFields, verb);
                }
            }
        }

        [TestMethod]
        [Timeout(10000)]
        public async Task ExcludedFields_Http_ContentTypeFiltersMetadataAndOptionalProperty()
        {
            await using var endpoint = new SyntheticSenseNetEndpoint();
            const string parent = "/Root/System/Schema/ContentTypes";
            endpoint.Seed("/Root/System");
            endpoint.Seed("/Root/System/Schema");
            endpoint.Seed(parent);
            using var services = Services(endpoint, options => options.ExcludedFields = new[] { "Image", "Thumbnail" });
            var content = new FieldContent("ArticleType", "ContentType");
            content["Image"] = new JObject { ["Url"] = "unsupported composite" };
            content["Body"] = "ordinary metadata";
            content.AddAttachment("Binary", "<ContentType />");
            content.AddAttachment("Thumbnail", "thumbnail bytes");
            using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            var state = await services.GetRequiredService<ISnRepositoryWriter>()
                .WriteAsync(parent + "/ArticleType", content, cancel.Token);

            Assert.AreEqual(WriterAction.Updated, state.Action, string.Join("; ", state.Messages));
            CollectionAssert.AreEqual(new[] { "Upload:Binary", "Import" }, endpoint.Operations);
            Assert.IsNull(endpoint.Imports.Single().Data["Fields"]["Image"]);
            Assert.AreEqual("ordinary metadata", endpoint.Imports.Single().Data["Fields"].Value<string>("Body"));
            Assert.IsTrue(content.OpenedStreams["Thumbnail"].WasDisposed);
            Assert.AreEqual(0, endpoint.RejectedRequests.Length);
            Assert.AreEqual(0, endpoint.Errors.Length);
        }

        [TestMethod]
        [Timeout(10000)]
        public async Task ExcludedFields_Http_RejectsRequiredContentTypeBinaryBeforeAnyRequest()
        {
            await using var endpoint = new SyntheticSenseNetEndpoint();
            using var services = Services(endpoint, options => options.ExcludedFields = new[] { "bInArY" });
            var content = new FieldContent("ArticleType", "ContentType");
            content.AddAttachment("Binary", "<ContentType />");
            content.AddAttachment("Thumbnail", "thumbnail bytes");
            using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            var state = await services.GetRequiredService<ISnRepositoryWriter>()
                .WriteAsync("/Root/System/Schema/ContentTypes/ArticleType", content, cancel.Token);

            Assert.AreEqual(WriterAction.Failed, state.Action);
            var message = string.Join("; ", state.Messages);
            StringAssert.Contains(message, "Binary");
            StringAssert.Contains(message.ToLowerInvariant(), "exclud");
            Assert.AreEqual(0, endpoint.Operations.Length);
            Assert.AreEqual(0, endpoint.ExistenceChecks.Length);
            Assert.AreEqual(0, endpoint.RejectedRequests.Length);
            Assert.AreEqual(2, content.OpenedStreams.Count);
            Assert.IsTrue(content.OpenedStreams.Values.All(stream => stream.WasDisposed));
        }

        [TestMethod]
        [Timeout(10000)]
        public async Task ExcludedFields_SyntheticEndpointDemonstratesOptInCompositeImageUpdateRejection()
        {
            await using var endpoint = new SyntheticSenseNetEndpoint { RejectCompositeImageOnUpdate = true };
            using var services = Services(endpoint);
            var writer = services.GetRequiredService<ISnRepositoryWriter>();
            using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            var created = await writer.WriteAsync("Article", ImageContent("Article"), cancel.Token);
            var updated = await writer.WriteAsync("Article", ImageContent("Article"), cancel.Token);

            Assert.AreEqual(WriterAction.Created, created.Action);
            Assert.AreEqual(WriterAction.Failed, updated.Action);
            Assert.AreEqual(1, endpoint.Imports.Length);
            Assert.AreEqual(1, endpoint.RejectedRequests.Length);
            StringAssert.Contains(endpoint.RejectedRequests.Single(), "composite Image");
            Assert.AreEqual(0, endpoint.Errors.Length);
        }

        private static FieldContent ImageContent(string name)
        {
            var content = new FieldContent(name, "Article");
            content["Name"] = name;
            content["Image"] = new JObject { ["Url"] = "/images/cover.jpg", ["Width"] = 160, ["Height"] = 90 };
            content["ImageRef"] = Container + "/existing-ref";
            content["Url"] = "https://example.invalid/image?Image=preserve";
            content["Body"] = "Ordinary Image text and " + Container + "/Article";
            content.AddAttachment("ImageData", "image bytes");
            return content;
        }

        private static ServiceProvider Services(SyntheticSenseNetEndpoint endpoint,
            Action<RepositoryWriterArgs> configure = null, string sourcePath = null) => new ServiceCollection()
            .AddLogging().AddSenseNetIO(configureFilesystemReader: options => options.Path = sourcePath,
                configureRepositoryWriter: options =>
                {
                    options.Url = endpoint.Url;
                    options.Path = Container;
                    configure?.Invoke(options);
                })
            .ConfigureSenseNetRepository("target", options => options.Url = endpoint.Url).BuildServiceProvider();

        private sealed class TrackingStream : MemoryStream
        {
            public bool WasDisposed { get; private set; }
            public TrackingStream(string text) : base(Encoding.UTF8.GetBytes(text)) { }
            protected override void Dispose(bool disposing) { WasDisposed = true; base.Dispose(disposing); }
        }

        private sealed class FieldContent : IContent
        {
            private readonly Dictionary<string, object> _fields = new();
            private readonly Dictionary<string, string> _attachments = new();
            public Dictionary<string, TrackingStream> OpenedStreams { get; } = new();
            public FieldContent(string name, string type) { Name = name; Type = type; }
            public void AddAttachment(string field, string text)
            {
                _fields[field] = new JObject { ["Attachment"] = Name + "." + field };
                _attachments[field] = text;
            }
            public string[] FieldNames => _fields.Keys.ToArray();
            public object this[string fieldName] { get => _fields[fieldName]; set => _fields[fieldName] = value; }
            public string Name { get; set; }
            public string Path => Name;
            public bool CutOff { get; set; }
            public string Type { get; }
            public PermissionInfo Permissions { get; set; }
            public bool IsFolder => false;
            public bool HasData => true;
            public Task<Attachment[]> GetAttachmentsAsync(CancellationToken cancel) => Task.FromResult(
                _attachments.Select(pair =>
                {
                    var stream = new TrackingStream(pair.Value);
                    OpenedStreams[pair.Key] = stream;
                    return new Attachment { FieldName = pair.Key, FileName = Name + "." + pair.Key, Stream = stream };
                }).ToArray());
        }

        private sealed class ImagePackage : IDisposable
        {
            private readonly string _directory = Path.Combine(Path.GetTempPath(), "snio-exclusion-test-" + Guid.NewGuid().ToString("N"));
            public string RootPath { get; }
            public ImagePackage()
            {
                RootPath = Path.Combine(_directory, "Source root");
                Directory.CreateDirectory(RootPath);
                var rootFields = new JObject
                {
                    ["Image"] = new JObject { ["Url"] = "/images/cover.jpg", ["Width"] = 160 },
                    ["ImageRef"] = Container + "/" + RootName + "/Kép ősz.jpg",
                    ["ImageData"] = new JObject { ["Attachment"] = "root.ImageData" },
                    ["Url"] = "https://example.invalid/image?path=" + RootName,
                    ["Body"] = "Literal Image and " + Container + "/" + RootName
                };
                File.WriteAllText(RootPath + ".Content", JsonConvert.SerializeObject(new
                {
                    ContentType = "Folder", ContentName = "Source root", Fields = rootFields
                }), new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(_directory, "root.ImageData"), "image bytes", new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(RootPath, "Kép ősz.jpg.Content"), JsonConvert.SerializeObject(new
                {
                    ContentType = "Image", ContentName = "Kép ősz.jpg", Fields = new JObject()
                }), new UTF8Encoding(false));
            }
            public void Dispose()
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Directory.Delete(_directory, recursive: true);
            }
        }
    }
}
