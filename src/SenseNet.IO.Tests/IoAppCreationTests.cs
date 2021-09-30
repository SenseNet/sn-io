using System;
using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SenseNet.IO.CLI;
using SenseNet.IO.Implementations;

namespace SenseNet.IO.Tests
{
    [TestClass]
    public class IoAppCreationTests
    {
        private string DefaultSettings = @"{
  ""repositoryReader"": { ""url"": ""https://localhost"", ""path"": ""/Root/Content"", ""blockSize"": 10 },
  ""repositoryWriter"": { ""url"": ""https://localhost"", ""path"": ""/Root"", ""name"": ""Content"" },
  ""fsReader"": { ""path"": ""Q:\\_sn-io-test\\content""  },
  ""fsWriter"": { ""path"": ""Q:\\_sn-io-test"",  }}";

        [TestMethod]
        public void App_Import_DefaultSource_DefaultTarget()
        {
            Test(new[] { "IMPORT" },
                DefaultSettings,
                typeof(FsReader), "Path: Q:\\_sn-io-test\\content",
                typeof(RepositoryWriter), "Url: https://localhost, Path: /Root, Name: Content");
        }
        [TestMethod]
        public void App_Import_OverriddenSource_DefaultTarget()
        {
            Test(new[] { "IMPORT", "-SOURCE", @"Q:\_sn-io-test\content1" },
                DefaultSettings,
                typeof(FsReader), "Path: Q:\\_sn-io-test\\content1",
                typeof(RepositoryWriter), "Url: https://localhost, Path: /Root, Name: Content");
        }
        [TestMethod]
        public void App_Import_DefaultSource_OverriddenTarget_1()
        {
            Test(new[] { "IMPORT", "-TARGET", "https://localhost:4242" },
                DefaultSettings,
                typeof(FsReader), "Path: Q:\\_sn-io-test\\content",
                typeof(RepositoryWriter), "Url: https://localhost:4242, Path: /Root, Name: Content");
        }
        [TestMethod]
        public void App_Import_DefaultSource_OverriddenTarget_2()
        {
            Test(new[] { "IMPORT", "-TARGET", "https://localhost:4242", "\"/Root/Backup\"" },
                DefaultSettings,
                typeof(FsReader), "Path: Q:\\_sn-io-test\\content",
                typeof(RepositoryWriter), "Url: https://localhost:4242, Path: /Root/Backup, Name: Content");
        }
        [TestMethod]
        public void App_Import_DefaultSource_OverriddenTarget_3()
        {
            Test(new[] { "IMPORT", "-TARGET", "https://localhost:4242", "\"/Root/Backup\"", "Content2" },
                DefaultSettings,
                typeof(FsReader), "Path: Q:\\_sn-io-test\\content",
                typeof(RepositoryWriter), "Url: https://localhost:4242, Path: /Root/Backup, Name: Content2");
        }
        [TestMethod]
        public void App_Import_DefaultSource_OverriddenTarget_4()
        {
            Test(new[] { "IMPORT", "-TARGET", "https://localhost:4242", "-NAME", "Content2" },
                DefaultSettings,
                typeof(FsReader), "Path: Q:\\_sn-io-test\\content",
                typeof(RepositoryWriter), "Url: https://localhost:4242, Path: /Root, Name: Content2");
        }
        [TestMethod]
        public void App_Import_DefaultSource_OverriddenTarget_5()
        {
            Test(new[] { "IMPORT", "-TARGET", "-NAME", "Content2" },
                DefaultSettings,
                typeof(FsReader), "Path: Q:\\_sn-io-test\\content",
                typeof(RepositoryWriter), "Url: https://localhost, Path: /Root, Name: Content2");
        }
        [TestMethod]
        public void App_Import_OverriddenSource_OverriddenTarget()
        {
            Test(new[] { "IMPORT", "-SOURCE", @"Q:\_sn-io-test\content1", "-TARGET", "https://localhost:4242", "\"/Root/Backup\"", "Content2" },
                DefaultSettings,
                typeof(FsReader), "Path: Q:\\_sn-io-test\\content1",
                typeof(RepositoryWriter), "Url: https://localhost:4242, Path: /Root/Backup, Name: Content2");
        }
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void App_Import_MissingSource()
        {
            Test(new[] { "IMPORT", "-TARGET", "https://localhost1" },
                @"{
  ""repositoryReader"": { ""url"": ""https://localhost"", ""path"": ""/Root/Content"", ""blockSize"": 10 },
  ""repositoryWriter"": { ""url"": ""https://localhost"", ""path"": ""/Root"", ""name"": ""Content"" },
  ""fsReader"": { },
  ""fsWriter"": { ""path"": ""Q:\\_sn-io-test"", } }",
                typeof(FsReader), "Path: ",
                typeof(RepositoryWriter), "Url: https://localhost1, Path: /Root, Name: Content");
        }
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void App_Import_MissingTarget()
        {
            Test(new[] { "IMPORT", "-SOURCE", "Q:\\_sn-io-test\\content1" },
                @"{
  ""repositoryReader"": { ""url"": ""https://localhost"", ""path"": ""/Root/Content"", ""blockSize"": 10 },
  ""repositoryWriter"": { },
  ""fsReader"": { ""path"": ""Q:\\_sn-io-test\\content"" },
  ""fsWriter"": { ""path"": ""Q:\\_sn-io-test"", } }",
                typeof(FsReader), "Path: Q:\\_sn-io-test\\content1",
                typeof(RepositoryWriter), "Url: , Path: /");
        }

        [TestMethod]
        public void App_Export_DefaultSource_DefaultTarget()
        {
            Test(new[] { "EXPORT" },
                DefaultSettings,
                typeof(RepositoryReader), "Url: https://localhost, Path: /Root/Content, BlockSize: 10",
                typeof(FsWriter), "Path: Q:\\_sn-io-test");
        }
        [TestMethod]
        public void App_Export_DefaultSource_OverriddenTarget_1()
        {
            Test(new[] { "EXPORT", "-TARGET", "-FLATTEN" },
                DefaultSettings,
                typeof(RepositoryReader), "Url: https://localhost, Path: /Root/Content, BlockSize: 10",
                typeof(FsWriter), "Path: Q:\\_sn-io-test, Flatten");
        }
        [TestMethod]
        public void App_Export_OverriddenSource_DefaultTarget_1()
        {
            Test(new[] { "EXPORT", "-SOURCE", "https://localhost:4242" },
                DefaultSettings,
                typeof(RepositoryReader), "Url: https://localhost:4242, Path: /Root/Content, BlockSize: 10",
                typeof(FsWriter), "Path: Q:\\_sn-io-test");
        }
        [TestMethod]
        public void App_Export_OverriddenSource_DefaultTarget_2()
        {
            Test(new[] { "EXPORT", "-SOURCE", "https://localhost:4242", "\"/Root/Backup\"" },
                DefaultSettings,
                typeof(RepositoryReader), "Url: https://localhost:4242, Path: /Root/Backup, BlockSize: 10",
                typeof(FsWriter), "Path: Q:\\_sn-io-test");
        }
        [TestMethod]
        public void App_Export_OverriddenSource_DefaultTarget_3()
        {
            Test(new[] { "EXPORT", "-SOURCE", "https://localhost:4242", "\"/Root/Backup\"", "-BLOCKSIZE", "42" },
                DefaultSettings,
                typeof(RepositoryReader), "Url: https://localhost:4242, Path: /Root/Backup, BlockSize: 42",
                typeof(FsWriter), "Path: Q:\\_sn-io-test");
        }
        [TestMethod]
        public void App_Export_OverriddenSource_DefaultTarget_4()
        {
            Test(new[] { "EXPORT", "-SOURCE", "https://localhost:4242", "-BLOCKSIZE", "42" },
                DefaultSettings,
                typeof(RepositoryReader), "Url: https://localhost:4242, Path: /Root/Content, BlockSize: 42",
                typeof(FsWriter), "Path: Q:\\_sn-io-test");
        }
        [TestMethod]
        public void App_Export_OverriddenSource_DefaultTarget_5()
        {
            Test(new[] { "EXPORT", "-SOURCE", "-BLOCKSIZE", "42" },
                DefaultSettings,
                typeof(RepositoryReader), "Url: https://localhost, Path: /Root/Content, BlockSize: 42",
                typeof(FsWriter), "Path: Q:\\_sn-io-test");
        }
        [TestMethod]
        public void App_Export_OverriddenSource_DefaultTarget_6()
        {
            Test(new[] { "EXPORT", "-SOURCE", "-FILTER", "+TypeIs:File" },
                DefaultSettings,
                typeof(RepositoryReader), "Url: https://localhost, Path: /Root/Content, Filter: +TypeIs:File, BlockSize: 10",
                typeof(FsWriter), "Path: Q:\\_sn-io-test");
        }
        [TestMethod]
        public void App_Export_DefaultSource_OverriddenTarget()
        {
            Test(new[] { "EXPORT", "-TARGET", @"Q:\_sn-io-test\content1" },
                DefaultSettings,
                typeof(RepositoryReader), "Url: https://localhost, Path: /Root/Content, BlockSize: 10",
                typeof(FsWriter), "Path: Q:\\_sn-io-test\\content1");
        }
        [TestMethod]
        public void App_Export_OverriddenSource_OverriddenTarget()
        {
            Test(new[] { "EXPORT", "-SOURCE", "https://localhost:4242", "\"/Root/Backup\"", "-BLOCKSIZE", "42", "-TARGET", @"Q:\_sn-io-test\content1" },
                DefaultSettings,
                typeof(RepositoryReader), "Url: https://localhost:4242, Path: /Root/Backup, BlockSize: 42",
                typeof(FsWriter), "Path: Q:\\_sn-io-test\\content1");
        }
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void App_Export_MissingSource()
        {
            Test(new[] { "EXPORT", "-TARGET", "Q:\\_sn-io-test\\content1" },
                @"{
  ""repositoryReader"": { },
  ""repositoryWriter"": { ""url"": ""https://localhost"", ""path"": ""/Root"", ""name"": ""Content"" },
  ""fsReader"": { ""path"": ""Q:\\_sn-io-test\\content""  },
  ""fsWriter"": { ""path"": ""Q:\\_sn-io-test"", } }",
                typeof(RepositoryReader), "Url: , Path: /Root, BlockSize: 10",
                typeof(FsWriter), "Path: Q:\\_sn-io-test\\content1");
        }
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void App_Export_MissingTarget()
        {
            Test(new[] { "EXPORT", "-SOURCE", "https://localhost:4242", "\"/Root/Backup\"", "42" },
                @"{
  ""repositoryReader"": { ""url"": ""https://localhost"", ""path"": ""/Root/Content"", ""blockSize"": 10 },
  ""repositoryWriter"": { ""url"": ""https://localhost"", ""path"": ""/Root"", ""name"": ""Content"" },
  ""fsReader"": { ""path"": ""Q:\\_sn-io-test\\content"" },
  ""fsWriter"": { } }",
                typeof(RepositoryReader), "Url: https://localhost:4242, Path: /Root/Backup, BlockSize: 42",
                typeof(FsWriter), "Path: ");
        }

        [TestMethod]
        public void App_Copy_DefaultSource_DefaultTarget()
        {
            Test(new[] { "COPY" },
                @"{
  ""fsReader"": { ""path"": ""Q:\\_sn-io-test\\source\\content"" },
  ""fsWriter"": { ""path"": ""Q:\\_sn-io-test\\target"" }}",
                typeof(FsReader), "Path: Q:\\_sn-io-test\\source\\content",
                typeof(FsWriter), "Path: Q:\\_sn-io-test\\target");
        }
        [TestMethod]
        public void App_Copy_DefaultSource_OverriddenTarget()
        {
            Test(new[] { "COPY", "-TARGET", "Q:\\_sn-io-test\\target", "content2" },
                @"{
  ""fsReader"": { ""path"": ""Q:\\_sn-io-test\\source\\content"" },
  ""fsWriter"": { ""path"": ""Q:\\_sn-io-test\\target"" }}",
                typeof(FsReader), "Path: Q:\\_sn-io-test\\source\\content",
                typeof(FsWriter), "Path: Q:\\_sn-io-test\\target, Name: content2");
        }
        [TestMethod]
        public void App_Copy_OverriddenSource_DefaultTarget()
        {
            Test(new[] { "COPY", "-SOURCE", "Q:\\_sn-io-test\\source\\content1" },
                @"{
  ""fsReader"": { ""path"": ""Q:\\_sn-io-test\\source\\content"" },
  ""fsWriter"": { ""path"": ""Q:\\_sn-io-test\\target"" }}",
                typeof(FsReader), "Path: Q:\\_sn-io-test\\source\\content1",
                typeof(FsWriter), "Path: Q:\\_sn-io-test\\target");
        }
        [TestMethod]
        public void App_Copy_OverriddenSource_OverriddenTarget()
        {
            Test(new[] { "COPY", "-SOURCE", "Q:\\_sn-io-test\\source\\content1", "-TARGET", "Q:\\_sn-io-test\\target", "content2" },
                @"{
  ""fsReader"": { ""path"": ""Q:\\_sn-io-test\\source\\content"" },
  ""fsWriter"": { ""path"": ""Q:\\_sn-io-test\\target"" }}",
                typeof(FsReader), "Path: Q:\\_sn-io-test\\source\\content1",
                typeof(FsWriter), "Path: Q:\\_sn-io-test\\target, Name: content2");
        }


