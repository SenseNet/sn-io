﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SenseNet.IO
{
    public class ContentFlow : IContentFlow
    {
        private class InitialContent : IContent
        {
            public string[] FieldNames { get; } = new string[0];

            public object this[string fieldName]
            {
                get => null;
                set => throw new NotImplementedException();
            }

            public string Name { get; set; }
            public string Path { get; }
            public string Type { get; }
            public PermissionInfo Permissions { get; set; }

            public InitialContent(string name, string type)
            {
                Name = name;
                Type = type;
            }

            public Task<Attachment[]> GetAttachmentsAsync()
            {
                return Task.FromResult(Array.Empty<Attachment>());
            }
        }

        private static readonly Dictionary<string, IContent> InitialContents = new Dictionary<string, IContent>
        {
            {"/Root", new InitialContent("Root", "PortalRoot")},
            {"/Root/System", new InitialContent("System", "SystemFolder")},
            {"/Root/System/Settings", new InitialContent("Settings", "SystemFolder")},
            {"/Root/System/Schema", new InitialContent("Schema", "SystemFolder")},
            {"/Root/System/Schema/Aspects", new InitialContent("Aspects", "SystemFolder")},
            {"/Root/System/Schema/ContentTypes", new InitialContent("ContentTypes", "SystemFolder")},
        };

        public IContentReader Reader { get; }
        public IContentWriter Writer { get; }
        public ContentFlow(IContentReader reader, IContentWriter writer)
        {
            Reader = reader;
            Writer = writer;
        }

        private int _count = 0;
        private string _rootName;
        public async Task TransferAsync(IProgress<(string Path, double Percent)> progress = null, CancellationToken cancel = default)
        {
            _rootName = Writer.RootName ?? ContentPath.GetName(Reader.RootPath);

            if (Reader.RootPath.Equals("/Root", StringComparison.OrdinalIgnoreCase))
            {
                if (await Reader.ReadContentTypesAsync(cancel))
                {
                    await EnsureRootAsync("", cancel);
                    await EnsureSystemAsync("System", cancel);
                    await EnsureSchemaAsync("System/Schema", cancel);
                    await EnsureContentTypesAsync("System/Schema/ContentTypes", cancel);

                    await WriteAsync(progress, cancel);
                    while (await Reader.ReadContentTypesAsync(cancel))
                        await WriteAsync(progress, cancel);
                }
                if (await Reader.ReadSettingsAsync(cancel))
                {
                    await EnsureRootAsync("", cancel);
                    await EnsureSystemAsync("System", cancel);
                    await EnsureSettingsAsync("System/Settings", cancel);

                    await WriteAsync(progress, cancel);
                    while (await Reader.ReadSettingsAsync(cancel))
                        await WriteAsync(progress, cancel);
                }
                if (await Reader.ReadAspectsAsync(cancel))
                {
                    await EnsureRootAsync("", cancel);
                    await EnsureSystemAsync("System", cancel);
                    await EnsureSchemaAsync("System/Schema", cancel);
                    await EnsureAspectsAsync("System/Schema/Aspects", cancel);

                    await WriteAsync(progress, cancel);
                    while (await Reader.ReadAspectsAsync(cancel))
                        await WriteAsync(progress, cancel);
                }
            }
            else if (Reader.RootPath.Equals("/Root/System", StringComparison.OrdinalIgnoreCase))
            {
                if (await Reader.ReadContentTypesAsync(cancel))
                {
                    await EnsureSystemAsync("", cancel);
                    await EnsureSchemaAsync("Schema", cancel);
                    await EnsureContentTypesAsync("Schema/ContentTypes", cancel);

                    await WriteAsync(progress, cancel);
                    while (await Reader.ReadContentTypesAsync(cancel))
                        await WriteAsync(progress, cancel);
                }
                if (await Reader.ReadSettingsAsync(cancel))
                {
                    await EnsureSystemAsync("", cancel);
                    await EnsureSettingsAsync("Settings", cancel);

                    await WriteAsync(progress, cancel);
                    while (await Reader.ReadSettingsAsync(cancel))
                        await WriteAsync(progress, cancel);
                }
                if (await Reader.ReadAspectsAsync(cancel))
                {
                    await EnsureSystemAsync("", cancel);
                    await EnsureSchemaAsync("Schema", cancel);
                    await EnsureAspectsAsync("Schema/Aspects", cancel);

                    await WriteAsync(progress, cancel);
                    while (await Reader.ReadAspectsAsync(cancel))
                        await WriteAsync(progress, cancel);
                }
            }
            else if (Reader.RootPath.Equals("/Root/System/Settings", StringComparison.OrdinalIgnoreCase))
            {
                if (await Reader.ReadSettingsAsync(cancel))
                {
                    await EnsureSettingsAsync("", cancel);

                    await WriteAsync(progress, cancel);
                    while (await Reader.ReadSettingsAsync(cancel))
                        await WriteAsync(progress, cancel);
                }
            }
            else if (Reader.RootPath.Equals("/Root/System/Schema", StringComparison.OrdinalIgnoreCase))
            {
                if (await Reader.ReadContentTypesAsync(cancel))
                {
                    await EnsureSchemaAsync("", cancel);
                    await EnsureContentTypesAsync("ContentTypes", cancel);

                    await WriteAsync(progress, cancel);
                    while (await Reader.ReadContentTypesAsync(cancel))
                        await WriteAsync(progress, cancel);
                }
                if (await Reader.ReadAspectsAsync(cancel))
                {
                    await EnsureSchemaAsync("", cancel);
                    await EnsureAspectsAsync("Aspects", cancel);

                    await WriteAsync(progress, cancel);
                    while (await Reader.ReadAspectsAsync(cancel))
                        await WriteAsync(progress, cancel);
                }
            }
            else if (Reader.RootPath.Equals("/Root/System/Schema/ContentTypes", StringComparison.OrdinalIgnoreCase))
            {
                if (await Reader.ReadContentTypesAsync(cancel))
                {
                    await EnsureContentTypesAsync("", cancel);

                    await WriteAsync(progress, cancel);
                    while (await Reader.ReadContentTypesAsync(cancel))
                        await WriteAsync(progress, cancel);
                }
            }
            else if (Reader.RootPath.Equals("/Root/System/Schema/Aspects", StringComparison.OrdinalIgnoreCase))
            {
                if (await Reader.ReadAspectsAsync(cancel))
                {
                    await EnsureAspectsAsync("", cancel);

                    await WriteAsync(progress, cancel);
                    while (await Reader.ReadAspectsAsync(cancel))
                        await WriteAsync(progress, cancel);
                }
            }

            await TransferAllAsync(progress, cancel);
        }

        private bool _rootWritten;
        private async Task EnsureRootAsync(string relativePath, CancellationToken cancel)
        {
            if (_rootWritten)
                return;
            await Writer.WriteAsync(ContentPath.Combine(_rootName, relativePath), InitialContents["/Root"], cancel);
            _rootWritten = true;
        }
        private bool _systemWritten;
        private async Task EnsureSystemAsync(string relativePath, CancellationToken cancel)
        {
            if (_systemWritten)
                return;
            await Writer.WriteAsync(ContentPath.Combine(_rootName, relativePath), InitialContents["/Root/System"], cancel);
            _systemWritten = true;
        }
        private bool _settingsWritten;
        private async Task EnsureSettingsAsync(string relativePath, CancellationToken cancel)
        {
            if (_settingsWritten)
                return;
            await Writer.WriteAsync(ContentPath.Combine(_rootName, relativePath), InitialContents["/Root/System/Settings"], cancel);
            _settingsWritten = true;
        }
        private bool _schemaWritten;
        private async Task EnsureSchemaAsync(string relativePath, CancellationToken cancel)
        {
            if (_schemaWritten)
                return;
            await Writer.WriteAsync(ContentPath.Combine(_rootName, relativePath), InitialContents["/Root/System/Schema"], cancel);
            _schemaWritten = true;
        }
        private bool _aspectsWritten;
        private async Task EnsureAspectsAsync(string relativePath, CancellationToken cancel)
        {
            if (_aspectsWritten)
                return;
            await Writer.WriteAsync(ContentPath.Combine(_rootName, relativePath), InitialContents["/Root/System/Schema/Aspects"], cancel);
            _aspectsWritten = true;
        }
        private bool _contentTypesWritten;
        private async Task EnsureContentTypesAsync(string relativePath, CancellationToken cancel)
        {
            if (_contentTypesWritten)
                return;
            await Writer.WriteAsync(ContentPath.Combine(_rootName, relativePath), InitialContents["/Root/System/Schema/ContentTypes"], cancel);
            _contentTypesWritten = true;
        }

        private async Task TransferAllAsync(IProgress<(string Path, double Percent)> progress = null, CancellationToken cancel = default)
        {
            if (await Reader.ReadAllAsync(cancel))
            {
                if (Writer.RootName != null)
                    Rename(Reader.Content, _rootName);

                await WriteAsync(progress, cancel);
                while (await Reader.ReadAllAsync(cancel))
                {
                    await WriteAsync(progress, cancel);
                }
            }
        }

        private async Task WriteAsync(IProgress<(string Path, double Percent)> progress, CancellationToken cancel = default)
        {
            var state = await Writer.WriteAsync(ContentPath.Combine(_rootName, Reader.RelativePath), Reader.Content, cancel);
            Progress(Reader.RelativePath, ref _count, state, progress);
        }
        private void Progress(string readerPath, ref int count, TransferState state, IProgress<(string Path, double Percent)> progress = null)
        {
            Console.Write($"{state.Action} {state.WriterPath}");
            Console.WriteLine(state.WriterPath.Length<40 ? new string(' ', 40-state.WriterPath.Length) : string.Empty);

            if(state.Action == TransferAction.Error)
                foreach (var message in state.Messages)
                    Console.WriteLine($"       {message.Replace("The server returned an error (HttpStatus: InternalServerError): ", "")}                               ");
            //UNDONE:!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!! Process TransferState

            ++count;
            var totalCount = Reader.EstimatedCount;
            if (totalCount > 0)
                progress?.Report((readerPath, count * 100.0 / totalCount));
        }

        private void Rename(IContent content, string newName)
        {
            if (content.FieldNames.Contains("Name"))
                content["Name"] = newName;
            content.Name = newName;
        }
    }
}
