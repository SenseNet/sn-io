using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace SenseNet.IO.Implementations
{
    internal class Level1ContentFlow : ContentFlow
    {
        public override IContentReader Reader { get; }
        public override IContentWriter Writer { get; }
        public Level1ContentFlow(IContentReader reader, IContentWriter writer)
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
                        await WriteAsync(progress, false, cancel);
                    }
                }
            }
            catch (Exception e)
            {
                WriteLog(e);
                throw;
            }

            timer.Stop();
            WriteSummaryToLog(Reader.EstimatedCount, _contentCount, _errorCount, timer.Elapsed);
        }

        private async Task WriteAsync(IProgress<TransferState> progress, bool updateReferences, CancellationToken cancel = default)
        {
            var readerPath = Reader.RelativePath;
            var writerPath = ContentPath.Combine(_rootName, readerPath);
            var state = await Writer.WriteAsync(writerPath, Reader.Content, cancel);
            state.ReaderPath = readerPath;
            state.WriterPath = writerPath;
            Progress(ref _contentCount, state, updateReferences, progress);
        }
        private void Progress(ref int count, WriterState state, bool updateReferences, IProgress<TransferState> progress = null)
        {
            if(state.Action == WriterAction.Failed)
                _errorCount++;

            WriteLogAndTask(state, updateReferences);

            progress?.Report(new TransferState
            {
                CurrentBatchAction = _currentBatchAction,
                CurrentCount = ++count,
                ContentCount = Reader.EstimatedCount,
                UpdateTaskCount = _referenceUpdateTasksTotalCount,
                ErrorCount = _errorCount,
                UpdatingReferences = updateReferences,
                State = state,
            });
        }
    }
}
