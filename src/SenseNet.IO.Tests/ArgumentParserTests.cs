using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SenseNet.IO.CLI;
using SenseNet.IO.Implementations;

namespace SenseNet.IO.Tests
{
    [TestClass]
    public class ArgumentParserTests
    {
        #region Mocks

        private class ArgumentParserMockForSectionTests : ArgumentParser
        {
            public Verb Verb { get; private set; }
            public string[] SourceArgs { get; private set; }
            public string[] TargetArgs { get; private set; }
            protected override IAppArguments ParseByVerb(Verb verb, string[] sourceArgs, string[] targetArgs)
            {
                Verb = verb;
                SourceArgs = sourceArgs;
                TargetArgs = targetArgs;

                return null;
            }
        }

        private class ArgumentParserMockForVerbTests : ArgumentParser
        {
            public string Reader { get; set; }
            public string[] ReaderArgs { get; set; }
            public string Writer { get; set; }
            public string[] WriterArgs { get; set; }
            protected override FsReaderArgs ParseFsReaderArgs(string[] args)
            {
                Reader = "FS";
                ReaderArgs = args;
                return null;
            }

            protected override FsWriterArgs ParseFsWriterArgs(string[] args)
            {
                Writer = "FS";
                WriterArgs = args;
                return null;
            }

            protected override RepositoryReaderArgs ParseRepositoryReaderArgs(string[] args)
            {
                Reader = "REPO";
                ReaderArgs = args;
                return null;
            }

            protected override RepositoryWriterArgs ParseRepositoryWriterArgs(string[] args)
            {
                Writer = "REPO";
                WriterArgs = args;
                return null;
            }
        }

        private class ArgumentParserMockForArgumentTests : ArgumentParser
        {
            public FsReaderArgs ParseFsReaderArgsTest(string[] args)
            {
                return base.ParseFsReaderArgs(args);
            }
            public FsWriterArgs ParseFsWriterArgsTest(string[] args)
            {
                return base.ParseFsWriterArgs(args);
            }
            public RepositoryReaderArgs ParseRepositoryReaderArgsTest(string[] args)
            {
                return base.ParseRepositoryReaderArgs(args);
            }
            public RepositoryWriterArgs ParseRepositoryWriterArgsTest(string[] args)
            {
                return base.ParseRepositoryWriterArgs(args);
            }
        }
        #endregion

        private static readonly StringComparison Cmp = StringComparison.OrdinalIgnoreCase;

        [TestMethod]
        public void ArgParser_Err_MissingVerb()
        {
            ArgumentParserException ex = null;
            try
            {
                var _ = new ArgumentParser().Parse(new string[0]);
                Assert.Fail("The expected ArgumentException was not thrown.");
            }
            catch (ArgumentParserException e)
            {
                ex = e;
            }
            Assert.IsTrue(ex.Message.Contains("Missing verb", Cmp));
        }
        [TestMethod]
        public void ArgParser_Err_InvalidVerb()
        {
            ArgumentParserException ex = null;
            try
            {
                var _ = new ArgumentParser().Parse(new[] { "Verb1", "Param1" });
                Assert.Fail("The expected ArgumentException was not thrown.");
            }
            catch (ArgumentParserException e)
            {
                ex = e;
            }
            Assert.IsTrue(ex.Message.Contains("Invalid verb", Cmp));
        }

        [TestMethod]
        public void ArgParser_Sections_Verb()
        {
            void VerbTest(string verb, Verb expected)
            {
                var parser = new ArgumentParserMockForSectionTests();

                // ACTION
                var _ = parser.Parse(new[] { verb });

                // ASSERT
                Assert.AreEqual(expected, parser.Verb);
                AssertSequencesAreEqual(Array.Empty<string>(), parser.SourceArgs);
                AssertSequencesAreEqual(Array.Empty<string>(), parser.TargetArgs);
            }

            VerbTest("export", Verb.Export);
            VerbTest("eXporT", Verb.Export);
            VerbTest("import", Verb.Import);
            VerbTest("copy", Verb.Copy);
            VerbTest("sync", Verb.Sync);
            VerbTest("transfer", Verb.Transfer);
        }
        [TestMethod]
        public void ArgParser_Sections_VerbSource()
        {
            var parser = new ArgumentParserMockForSectionTests();

            // ACTION
            var _ = parser.Parse(new[]
                {"EXPORT",  "-SOURCE", "source1", "source2", "source3"});

            // ASSERT
            Assert.AreEqual(Verb.Export, parser.Verb);
            AssertSequencesAreEqual(new[] { "source1", "source2", "source3" }, parser.SourceArgs);
            AssertSequencesAreEqual(Array.Empty<string>(), parser.TargetArgs);
        }
        [TestMethod]
        public void ArgParser_Sections_VerbTarget()
        {
            var parser = new ArgumentParserMockForSectionTests();

            // ACTION
            var _ = parser.Parse(new[]
                {"EXPORT", "-TARGET", "target1", "target2"});

            // ASSERT
            Assert.AreEqual(Verb.Export, parser.Verb);
            AssertSequencesAreEqual(Array.Empty<string>(), parser.SourceArgs);
            AssertSequencesAreEqual(new[] { "target1", "target2" }, parser.TargetArgs);
        }

