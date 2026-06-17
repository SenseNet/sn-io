using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SenseNet.IO.CLI;
using SenseNet.IO.Implementations;

namespace SenseNet.IO.Tests
{
    [TestClass]
    public class LegacyRepositoryReaderTests
    {
        private class Response
        {
            public Func<HttpRequestMessage, HttpResponseMessage> Factory { get; set; }
        }

        private class TestHttpMessageHandler : HttpMessageHandler
        {
            private readonly Queue<Response> _responses;
            public List<HttpRequestMessage> Requests { get; } = new();
            public List<string> Bodies { get; } = new();

            public TestHttpMessageHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] responses)
            {
                _responses = new Queue<Response>(responses.Select(x => new Response { Factory = x }));
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                if (request.Content != null)
                    Bodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));

                if (_responses.Count == 0)
                    return new HttpResponseMessage(HttpStatusCode.NotFound);

                return _responses.Dequeue().Factory(request);
            }
        }

        private class LegacyRepositoryReaderMock : LegacyRepositoryReader
        {
            public LegacyRepositoryReaderMock(RepositoryReaderArgs args, HttpMessageHandler handler)
                : base(Options.Create(args), NullLogger<LegacyRepositoryReader>.Instance, handler)
            {
            }
        }

        [TestMethod]
        public void ArgParser_RepositoryReader_LegacyAuth()
        {
            var args = new[]
            {
                "EXPORT",
                "-SOURCE",
                "https://legacy.example.com",
                "/Root/Sites",
                "-AUTH",
                "legacy",
                "-USERNAME",
                "admin",
                "-PASSWORD",
                "pwd",
                "-DOMAIN",
                "BuiltIn",
                "-LOGINPATH",
                "/sn-login",
                "-EXPAND",
                "Owner,CreatedBy,ModifiedBy",
                "-EXCLUDEFIELDS",
                "CustomSystemField;AnotherRuntimeField",
                "-TARGET",
                @"c:\export"
            };

            var parsed = (ExportArguments)new ArgumentParser().Parse(args);

            Assert.AreEqual("https://legacy.example.com", parsed.ReaderArgs.Url);
            Assert.AreEqual("/Root/Sites", parsed.ReaderArgs.Path);
            Assert.AreEqual("legacy", parsed.ReaderArgs.Authentication.Type);
            Assert.AreEqual("admin", parsed.ReaderArgs.Authentication.Username);
            Assert.AreEqual("pwd", parsed.ReaderArgs.Authentication.Password);
            Assert.AreEqual("BuiltIn", parsed.ReaderArgs.Authentication.Domain);
            Assert.AreEqual("/sn-login", parsed.ReaderArgs.Authentication.LoginPath);
            Assert.AreEqual("Owner,CreatedBy,ModifiedBy", parsed.ReaderArgs.Expand);
            CollectionAssert.AreEqual(new[] { "CustomSystemField", "AnotherRuntimeField" },
                parsed.ReaderArgs.ExcludedFields);
            Assert.AreEqual(@"c:\export", parsed.WriterArgs.Path);
        }

        [TestMethod]
        public async Task LegacyRepositoryReader_ReadsExportFormatAndAttachments()
        {
            var handler = new TestHttpMessageHandler(
                request => new HttpResponseMessage(HttpStatusCode.OK),
                request => JsonResponse(@"{""d"":{""__count"":""1""}}"),
                request => JsonResponse(@"{
                    ""d"": {
                        ""results"": [
                            {
                                ""Type"": ""File"",
                                ""Name"": ""readme.txt"",
                                ""Path"": ""/Root/Sites/readme.txt"",
                                ""BrowseUrl"": ""/Root/Sites/readme.txt"",
                                ""DisplayName"": ""Read me"",
                                ""CustomSystemField"": ""runtime"",
                                ""Actions"": [],
                                ""Icon"": ""File"",
                                ""IsFile"": true,
                                ""Owner"": {
                                    ""Path"": ""/Root/IMS/BuiltIn/Portal/Admin""
                                },
                                ""Managers"": {
                                    ""results"": [
                                        {
                                            ""Path"": ""/Root/IMS/BuiltIn/Portal/Editors""
                                        },
                                        {
                                            ""Path"": ""/Root/IMS/BuiltIn/Portal/Operators""
                                        }
                                    ]
                                },
                                ""CreatedBy"": {
                                    ""__deferred"": {
                                        ""uri"": ""/OData.svc/Root('IMS')/CreatedBy""
                                    }
                                },
                                ""Binary"": {
                                    ""__mediaresource"": {
                                        ""media_src"": ""/binary/readme.txt"",
                                        ""content_type"": ""text/plain""
                                    }
                                }
                            }
                        ]
                    }
                }"),
                request => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("hello")
                });

            var reader = new LegacyRepositoryReaderMock(new RepositoryReaderArgs
            {
                Url = "https://legacy.example.com",
                Path = "/Root/Sites",
                BlockSize = 10,
                Expand = "Owner,Managers,CreatedBy",
                ExcludedFields = new[] { "CustomSystemField" },
                Authentication = new RepositoryAuthenticationOptions
                {
                    Type = "legacy",
                    Username = "admin",
                    Password = "pwd",
                    Domain = "BuiltIn"
                }
            }, handler);

            var hasContent = await reader.ReadAllAsync(Array.Empty<string>());

            Assert.IsTrue(hasContent);
            Assert.AreEqual(1, reader.EstimatedCount);
            Assert.AreEqual("readme.txt", reader.Content.Name);
            Assert.AreEqual("File", reader.Content.Type);
            Assert.AreEqual("readme.txt", reader.RelativePath);
            Assert.AreEqual("Read me", reader.Content["DisplayName"]);
            Assert.IsFalse(reader.Content.FieldNames.Contains("Actions"));
            Assert.IsFalse(reader.Content.FieldNames.Contains("Icon"));
            Assert.IsFalse(reader.Content.FieldNames.Contains("Type"));
            Assert.IsFalse(reader.Content.FieldNames.Contains("BrowseUrl"));
            Assert.IsFalse(reader.Content.FieldNames.Contains("IsFile"));
            Assert.IsFalse(reader.Content.FieldNames.Contains("CustomSystemField"));
            Assert.AreEqual("/Root/IMS/BuiltIn/Portal/Admin", reader.Content["Owner"]);
            CollectionAssert.AreEqual(new[]
            {
                "/Root/IMS/BuiltIn/Portal/Editors",
                "/Root/IMS/BuiltIn/Portal/Operators"
            }, (string[])reader.Content["Managers"]);
            Assert.IsNull(reader.Content["CreatedBy"]);

            var attachments = await reader.Content.GetAttachmentsAsync(CancellationToken.None);
            Assert.AreEqual(1, attachments.Length);
            Assert.AreEqual("Binary", attachments[0].FieldName);
            Assert.AreEqual("readme.txt", attachments[0].FileName);
            Assert.AreEqual("text/plain", attachments[0].ContentType);
            Assert.AreEqual("hello", ReadAllText(attachments[0].Stream));

            Assert.AreEqual(HttpMethod.Post, handler.Requests[0].Method);
            Assert.AreEqual("https://legacy.example.com/sn-login", handler.Requests[0].RequestUri.ToString());
            Assert.IsTrue(handler.Bodies[0].Contains("UserName=admin"));
            Assert.IsTrue(handler.Bodies[0].Contains("Password=pwd"));
            Assert.IsTrue(handler.Bodies[0].Contains("Domain=BuiltIn"));

            var queryUrl = Uri.UnescapeDataString(handler.Requests[2].RequestUri.ToString());
            Assert.IsTrue(queryUrl.Contains("/OData.svc/Root?"));
            Assert.IsTrue(queryUrl.Contains("query=+InTree:'/Root/Sites'"));
            Assert.IsFalse(Uri.UnescapeDataString(handler.Requests[1].RequestUri.ToString()).Contains("$expand"));
            Assert.IsTrue(queryUrl.Contains("$expand=Owner,Managers,CreatedBy"));
            Assert.IsTrue(queryUrl.Contains(".SORT:Path"));
            Assert.IsTrue(queryUrl.Contains(".TOP:10"));
            Assert.IsFalse(queryUrl.Contains("$format"));
            Assert.IsFalse(queryUrl.Contains("$orderby=Path"));
            Assert.AreEqual("https://legacy.example.com/binary/readme.txt", handler.Requests[3].RequestUri.ToString());
        }

        [TestMethod]
        public void LegacyRepositoryContent_UsesLegacyContentTypeNames()
        {
            var content = new LegacyRepositoryContent(NewContent(@"
                {
                    ""ContentTypeName"": ""PopupContainer"",
                    ""Name"": ""popups"",
                    ""Path"": ""/Root/Sites/demo/popups""
                }"), EmptyStreamLoader);

            Assert.AreEqual("PopupContainer", content.Type);
            Assert.AreEqual("popups", content.Name);
            Assert.IsFalse(content.FieldNames.Contains("ContentTypeName"));
        }

        [TestMethod]
        public void LegacyRepositoryContent_UsesNodeTypeName()
        {
            var content = new LegacyRepositoryContent(NewContent(@"
                {
                    ""NodeType"": {
                        ""Name"": ""Page""
                    },
                    ""Name"": ""login"",
                    ""Path"": ""/Root/Sites/demo/login""
                }"), EmptyStreamLoader);

            Assert.AreEqual("Page", content.Type);
        }

        [TestMethod]
        public void LegacyRepositoryContent_InfersBuiltInVisitorUserType()
        {
            var content = new LegacyRepositoryContent(NewContent(@"
                {
                    ""Name"": ""Visitor"",
                    ""Id"": 6,
                    ""Path"": ""/Root/IMS/BuiltIn/Portal/Visitor"",
                    ""IsFile"": false
                }"), EmptyStreamLoader);

            Assert.AreEqual("User", content.Type);
        }

        private static HttpResponseMessage JsonResponse(string json)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json)
            };
        }

        private static string ReadAllText(Stream stream)
        {
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        private static Newtonsoft.Json.Linq.JObject NewContent(string json)
        {
            return Newtonsoft.Json.Linq.JObject.Parse(json);
        }

        private static Task<Stream> EmptyStreamLoader(string url, CancellationToken cancel)
        {
            return Task.FromResult<Stream>(new MemoryStream());
        }
    }
}
