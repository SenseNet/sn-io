using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SenseNet.IO.Implementations
{
    internal class SimpleContentFlow : ContentFlow
    {
        public SimpleContentFlow(IContentReader reader, IContentWriter writer, ILogger<ContentFlow> logger)
            : base(reader, writer, logger)
        {
        }

        private int _contentCount;
        private string _currentBatchAction;
        private int _errorCount;
        private string _rootName;
        public override async Task TransferAsync(IProgress<TransferState> progress, CancellationToken cancel = default)
        {
            await InitializeAsync().ConfigureAwait(false);

            var timer = Stopwatch.StartNew();

            _rootName = Writer.RootName ?? Reader.RootName;
            try
            {
                if (await Reader.ReadAllAsync(new string[0], cancel))
                {
                    if (Writer.RootName != null)
                        Rename(Reader.Content, _rootName);

                    _currentBatchAction = "Transfer contents";
                    WriteLog($"------------ {_currentBatchAction.ToUpper()} ------------");

                    await WriteAsync(progress, false, cancel);
                    while (await Reader.ReadAllAsync(new string[0], cancel))
                    {
                        //UNDONE: Not tested: SimpleContentFlow.TransferAsync
                        var writerState = await WriteAsync(progress, false, cancel);
                        if (writerState.Action == WriterAction.MissingParent)
                            Reader.SkipSubtree(ContentPath.GetParentPath(writerState.ReaderPath));
                    }
                }
            }
            catch (Exception e)
            {
                WriteLog(e);
                throw;
            }

            timer.Stop();
            WriteSummaryToLog(_contentCount, Reader.EstimatedCount, 0, _errorCount, timer.Elapsed);
        }

        private async Task<WriterState> WriteAsync(IProgress<TransferState> progress, bool updateReferences, CancellationToken cancel = default)
        {
            var readerPath = Reader.RelativePath;
            var writerPath = ContentPath.Combine(_rootName, readerPath);
            var state = await Writer.WriteAsync(writerPath, Reader.Content, cancel);
            state.ReaderPath = readerPath;
            state.WriterPath = writerPath;
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
    }
}