        [TestMethod]
        public void ArgParser_Sections_VerbSourceTarget()
        {
            var parser = new ArgumentParserMockForSectionTests();

            // ACTION
            var _ = parser.Parse(new[]
                {"EXPORT",  "-SOURCE", "source1", "source2", "source3", "-TARGET", @"target1", "target2"});

            // ASSERT
            Assert.AreEqual(Verb.Export, parser.Verb);
            AssertSequencesAreEqual(new[] { "source1", "source2", "source3" }, parser.SourceArgs);
            AssertSequencesAreEqual(new[] { "target1", "target2" }, parser.TargetArgs);
        }
        [TestMethod]
        public void ArgParser_Sections_VerbTargetSource()
        {
            var parser = new ArgumentParserMockForSectionTests();

            // ACTION
            var _ = parser.Parse(new[]
                {"EXPORT", "-TARGET", "target1", "target2", "-SOURCE", "source1", "source2", "source3"});

            // ASSERT
            Assert.AreEqual(Verb.Export, parser.Verb);
            AssertSequencesAreEqual(new[] { "source1", "source2", "source3" }, parser.SourceArgs);
            AssertSequencesAreEqual(new[] { "target1", "target2" }, parser.TargetArgs);
        }

        [TestMethod]
        public void ArgParser_ArgRouting()
        {
            void Test(string reader, string writer, string[] args)
            {
                var parser = new ArgumentParserMockForVerbTests();
                // ACTION
                var _ = parser.Parse(args);

                Assert.AreEqual(reader, parser.Reader);
                Assert.AreEqual(writer, parser.Writer);
                Assert.AreEqual("source1", parser.ReaderArgs[0]);
                Assert.AreEqual("target1", parser.WriterArgs[0]);
            }

            Test("REPO", "FS", new[] { "EXPORT", "-SOURCE", "source1", "source2", "-TARGET", @"target1", "target2" });
            Test("FS", "REPO", new[] { "IMPORT", "-SOURCE", "source1", "source2", "-TARGET", @"target1", "target2" });
            Test("FS", "FS", new[] { "COPY", "-SOURCE", "source1", "source2", "-TARGET", @"target1", "target2" });
            Test("REPO", "REPO", new[] { "SYNC", "-SOURCE", "source1", "source2", "-TARGET", @"target1", "target2" });
        }
        [TestMethod]
        [ExpectedException(typeof(NotImplementedException))]
        public void ArgParser_ArgRouting_TransferNotImplemented()
        {
            // ACTION
            var _ = new ArgumentParser().Parse(new[] { "TRANSFER", "-SOURCE", "source1", "-TARGET", @"target1" });
        }

