using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SenseNet.IO.Implementations;
using SenseNet.IO.CLI;

namespace SenseNet.IO.Tests
{
    [TestClass]
    public class FsXmlReaderTests
    {
        /// <summary>
        /// Fields that should be excluded during import (not written to target).
        /// </summary>
        private static readonly HashSet<string> ExcludedImportFields = new(StringComparer.OrdinalIgnoreCase)
        {
            "ApprovingMode",
            "CreatedBy",
            "CreationDate",
            "DateTaken",
            "GroupAttachments",
            "IconName",
            "InheritableApprovingMode",
            "InheritableVersioningMode",
            "IsRateable",
            "IsTaggable",
            "ModificationDate",
            "ModifiedBy",
            "NotificationMode",
            "Owner",
            "PageCount",
            "RateAvg",
            "RateCount",
            "Sharing",
            "SeeAlso",
            "TrashDisabled",
            "VersionCreatedBy",
            "VersionCreationDate",
            "VersionModificationDate",
            "VersionModifiedBy",
            "VersioningMode"
        };

        /// <summary>
        /// Fields that should be excluded from export (should not appear in source reading).
        /// </summary>
        private static readonly HashSet<string> ExcludedExportFields = new(StringComparer.OrdinalIgnoreCase)
        {
            "Actions", "AllFieldSettingContents", "AllRoles", "Approvable", "ApprovingMode",
            "AvailableContentTypeFields", "AvailableViews", "BrowseApplication", "BrowseUrl",
            "CheckInComments", "CheckedOutTo", "Children", "CreatedById", "Depth", "DirectRoles",
            "EffectiveAllowedChildTypes", "ExtensionData", "FieldSettingContents", "HandlerName",
            "Icon", "Id", "Image", "InFolder", "InTree", "InheritableApprovingMode",
            "InheritableVersioningMode", "IsFile", "IsFolder", "IsSystemContent", "Locked",
            "ModifiedById", "Name", "OwnerId", "OwnerWhenVisitor", "ParentId", "ParentTypeName",
            "Path", "Publishable", "Rate", "RateStr", "RejectReason", "SavingState", "SharedBy",
            "SharedWith", "Sharing", "SharingLevel", "SharingMode", "Tags", "Type", "TypeIs",
            "Version", "VersionCreatedBy", "VersionCreationDate", "VersionId",
            "VersionModificationDate", "VersionModifiedBy", "VersioningMode", "Versions",
            "Workspace", "WorkspaceSkin"
        };

        /// <summary>
        /// Path mappings from SenseNet 6 structure to SenseNet 7+ structure.
        /// </summary>
        private static readonly Dictionary<string, string> PathMappings = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Root/Sites", "Root/Content" },
        };

        /// <summary>
        /// Subtrees to skip during conversion (obsolete in SenseNet 7+).
        /// </summary>
        private static readonly string[] SkippedSubtrees =
        {
            "Root/(apps)",
            "Root/Skins",
            "Root/Portlets",
        };

        /// <summary>
        /// Applies SenseNet 6 → 7+ path transformations.
        /// </summary>
        private static string TransformPath(string relativePath)
        {
            foreach (var mapping in PathMappings)
            {
                if (relativePath.Equals(mapping.Key, StringComparison.OrdinalIgnoreCase))
                    return mapping.Value;
                if (relativePath.StartsWith(mapping.Key + "/", StringComparison.OrdinalIgnoreCase))
                    return mapping.Value + relativePath.Substring(mapping.Key.Length);
            }
            return relativePath;
        }

