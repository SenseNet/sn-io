using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SenseNet.IO.Implementations
{
    internal class Level1ContentFlow : ContentFlow
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

        public override IContentReader Reader { get; }
        public override IContentWriter Writer { get; }
        public Level1ContentFlow(IContentReader reader, IContentWriter writer)
        {
            Reader = reader;
            Writer = writer;
        }

        private int _contentCount = 0;
        private string _currentBatchAction;
        private int _errorCount;
        private string _rootName;
        public override async Task TransferAsync(IProgress<TransferState> progress, CancellationToken cancel = default)
        {
            _rootName = Writer.RootName ?? Reader.RootName;

            if (await Reader.ReadAllAsync(new string[0], cancel))
            {
                if (Writer.RootName != null)
                    Rename(Reader.Content, _rootName);

                _currentBatchAction = "Transfer contents";
                WriteLog($"------------ {_currentBatchAction.ToUpper()} ------------");

                await WriteAsync(progress, false, cancel);
                while (await Reader.ReadAllAsync(new string[0], cancel))
                {
                    await WriteAsync(progress, false, cancel);
                }
            }
        }

        private async Task WriteAsync(IProgress<TransferState> progress, bool updateReferences, CancellationToken cancel = default)
        {
            var readerPath = Reader.RelativePath;
            var writerPath = ContentPath.Combine(_rootName, readerPath);
            var state = await Writer.WriteAsync(writerPath, Reader.Content, cancel);
            state.ReaderPath = readerPath;
            state.WriterPath = writerPath;
            Progress(readerPath, ref _contentCount, state, updateReferences, progress);
        }
        private void Progress(string readerPath, ref int count, WriterState state, bool updateReferences, IProgress<TransferState> progress = null)
        {
            if(state.Action == WriterAction.Failed)
                _errorCount++;

            WriteLogAndTask(state, updateReferences);

            progress?.Report(new TransferState
            {
                CurrentBatchAction = _currentBatchAction,
                CurrentCount = ++count,
                TotalCount = Reader.EstimatedCount + _referenceUpdateTasksTotalCount,
                ErrorCount = _errorCount,
                UpdatingReferences = updateReferences,
                State = state,
            });
        }
    }
}
