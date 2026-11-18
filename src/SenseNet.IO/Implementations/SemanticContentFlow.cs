using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SenseNet.IO.Implementations
{
    internal class SemanticContentFlow : ContentFlow
    {
        public SemanticContentFlow(IContentReader reader, IContentWriter writer, ILogger<ContentFlow> logger)
            : base(reader, writer, logger)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));

            if (writer is not ISnRepositoryWriter)
                throw new ArgumentException($"Type mismatch: the writer is {writer.GetType().FullName} but should be ISnRepositoryWriter");
        }

        private int _contentCount;
        private int _referenceUpdatesCount;
        private string _currentBatchAction;
        private int _errorCount;
        private string _rootName;
        private readonly ContentPathMap _paths = new();
        private readonly Dictionary<string, TransferTask> _referenceTasks = new(StringComparer.OrdinalIgnoreCase);
        public override async Task TransferAsync(IProgress<TransferState> progress, CancellationToken cancel = default)
        {
            await InitializeAsync().ConfigureAwait(false);

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
            WriteSummaryToLog(_contentCount, Reader.EstimatedCount, _referenceUpdatesCount, _errorCount, timer.Elapsed);
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

                var writerState = await WriteAsync(progress, false, cancel);
                if (writerState.Action == WriterAction.MissingParent)
                    return;

                while (await Reader.ReadAllAsync(skippedSubTreePaths, cancel))
                {
                    writerState = await WriteAsync(progress, false, cancel);
                    if (writerState.Action == WriterAction.Failed)
                    {
                        if (await Writer.ShouldSkipSubtreeAsync(writerState.WriterPath, cancel))
                        {
                            Reader.SkipSubtree(writerState.ReaderPath);
                            WriteLog($"Skip subtree: reader: {Reader.Content.Path}");
                            WriteLog($"Skip subtree: writer: {writerState.WriterPath}");
                        }
                    }
                }
            }
        }

        private async Task UpdateReferencesAsync(IProgress<TransferState> progress = null,
            CancellationToken cancel = default)
        {
            _referenceUpdatesCount = LoadTaskCount();
            if (_referenceUpdatesCount == 0)
                return;

            _currentBatchAction = "Update references";
            WriteLog($"------------ {_currentBatchAction.ToUpper()} ------------");

            var tasks = LoadTasks().ToArray();
            foreach (var task in tasks)
            {
                _referenceTasks[task.ReaderPath] = task;
                _paths.Record(ContentPath.Combine(Writer.ContainerPath, _rootName, task.ReaderPath), task.WriterPath);
            }
            Reader.SetReferenceUpdateTasks(tasks, _referenceUpdatesCount);

            while (await Reader.ReadByReferenceUpdateTasksAsync(cancel))
                await WriteAsync(progress, true, cancel);
        }

        private async Task<WriterState> WriteAsync(IProgress<TransferState> progress, bool updateReferences, CancellationToken cancel = default)
        {
            var readerPath = Reader.RelativePath;
            var requestedPath = ContentPath.Combine(Writer.ContainerPath, _rootName, readerPath);
            var writerPath = _paths.Resolve(requestedPath);
            _referenceTasks.TryGetValue(readerPath, out var task);
            var name = _paths.TryGetExact(requestedPath, out var actualPath)
                ? ContentPath.GetName(actualPath) : Reader.Content.Name;
            var content = new ImportContent(Reader.Content, name, _paths,
                updateReferences ? task?.BrokenReferences : null,
                updateReferences && task?.RetryPermissions == true, updateReferences);
            var state = Reader.Content.CutOff
                ? new WriterState {Action = WriterAction.CutOff, WriterPath = writerPath/*, Messages = new[] {"--cutoff--"}*/}
                : await Writer.WriteAsync(writerPath, content, cancel);
            state.ReaderPath = readerPath;
            if (string.IsNullOrEmpty(state.WriterPath))
                state.WriterPath = writerPath;
            if (!Reader.Content.CutOff)
                _paths.Record(requestedPath, state.WriterPath);
            Progress(ref _contentCount, state, updateReferences, progress);
            return state;
        }
        private void Progress(ref int count, WriterState state, bool updateReferences, IProgress<TransferState> progress = null)
        {
            if (state.Action == WriterAction.MissingParent || state.Action == WriterAction.Failed)
                _errorCount++;

            WriteLogAndTask(state, updateReferences);

            progress?.Report(new TransferState
            {
                CurrentBatchAction = _currentBatchAction,
                CurrentCount = ++count,
                ContentCount = Reader.EstimatedCount,
                UpdateTaskCount = ReferenceUpdateTasksTotalCount,
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