        [TestMethod]
        public void App_DisplaySettings_DisplayLevel_Default()
        {
            var settings = @"{
  ""repositoryReader"": { ""url"": ""https://localhost"", ""path"": ""/Root/Content"", ""blockSize"": 10 },
  ""fsWriter"": { ""path"": ""Q:\\_sn-io-test"" },
  ""display"": { ""level"": null }
}";
            var args = new[] {"EXPORT"};

            // ACTION
            var app = SenseNet.IO.CLI.Program.CreateApp(args, new MemoryStream(Encoding.UTF8.GetBytes(settings)));

            // ASSERT
            Assert.AreEqual(DisplayLevel.Errors, app.DisplaySettings.DisplayLevel);
        }
        [TestMethod]
        public void App_DisplaySettings_None()
        {
            var settings = @"{
  ""repositoryReader"": { ""url"": ""https://localhost"", ""path"": ""/Root/Content"", ""blockSize"": 10 },
  ""fsWriter"": { ""path"": ""Q:\\_sn-io-test"" },
  ""display"": { ""level"": ""none"" }
}";
            var args = new[] { "EXPORT" };

            // ACTION
            var app = SenseNet.IO.CLI.Program.CreateApp(args, new MemoryStream(Encoding.UTF8.GetBytes(settings)));

            // ASSERT
            Assert.AreEqual(DisplayLevel.None, app.DisplaySettings.DisplayLevel);
        }
        [TestMethod]
        public void App_DisplaySettings_DisplayLevel_Progress()
        {
            var settings = @"{
  ""repositoryReader"": { ""url"": ""https://localhost"", ""path"": ""/Root/Content"", ""blockSize"": 10 },
  ""fsWriter"": { ""path"": ""Q:\\_sn-io-test"" },
  ""display"": { ""level"": ""progress"" }
}";
            var args = new[] { "EXPORT" };

            // ACTION
            var app = SenseNet.IO.CLI.Program.CreateApp(args, new MemoryStream(Encoding.UTF8.GetBytes(settings)));

            // ASSERT
            Assert.AreEqual(DisplayLevel.Progress, app.DisplaySettings.DisplayLevel);
        }
        [TestMethod]
        public void App_DisplaySettings_DisplayLevel_Errors()
        {
            var settings = @"{
  ""repositoryReader"": { ""url"": ""https://localhost"", ""path"": ""/Root/Content"", ""blockSize"": 10 },
  ""fsWriter"": { ""path"": ""Q:\\_sn-io-test"" },
  ""display"": { ""level"": null }
}";
            var args = new[] { "EXPORT" };

            // ACTION
            var app = SenseNet.IO.CLI.Program.CreateApp(args, new MemoryStream(Encoding.UTF8.GetBytes(settings)));

            // ASSERT
            Assert.AreEqual(DisplayLevel.Errors, app.DisplaySettings.DisplayLevel);
        }
        [TestMethod]
        public void App_DisplaySettings_DisplayLevel_Verbose()
        {
            var settings = @"{
  ""repositoryReader"": { ""url"": ""https://localhost"", ""path"": ""/Root/Content"", ""blockSize"": 10 },
  ""fsWriter"": { ""path"": ""Q:\\_sn-io-test"" },
  ""display"": { ""level"": ""verbose"" }
}";
            var args = new[] { "EXPORT" };

            // ACTION
            var app = SenseNet.IO.CLI.Program.CreateApp(args, new MemoryStream(Encoding.UTF8.GetBytes(settings)));

            // ASSERT
            Assert.AreEqual(DisplayLevel.Verbose, app.DisplaySettings.DisplayLevel);
        }
        [TestMethod]
        public void App_DisplaySettings_DisplayLevel_Fake()
        {
            var settings = @"{
  ""repositoryReader"": { ""url"": ""https://localhost"", ""path"": ""/Root/Content"", ""blockSize"": 10 },
  ""fsWriter"": { ""path"": ""Q:\\_sn-io-test"" },
  ""display"": { ""level"": ""fake"" }
}";
            var args = new[] { "EXPORT" };

            try
            {
                // ACTION
                _ = SenseNet.IO.CLI.Program.CreateApp(args, new MemoryStream(Encoding.UTF8.GetBytes(settings)));
                // ASSERT
                Assert.Fail("Missing exception");
            }
            catch (TargetInvocationException tie)
            {
                if(!(tie.InnerException is ArgumentException ae))
                    Assert.Fail("Wrong exception type.");
            }
        }

        /* ============================================================ TOOLS */

        private void Test(string[] args, string settings,
            Type readerType, string expectedReaderParams,
            Type writerType, string expectedWriterParams)
        {
            // ACTION
            var app = SenseNet.IO.CLI.Program.CreateApp(args, new MemoryStream(Encoding.UTF8.GetBytes(settings)));

            // ASSERT
            Assert.AreEqual(readerType, app.Reader.GetType());
            Assert.AreEqual(expectedReaderParams, app.Reader.ParamsToDisplay());
            Assert.AreEqual(writerType, app.Writer.GetType());
            Assert.AreEqual(expectedWriterParams, app.Writer.ParamsToDisplay());
        }

        private Stream GetSettings(string src)
        {
            var bytes = Encoding.UTF8.GetBytes(src);
            return new MemoryStream(bytes);
        }
    }

}