        [TestMethod]
        public void ArgParser_FsReaderArgs()
        {
            var parser = new ArgumentParserMockForArgumentTests();

            var fsReaderArgs = parser.ParseFsReaderArgsTest(new string[0]);
            Assert.AreEqual(null, fsReaderArgs.Path);

            try { parser.ParseFsReaderArgsTest(new[] { "-PATH" }); Assert.Fail(); }
            catch (ArgumentParserException) { /* do nothing */ }

            fsReaderArgs = parser.ParseFsReaderArgsTest(new[] { "q:\\readerPath" });
            Assert.AreEqual("q:\\readerPath", fsReaderArgs.Path);

            fsReaderArgs = parser.ParseFsReaderArgsTest(new[] { "-PATH", "q:\\readerPath" });
            Assert.AreEqual("q:\\readerPath", fsReaderArgs.Path);

            try { parser.ParseFsReaderArgsTest(new[] { "-PATH", "q:\\readerPath", "fake" }); Assert.Fail(); }
            catch (ArgumentParserException e) { Assert.IsTrue(e.Message.Contains("too many", Cmp)); }

            try { parser.ParseFsReaderArgsTest(new[] { "-NAME", "q:\\readerPath" }); Assert.Fail(); }
            catch (ArgumentParserException e) { Assert.IsTrue(e.Message.Contains("invalid", Cmp)); }
        }
        [TestMethod]
        public void ArgParser_FsWriterArgs()
        {
            var parser = new ArgumentParserMockForArgumentTests();
            FsWriterArgs fsWriterArgs;

            fsWriterArgs = parser.ParseFsWriterArgsTest(new string[0]);
            Assert.AreEqual(null, fsWriterArgs.Path);
            Assert.AreEqual(null, fsWriterArgs.Name);

            fsWriterArgs = parser.ParseFsWriterArgsTest(new[] { "q:\\writerPath" });
            Assert.AreEqual("q:\\writerPath", fsWriterArgs.Path);
            Assert.AreEqual(null, fsWriterArgs.Name);

            try { parser.ParseFsWriterArgsTest(new[] { "-fake" }); Assert.Fail(); }
            catch (ArgumentParserException e) { Assert.IsTrue(e.Message.Contains("Unknown", Cmp)); }

            fsWriterArgs = parser.ParseFsWriterArgsTest(new[] { "-PATH", "q:\\writerPath" });
            Assert.AreEqual("q:\\writerPath", fsWriterArgs.Path);
            Assert.AreEqual(null, fsWriterArgs.Name);

            fsWriterArgs = parser.ParseFsWriterArgsTest(new[] { "q:\\writerPath", "newName" });
            Assert.AreEqual("q:\\writerPath", fsWriterArgs.Path);
            Assert.AreEqual("newName", fsWriterArgs.Name);

            try { parser.ParseFsWriterArgsTest(new[] { "-PATH", "q:\\writerPath", "newName" }); Assert.Fail(); }
            catch (ArgumentParserException e) { Assert.IsTrue(e.Message.Contains("Invalid", Cmp)); }

            fsWriterArgs = parser.ParseFsWriterArgsTest(new[] { "q:\\writerPath", "-NAME", "newName" });
            Assert.AreEqual("q:\\writerPath", fsWriterArgs.Path);
            Assert.AreEqual("newName", fsWriterArgs.Name);

            fsWriterArgs = parser.ParseFsWriterArgsTest(new[] { "-PATH", "q:\\writerPath", "-NAME", "newName" });
            Assert.AreEqual("q:\\writerPath", fsWriterArgs.Path);
            Assert.AreEqual("newName", fsWriterArgs.Name);

            fsWriterArgs = parser.ParseFsWriterArgsTest(new[] { "-NAME", "newName", "-PATH", "q:\\writerPath" });
            Assert.AreEqual("q:\\writerPath", fsWriterArgs.Path);
            Assert.AreEqual("newName", fsWriterArgs.Name);

            fsWriterArgs = parser.ParseFsWriterArgsTest(new[] { "-NAME", "newName", "q:\\writerPath" });
            Assert.AreEqual("q:\\writerPath", fsWriterArgs.Path);
            Assert.AreEqual("newName", fsWriterArgs.Name);

            try { parser.ParseFsWriterArgsTest(new[] { "newName", "-PATH", "q:\\readerPath" }); Assert.Fail(); }
            catch (ArgumentParserException e) { Assert.IsTrue(e.Message.Contains("Invalid", Cmp)); }
        }
        [TestMethod]
        public void ArgParser_RepositoryReaderArgs()
        {
            var parser = new ArgumentParserMockForArgumentTests();
            RepositoryReaderArgs repoReaderArgs;

            repoReaderArgs = parser.ParseRepositoryReaderArgsTest(new string[0]);
            Assert.AreEqual(null, repoReaderArgs.Url);
            Assert.AreEqual(null, repoReaderArgs.Path);
            Assert.AreEqual(null, repoReaderArgs.BlockSize);

            repoReaderArgs = parser.ParseRepositoryReaderArgsTest(new[] { "https://localhost" });
            Assert.AreEqual("https://localhost", repoReaderArgs.Url);
            Assert.AreEqual(null, repoReaderArgs.Path);
            Assert.AreEqual(null, repoReaderArgs.BlockSize);

            repoReaderArgs = parser.ParseRepositoryReaderArgsTest(new[] { "https://localhost", "'/Root'" });
            Assert.AreEqual("https://localhost", repoReaderArgs.Url);
            Assert.AreEqual("/Root", repoReaderArgs.Path);
            Assert.AreEqual(null, repoReaderArgs.BlockSize);

            repoReaderArgs = parser.ParseRepositoryReaderArgsTest(new[] { "https://localhost", "'/Root'", "42" });
            Assert.AreEqual("https://localhost", repoReaderArgs.Url);
            Assert.AreEqual("/Root", repoReaderArgs.Path);
            Assert.AreEqual(42, repoReaderArgs.BlockSize);

            repoReaderArgs = parser.ParseRepositoryReaderArgsTest(new[] { "https://localhost", "-BLOCKSIZE", "42" });
            Assert.AreEqual("https://localhost", repoReaderArgs.Url);
            Assert.AreEqual(null, repoReaderArgs.Path);
            Assert.AreEqual(42, repoReaderArgs.BlockSize);

            repoReaderArgs = parser.ParseRepositoryReaderArgsTest(new[] { "https://localhost", "-BLOCKSIZE", "42", "-PATH", "'/Root'" });
            Assert.AreEqual("https://localhost", repoReaderArgs.Url);
            Assert.AreEqual("/Root", repoReaderArgs.Path);
            Assert.AreEqual(42, repoReaderArgs.BlockSize);

            repoReaderArgs = parser.ParseRepositoryReaderArgsTest(new[] { "-URL", "https://localhost", "-BLOCKSIZE", "42", "-PATH", "'/Root'" });
            Assert.AreEqual("https://localhost", repoReaderArgs.Url);
            Assert.AreEqual("/Root", repoReaderArgs.Path);
            Assert.AreEqual(42, repoReaderArgs.BlockSize);

            repoReaderArgs = parser.ParseRepositoryReaderArgsTest(new[] { "-BLOCKSIZE", "42", "-PATH", "'/Root'", "-URL", "https://localhost" });
            Assert.AreEqual("https://localhost", repoReaderArgs.Url);
            Assert.AreEqual("/Root", repoReaderArgs.Path);
            Assert.AreEqual(42, repoReaderArgs.BlockSize);

            repoReaderArgs = parser.ParseRepositoryReaderArgsTest(new[] { "-BLOCKSIZE", "42", "-URL", "https://localhost" });
            Assert.AreEqual("https://localhost", repoReaderArgs.Url);
            Assert.AreEqual(null, repoReaderArgs.Path);
            Assert.AreEqual(42, repoReaderArgs.BlockSize);

            repoReaderArgs = parser.ParseRepositoryReaderArgsTest(new[] { "-BLOCKSIZE", "42" });
            Assert.AreEqual(null, repoReaderArgs.Url);
            Assert.AreEqual(null, repoReaderArgs.Path);
            Assert.AreEqual(42, repoReaderArgs.BlockSize);

            try { parser.ParseRepositoryReaderArgsTest(new[] { "-fake" }); Assert.Fail(); }
            catch (ArgumentParserException e) { Assert.IsTrue(e.Message.Contains("Unknown", Cmp)); }

            try { parser.ParseRepositoryReaderArgsTest(new[] { "-URL", "https://localhost", "-PATH", "'/Root'", "42" }); Assert.Fail(); }
            catch (ArgumentParserException e) { Assert.IsTrue(e.Message.Contains("Invalid", Cmp)); }

            try { parser.ParseRepositoryReaderArgsTest(new[] { "-URL", "https://localhost", "'/Root'", "-BLOCKSIZE", "42" }); Assert.Fail(); }
            catch (ArgumentParserException e) { Assert.IsTrue(e.Message.Contains("Invalid", Cmp)); }

            try { parser.ParseRepositoryReaderArgsTest(new[] { "-URL", "https://localhost", "'/Root'", "42" }); Assert.Fail(); }
            catch (ArgumentParserException e) { Assert.IsTrue(e.Message.Contains("Invalid", Cmp)); }

        }
        [TestMethod]
        public void ArgParser_RepositoryWriterArgs()
        {
            var parser = new ArgumentParserMockForArgumentTests();
            RepositoryWriterArgs repoWriterArgs;

            repoWriterArgs = parser.ParseRepositoryWriterArgsTest(new string[0]);
            Assert.AreEqual(null, repoWriterArgs.Url);
            Assert.AreEqual(null, repoWriterArgs.Path);
            Assert.AreEqual(null, repoWriterArgs.Name);

            repoWriterArgs = parser.ParseRepositoryWriterArgsTest(new[] { "https://localhost" });
            Assert.AreEqual("https://localhost", repoWriterArgs.Url);
            Assert.AreEqual(null, repoWriterArgs.Path);
            Assert.AreEqual(null, repoWriterArgs.Name);

            repoWriterArgs = parser.ParseRepositoryWriterArgsTest(new[] { "https://localhost", "'/Root'" });
            Assert.AreEqual("https://localhost", repoWriterArgs.Url);
            Assert.AreEqual("/Root", repoWriterArgs.Path);
            Assert.AreEqual(null, repoWriterArgs.Name);

            repoWriterArgs = parser.ParseRepositoryWriterArgsTest(new[] { "https://localhost", "'/Root'", "NewName" });
            Assert.AreEqual("https://localhost", repoWriterArgs.Url);
            Assert.AreEqual("/Root", repoWriterArgs.Path);
            Assert.AreEqual("NewName", repoWriterArgs.Name);

            repoWriterArgs = parser.ParseRepositoryWriterArgsTest(new[] { "https://localhost", "-NAME", "NewName" });
            Assert.AreEqual("https://localhost", repoWriterArgs.Url);
            Assert.AreEqual(null, repoWriterArgs.Path);
            Assert.AreEqual("NewName", repoWriterArgs.Name);

            repoWriterArgs = parser.ParseRepositoryWriterArgsTest(new[] { "https://localhost", "-NAME", "NewName", "-PATH", "'/Root'" });
            Assert.AreEqual("https://localhost", repoWriterArgs.Url);
            Assert.AreEqual("/Root", repoWriterArgs.Path);
            Assert.AreEqual("NewName", repoWriterArgs.Name);

            repoWriterArgs = parser.ParseRepositoryWriterArgsTest(new[] { "-URL", "https://localhost", "-NAME", "NewName", "-PATH", "'/Root'" });
            Assert.AreEqual("https://localhost", repoWriterArgs.Url);
            Assert.AreEqual("/Root", repoWriterArgs.Path);
            Assert.AreEqual("NewName", repoWriterArgs.Name);

            repoWriterArgs = parser.ParseRepositoryWriterArgsTest(new[] { "-NAME", "NewName", "-PATH", "'/Root'", "-URL", "https://localhost" });
            Assert.AreEqual("https://localhost", repoWriterArgs.Url);
            Assert.AreEqual("/Root", repoWriterArgs.Path);
            Assert.AreEqual("NewName", repoWriterArgs.Name);

            repoWriterArgs = parser.ParseRepositoryWriterArgsTest(new[] { "-NAME", "NewName", "-URL", "https://localhost" });
            Assert.AreEqual("https://localhost", repoWriterArgs.Url);
            Assert.AreEqual(null, repoWriterArgs.Path);
            Assert.AreEqual("NewName", repoWriterArgs.Name);

            repoWriterArgs = parser.ParseRepositoryWriterArgsTest(new[] { "-NAME", "NewName" });
            Assert.AreEqual(null, repoWriterArgs.Url);
            Assert.AreEqual(null, repoWriterArgs.Path);
            Assert.AreEqual("NewName", repoWriterArgs.Name);

            try { parser.ParseRepositoryWriterArgsTest(new[] { "-fake" }); Assert.Fail(); }
            catch (ArgumentParserException e) { Assert.IsTrue(e.Message.Contains("Unknown", Cmp)); }

            try { parser.ParseRepositoryWriterArgsTest(new[] { "-URL", "https://localhost", "-PATH", "'/Root'", "NewName" }); Assert.Fail(); }
            catch (ArgumentParserException e) { Assert.IsTrue(e.Message.Contains("Invalid", Cmp)); }

            try { parser.ParseRepositoryWriterArgsTest(new[] { "-URL", "https://localhost", "'/Root'", "-NAME", "NewName" }); Assert.Fail(); }
            catch (ArgumentParserException e) { Assert.IsTrue(e.Message.Contains("Invalid", Cmp)); }

            try { parser.ParseRepositoryWriterArgsTest(new[] { "-URL", "https://localhost", "'/Root'", "NewName" }); Assert.Fail(); }
            catch (ArgumentParserException e) { Assert.IsTrue(e.Message.Contains("Invalid", Cmp)); }

        }

        /* ========================================================================== TOOLS */

        private void AssertSequencesAreEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual)
        {
            var e = string.Join(", ", expected.Select(x => x.ToString()));
            var a = string.Join(", ", actual.Select(x => x.ToString()));
            Assert.AreEqual(e, a);
        }
    }
}
