using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SenseNet.Extensions.DependencyInjection;
using SenseNet.IO.Implementations;

namespace SenseNet.IO.Tests
{
    [TestClass]
    public class ServicesTests : TestBase
    {
        [TestMethod]
        public void Services_Defaults()
        {
            var services = new ServiceCollection()
                .AddLogging()
                .AddSenseNetIO()
                .BuildServiceProvider();

            var importer = services.GetRequiredService<IImportFlowFactory>();
            var exporter = services.GetRequiredService<IExportFlowFactory>();
            var copier = services.GetRequiredService<ICopyFlowFactory>();
            var synchronizer = services.GetRequiredService<ISynchronizeFlowFactory>();
            
            //------------ Importer

            var impF1 = importer.Create();

            Assert.IsTrue(impF1 is ImportContentFlow);
            Assert.IsTrue(impF1.Reader is FsReader);
            Assert.IsTrue(impF1.Writer is RepositoryWriter);
            
            //------------ Exporter

            var expF1 = exporter.Create();

            Assert.IsTrue(expF1 is ExportContentFlow);
            Assert.IsTrue(expF1.Reader is RepositoryReader);
            Assert.IsTrue(expF1.Writer is FsWriter);

            //------------ Copier

            var copyF1 = copier.Create();
            
            Assert.IsTrue(copyF1 is CopyContentFlow);
            Assert.IsTrue(copyF1.Reader is FsReader);
            Assert.IsTrue(copyF1.Writer is FsWriter);

            //------------ Synchronizer

            var synchF1 = synchronizer.Create();
            
            Assert.IsTrue(synchF1 is SynchronizeContentFlow);
            Assert.IsTrue(synchF1.Reader is RepositoryReader);
            Assert.IsTrue(synchF1.Writer is RepositoryWriter);
        }

        [TestMethod]
        public void Services_Configuration_Custom()

        {
            // register the feature with global config values
            var services = new ServiceCollection()
                .AddLogging()
                .AddSenseNetIO(
                    fsReader => { fsReader.Path = "c:\\source"; },
                    repoReader =>
                    {
                        repoReader.Path = "/Root/abc";
                        repoReader.Url = "abc";
                    },
                    fsWriter => { fsWriter.Path = "c:\\target"; },
                    repoWriter =>
                    {
                        repoWriter.Path = "/Root/def";
                        repoWriter.Url = "def";
                    })
                .BuildServiceProvider();

            var importer = services.GetRequiredService<IImportFlowFactory>();
            var exporter = services.GetRequiredService<IExportFlowFactory>();
            var copier = services.GetRequiredService<ICopyFlowFactory>();
            var synchronizer = services.GetRequiredService<ISynchronizeFlowFactory>();

            //------------ Importer

            var impF1 = importer.Create();
            var impF2 = importer.Create(
                ropt => { ropt.Path = "d:\\temp"; },
                wopt => { wopt.Path = "/Root/xyz"; });

            Assert.AreNotSame(impF1, impF2);
            
            Assert.AreEqual(((FsReader)impF1.Reader).ReaderOptions.Path, "c:\\source");
            Assert.AreEqual(((FsReader)impF2.Reader).ReaderOptions.Path, "d:\\temp");
            Assert.AreEqual(((RepositoryWriter)impF1.Writer).WriterOptions.Path, "/Root/def");
            Assert.AreEqual(((RepositoryWriter)impF2.Writer).WriterOptions.Path, "/Root/xyz");

            //------------ Exporter

            var expF1 = exporter.Create();
            var expF2 = exporter.Create(
                ropt => { ropt.Path = "/Root/xyz"; },
                wopt => { wopt.Path = "d:\\temp"; });

            Assert.AreNotSame(expF1, expF2);
            
            Assert.AreEqual(((RepositoryReader)expF1.Reader).ReaderOptions.Path, "/Root/abc");
            Assert.AreEqual(((RepositoryReader)expF2.Reader).ReaderOptions.Path, "/Root/xyz");
            Assert.AreEqual(((FsWriter)expF1.Writer).WriterOptions.Path, "c:\\target");
            Assert.AreEqual(((FsWriter)expF2.Writer).WriterOptions.Path, "d:\\temp");

            //------------ Copier

            var copyF1 = copier.Create();
            var copyF2 = copier.Create(
                ropt => { ropt.Path = "d:\\temp"; },
                wopt => { wopt.Path = "e:\\temp"; });

            Assert.AreNotSame(copyF1, copyF2);

            Assert.AreEqual(((FsReader)copyF1.Reader).ReaderOptions.Path, "c:\\source");
            Assert.AreEqual(((FsReader)copyF2.Reader).ReaderOptions.Path, "d:\\temp");
            Assert.AreEqual(((FsWriter)copyF1.Writer).WriterOptions.Path, "c:\\target");
            Assert.AreEqual(((FsWriter)copyF2.Writer).WriterOptions.Path, "e:\\temp");

            //------------ Synchronizer

            var synchF1 = synchronizer.Create();
            var synchF2 = synchronizer.Create(
                ropt => { ropt.Path = "/Root/xyz"; },
                wopt => { wopt.Path = "/Root/other"; });

            Assert.AreNotSame(synchF1, synchF2);

            Assert.AreEqual(((RepositoryReader)synchF1.Reader).ReaderOptions.Path, "/Root/abc");
            Assert.AreEqual(((RepositoryReader)synchF2.Reader).ReaderOptions.Path, "/Root/xyz");
            Assert.AreEqual(((RepositoryWriter)synchF1.Writer).WriterOptions.Path, "/Root/def");
            Assert.AreEqual(((RepositoryWriter)synchF2.Writer).WriterOptions.Path, "/Root/other");
        }
    }
}