        /// <summary>
        /// Converts old SenseNet 6 XML export from c:\tmp\export2\ to new JSON format in c:\tmp\exportuj\
        /// using FsXmlReader as source and FsWriter as output.
        /// Applies field filtering and path transformations for SenseNet 6 → 7+ migration.
        /// </summary>
        [TestMethod]
        public async Task ConvertOldXmlExportToNewJsonFormat()
        {
            var sourcePath = @"c:\tmp\export2";
            var targetPath = @"c:\tmp\exportuj";

            var loggerFactory = LoggerFactory.Create(builder => builder.AddDebug());

            // Combine all excluded fields
            var allExcludedFields = ExcludedImportFields.Union(ExcludedExportFields).ToArray();

            var readerArgs = Options.Create(new FsXmlReaderArgs 
            { 
                Path = sourcePath, 
                Skip = SkippedSubtrees,
                ExcludedFields = allExcludedFields
            });
            var reader = new FsXmlReader(readerArgs, loggerFactory.CreateLogger<FsXmlReader>());

            var writerArgs = Options.Create(new FsWriterArgs { Path = targetPath });
            var writer = new FsWriter(writerArgs, loggerFactory.CreateLogger<FsWriter>());

            await reader.InitializeAsync();
            await writer.InitializeAsync();

            var cancel = CancellationToken.None;
            var count = 0;

            while (await reader.ReadAllAsync(Array.Empty<string>(), cancel))
            {
                var content = reader.Content;
                var relativePath = reader.RelativePath;

                if (string.IsNullOrEmpty(relativePath))
                    relativePath = content.Name;

                // Apply path transformation (e.g., Root/Sites → Root/Content)
                var targetRelativePath = TransformPath(relativePath);

                var result = await writer.WriteAsync(targetRelativePath, content, cancel);
                count++;
            }

            Assert.IsTrue(count > 0, $"No content was read from the source directory.");
        }

        /// <summary>
        /// Tests the CLI COPYXML verb with FsXmlReader and FsWriter using command line arguments.
        /// 
        /// Command line usage:
        /// SenseNet.IO.CLI.exe COPYXML -source "c:\tmp\export2" -SKIP "Root/(apps),Root/Skins,Root/Portlets" -target "c:\tmp\exportuj"
        /// 
        /// This converts old SenseNet 6 XML exports to new JSON format on the filesystem.
        /// </summary>
        [TestMethod]
        public void Test_CLI_CopyXmlToJson()
        {
            var commandLineArgs = new[]
            {
                "COPYXML",
                "-source", "c:\\tmp\\export2",
                "-SKIP", "Root/(apps),Root/Skins,Root/Portlets",
                "-target", "c:\\tmp\\exportuj"
            };

            var parser = new ArgumentParser();
            var arguments = parser.Parse(commandLineArgs);

            Assert.IsNotNull(arguments);
            Assert.AreEqual(Verb.CopyXml, arguments.Verb);

            var copyXmlArgs = (CopyXmlArguments)arguments;
            Assert.AreEqual("c:\\tmp\\export2", copyXmlArgs.ReaderArgs.Path);
            Assert.AreEqual(3, copyXmlArgs.ReaderArgs.Skip.Length);
            Assert.AreEqual("c:\\tmp\\exportuj", copyXmlArgs.WriterArgs.Path);
        }

        /// <summary>
        /// Tests the CLI COPYXML verb with excluded fields parameter.
        /// 
        /// Command line usage:
        /// SenseNet.IO.CLI.exe COPYXML -source "c:\tmp\export2" -EXCLUDEFIELDS "CreatedBy,ModifiedBy,Actions,Id" -target "c:\tmp\exportuj"
        /// </summary>
        [TestMethod]
        public void Test_CLI_CopyXmlToJson_WithExcludedFields()
        {
            var commandLineArgs = new[]
            {
                "COPYXML",
                "-source", "c:\\tmp\\export2",
                "-EXCLUDEFIELDS", "CreatedBy,ModifiedBy,Actions,Id,Type,Version",
                "-target", "c:\\tmp\\exportuj"
            };

            var parser = new ArgumentParser();
            var arguments = parser.Parse(commandLineArgs);

            Assert.IsNotNull(arguments);
            Assert.AreEqual(Verb.CopyXml, arguments.Verb);

            var copyXmlArgs = (CopyXmlArguments)arguments;
            Assert.AreEqual("c:\\tmp\\export2", copyXmlArgs.ReaderArgs.Path);
            Assert.IsNotNull(copyXmlArgs.ReaderArgs.ExcludedFields);
            Assert.AreEqual(6, copyXmlArgs.ReaderArgs.ExcludedFields.Length);
            Assert.IsTrue(copyXmlArgs.ReaderArgs.ExcludedFields.Contains("CreatedBy"));
            Assert.IsTrue(copyXmlArgs.ReaderArgs.ExcludedFields.Contains("Version"));
            Assert.AreEqual("c:\\tmp\\exportuj", copyXmlArgs.WriterArgs.Path);
        }
    }
}
