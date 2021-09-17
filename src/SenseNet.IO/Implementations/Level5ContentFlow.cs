using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace SenseNet.IO.Implementations
{
    internal class Level5ContentFlow : ContentFlow, IContentFlow
    {
        public override IContentReader Reader { get; }
        IContentWriter IContentFlow.Writer => Writer;
        public override ISnRepositoryWriter Writer { get; }

        public Level5ContentFlow(IContentReader reader, ISnRepositoryWriter writer)
        {
            Reader = reader;
            Writer = writer;
        }

        private int _contentCount;
        private string _currentBatchAction;
        private int _errorCount;
        private string _rootName;
        public override async Task TransferAsync(IProgress<TransferState> progress, CancellationToken cancel = default)
        {
            var timer = Stopwatch.StartNew();

            _rootName = Writer.RootName ?? Reader.RootName;
            var firstTargetPath = ContentPath.Combine(Writer.ContainerPath, _rootName);

            string[] subTreePaths = new string[0];
            if (firstTargetPath.Equals("/Root", StringComparison.OrdinalIgnoreCase))
            {
                subTreePaths = new[] {"System/Schema/ContentTypes", "System/Settings", "System/Schema/Aspects",};
                await CopyContentTypesAsync(subTreePaths[0], progress, cancel);
                await CopySettingsAsync(subTreePaths[1], progress, cancel);
                await CopyAspectsAsync(subTreePaths[2], progress, cancel);
            }
            else if (firstTargetPath.Equals("/Root/System", StringComparison.OrdinalIgnoreCase))
            {
                subTreePaths = new[] { "Schema/ContentTypes", "Settings", "Schema/Aspects", };
                await CopyContentTypesAsync(subTreePaths[0], progress, cancel);
                await CopySettingsAsync(subTreePaths[1], progress, cancel);
                await CopyAspectsAsync(subTreePaths[2], progress, cancel);
            }
            else if (firstTargetPath.Equals("/Root/System/Settings", StringComparison.OrdinalIgnoreCase))
            {
                subTreePaths = new[] { "" };
                await CopySettingsAsync(subTreePaths[0], progress, cancel);
            }
            else if (firstTargetPath.Equals("/Root/System/Schema", StringComparison.OrdinalIgnoreCase))
            {
                subTreePaths = new[] { "ContentTypes", "Aspects", };
                await CopyContentTypesAsync(subTreePaths[0], progress, cancel);
                await CopyAspectsAsync(subTreePaths[1], progress, cancel);
            }
            else if (firstTargetPath.Equals("/Root/System/Schema/ContentTypes", StringComparison.OrdinalIgnoreCase))
            {
                subTreePaths = new[] { "", };
                await CopyContentTypesAsync(subTreePaths[0], progress, cancel);
            }
            else if (firstTargetPath.Equals("/Root/System/Schema/Aspects", StringComparison.OrdinalIgnoreCase))
            {
                subTreePaths = new[] { "", };
                await CopyAspectsAsync(subTreePaths[0], progress, cancel);
            }

            await CopyAllAsync(subTreePaths, progress, cancel);

            await UpdateReferencesAsync(progress, cancel);

            timer.Stop();
            WriteSummaryToLog(Reader.EstimatedCount, _contentCount, _errorCount, timer.Elapsed);
        }
        private async Task CopyContentTypesAsync(string relativePath, IProgress<TransferState> progress, CancellationToken cancel)
        {
            var firstRead = true;
            while (await Reader.ReadSubTreeAsync(relativePath, cancel))
            {
                if (Reader.Content.Type != "ContentType")
                    continue;
                if (firstRead)
                {
                    firstRead = false;
                    _currentBatchAction = "Transfer content types";
                    WriteLog($"------------ {_currentBatchAction.ToUpper()} ------------");

                    await EnsureContainerAsync("/Root/System/Schema/ContentTypes", cancel);
                }

                await WriteAsync(progress, false, cancel);
            }
        }
        private async Task CopyAspectsAsync(string relativePath, IProgress<TransferState> progress, CancellationToken cancel)
        {
            var firstContent = true;
            var firstRead = true;
            while (await Reader.ReadSubTreeAsync(relativePath, cancel))
            {
                if (firstContent)
                {
                    firstContent = false;
                    if (relativePath == "" && Writer.RootName != null)
                        Rename(Reader.Content, _rootName);

                    if (Reader.Content.Name == "Aspects" && Reader.Content.Type == "SystemFolder")
                        continue;
                }
                if (firstRead)
                {
                    firstRead = false;
                    _currentBatchAction = "Transfer aspect definitions";
                    WriteLog($"------------ {_currentBatchAction.ToUpper()} ------------");

                    await EnsureContainerAsync("/Root/System/Schema/Aspects", cancel);
                }

                await WriteAsync(progress, false, cancel);
            }
        }
        private async Task CopySettingsAsync(string relativePath, IProgress<TransferState> progress, CancellationToken cancel)
        {
            var firstContent = true;
            var firstRead = true;
            while (await Reader.ReadSubTreeAsync(relativePath, cancel))
            {
                if (firstContent)
                {
                    firstContent = false;
                    if (relativePath == "" && Writer.RootName != null)
                        Rename(Reader.Content, _rootName);

                    if (Reader.Content.Name == "Settings" && Reader.Content.Type == "SystemFolder")
                        continue;
                }
                if (firstRead)
                {
                    firstRead = false;
                    _currentBatchAction = "Transfer settings";
                    WriteLog($"------------ {_currentBatchAction.ToUpper()} ------------");

                    await EnsureContainerAsync("/Root/System/Settings", cancel);
                }

                await WriteAsync(progress, false, cancel);
            }
        }
        private async Task CopyAllAsync(string[] skippedSubTreePaths, IProgress<TransferState> progress = null, CancellationToken cancel = default)
        {
            if (await Reader.ReadAllAsync(skippedSubTreePaths, cancel))
            {
                if (Writer.RootName != null)
                    Rename(Reader.Content, _rootName);

                _currentBatchAction = "Transfer contents";
                WriteLog($"------------ {_currentBatchAction.ToUpper()} ------------");

                await WriteAsync(progress, false, cancel);
                while (await Reader.ReadAllAsync(skippedSubTreePaths, cancel))
                {
                    await WriteAsync(progress, false, cancel);
                }
            }
        }

        private async Task UpdateReferencesAsync(IProgress<TransferState> progress = null,
            CancellationToken cancel = default)
        {
            var taskCount = LoadTaskCount();
            if (taskCount == 0)
                return;

            _currentBatchAction = "Update references";
            WriteLog($"------------ {_currentBatchAction.ToUpper()} ------------");

            var tasks = LoadTasks();
            Reader.SetReferenceUpdateTasks(tasks, taskCount);

            while (await Reader.ReadByReferenceUpdateTasksAsync(cancel))
                await WriteAsync(progress, true, cancel);
        }

        private async Task WriteAsync(IProgress<TransferState> progress, bool updateReferences, CancellationToken cancel = default)
        {
            var readerPath = Reader.RelativePath;
            var writerPath = ContentPath.Combine(Writer.ContainerPath, _rootName, readerPath);
            var state = await Writer.WriteAsync(writerPath, Reader.Content, cancel);
            state.ReaderPath = readerPath;
            state.WriterPath = writerPath;
            Progress(ref _contentCount, state, updateReferences, progress);
        }
        private void Progress(ref int count, WriterState state, bool updateReferences, IProgress<TransferState> progress = null)
        {
            if (state.Action == WriterAction.Failed)
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

        private readonly List<string> _writtenContainers = new List<string>();
        private async Task EnsureContainerAsync(string path, CancellationToken cancel)
        {
            if (path == "/")
                return;

            if (_writtenContainers.Contains(path))
                return;

            var parentPath = ContentPath.GetParentPath(path);
            await EnsureContainerAsync(parentPath, cancel);

            var content = new InitialContent(path, ContentPath.GetName(path), path == "/Root" ? "PortalRoot" : "SystemFolder");
            await Writer.WriteAsync(path, content, cancel);
            _writtenContainers.Add(path);
        }
    }
}
