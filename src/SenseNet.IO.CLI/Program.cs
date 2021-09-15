using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using SenseNet.IO.Implementations;

namespace SenseNet.IO.CLI
{
    class Program
    {
        /*
        private static int[] _source = new int[500];
        private static ConcurrentQueue<int> _target = new(new List<int>(200));
        static async Task Produce(ITargetBlock<int> target)
        {
            for (int i = 0; i < _source.Length; ++i)
            {
                await target.SendAsync(_source[i]);
                var count = ((BufferBlock<int>)target).Count;
                Console.Write($" {count}");
            }
            target.Complete();
        }
        static async Task<int> ConsumeAsync(ISourceBlock<int> source)
        {
            try
            {
                int processed = 0;
                while (await source.OutputAvailableAsync())
                {
                    var data = await source.ReceiveAsync();
                    _target.Enqueue(data);
                    processed += 1;
                    var count = ((BufferBlock<int>) source).Count;
                    Console.Write($" {count}");
                    await Task.Delay(1);
                }

                return processed;
            }
            catch (Exception e)
            {
                throw;
            }
        }
        static async Task DataFlowDemo()
        {
            var rand = new Random();
            for (int i = 0; i < _source.Length; i++)
                _source[i] = rand.Next(100, 1000);

            var options = new DataflowBlockOptions { BoundedCapacity = 10 };
            var buffer = new BufferBlock<int>(options);

            var consumerTasks = new Task<int>[5];
            for (int i = 0; i < consumerTasks.Length; i++)
                consumerTasks[i] = ConsumeAsync(buffer);
            var producerTasks = new Task[1];
            for (int i = 0; i < producerTasks.Length; i++)
                producerTasks[i] = Produce(buffer);

            var allTasks = consumerTasks.Union(producerTasks).ToArray();

            try
            {
                await Task.WhenAll(allTasks);
            }
            catch (Exception e)
            {
                // InvalidOperationException: The source completed without providing data to receive.
                Console.WriteLine("ERROR");
            }

            var processed = -1;
            try
            {
                processed = consumerTasks.Sum(x => x.Result);
            }
            catch (Exception e)
            {
                // InvalidOperationException: The source completed without providing data to receive.
                Console.WriteLine("ERROR");
            }

            Console.WriteLine();
            Console.WriteLine($"Processed {processed:#,#}.");
            Console.WriteLine($"Source length: {_source.Length:#,#}.");
            Console.WriteLine($"Target length: {_target.Count:#,#}.");
            Console.WriteLine($"Source sum: {_source.Sum():#,#}.");
            Console.WriteLine($"Target sum: {_target.Sum():#,#}.");
        }
        */

        static async Task Main()
        {
            IContentReader reader;
            IContentWriter writer;

            reader = new RepositoryReader(Options.Create(
                new RepositoryReaderArgs { Url = "https://localhost:44362", Path = "/Root", BlockSize = 10}));
            writer = new FsWriter(Options.Create(
                new FsWriterArgs{ Path = @"D:\dev\_sn-io-test\localhost_44362_(export)" }));

            reader = new FsReader(Options.Create(
                new FsReaderArgs { Path = @"D:\dev\_sn-io-test\FsReader\Root" }));
            writer = new FsWriter(Options.Create(
                new FsWriterArgs { Path = @"D:\dev\_sn-io-test\FsWriter" }));

            reader = new FsReader(Options.Create(
                new FsReaderArgs { Path = @"D:\dev\_sn-io-test\FsReader\Root\GyebiTesztel" }));
            writer = new FsWriter(Options.Create(
                new FsWriterArgs { Path = @"D:\dev\_sn-io-test\FsWriter\Root", Name= "XXX" }));

            /* =================================================================================== TEST CASES */

            // Export Settings
            reader = new RepositoryReader(Options.Create(
                new RepositoryReaderArgs { Url = "https://localhost:44362", Path = "/Root/System/Settings", BlockSize = 10}));
            writer = new FsWriter(Options.Create(
                new FsWriterArgs { Path = @"D:\dev\_sn-io-test\localhost_44362_Settings" }));

            // Export Settings to Settings2
            reader = new RepositoryReader(Options.Create(
                new RepositoryReaderArgs { Url = "https://localhost:44362", Path = "/Root/System/Settings", BlockSize = 10}));
            writer = new FsWriter(Options.Create(
                new FsWriterArgs { Path = @"D:\dev\_sn-io-test\localhost_44362_Settings", Name = "Settings2" }));

            // Export All
            reader = new RepositoryReader(Options.Create(
                new RepositoryReaderArgs { Url = "https://localhost:44362", Path = "/Root", BlockSize = 10}));
            writer = new FsWriter(Options.Create(
                new FsWriterArgs { Path = @"D:\dev\_sn-io-test\localhost_44362" }));

            // Import Settings
            reader = new FsReader(Options.Create(
                new FsReaderArgs { Path = @"D:\dev\_sn-io-test\localhost_44362_Settings\Settings" }));
            writer = new RepositoryWriter(Options.Create(
                new RepositoryWriterArgs { Url = "https://localhost:44362", Path = "/Root/System"}));

            // Import \Root\System\Settings
            reader = new FsReader(Options.Create(
                new FsReaderArgs { Path = @"D:\dev\_sn-io-test\localhost_44362\Root\System\Settings" }));
            writer = new RepositoryWriter(Options.Create(
                new RepositoryWriterArgs { Url = "https://localhost:44362", Path = "/Root/System"}));

            // Import Settings2 to Settings
            reader = new FsReader(Options.Create(
                new FsReaderArgs { Path = @"D:\dev\_sn-io-test\localhost_44362_Settings\Settings2" }));
            writer = new RepositoryWriter(Options.Create(
                new RepositoryWriterArgs { Url = "https://localhost:44362", Path = "/Root/System", Name = "Settings"}));

            // Import All
            reader = new FsReader(Options.Create(
                new FsReaderArgs { Path = @"D:\dev\_sn-io-test\localhost_44362\Root" }));
            writer = new RepositoryWriter(Options.Create(
                new RepositoryWriterArgs { Url = "https://localhost:44362" }));

            // Copy Settings to Settings3
            reader = new FsReader(Options.Create(
                new FsReaderArgs { Path = @"D:\dev\_sn-io-test\localhost_44362_Settings\Settings" }));
            writer = new FsWriter(Options.Create(
                new FsWriterArgs { Path = @"D:\dev\_sn-io-test\localhost_44362_Settings", Name = "Settings3" }));

            // Copy All
            reader = new FsReader(Options.Create(
                new FsReaderArgs { Path = @"D:\dev\_sn-io-test\localhost_44362\Root" }));
            writer = new FsWriter(Options.Create(
                new FsWriterArgs { Path = @"D:\dev\_sn-io-test\localhost_44362_backup" }));

            /* =================================================================================== */

            _displayLevel = DisplayLevel.Verbose;
            var flow = ContentFlow.Create(reader, writer);
            var progress = new Progress<TransferState>(ShowProgress);
            await flow.TransferAsync(progress);

            await Task.Delay(1000);

            Console.WriteLine();
            Console.WriteLine("Done.");
        }

        /* ========================================================================== Display progress */

        private enum DisplayLevel { None, Progress, Errors, Verbose }

        private static DisplayLevel _displayLevel = DisplayLevel.Errors;
        private static readonly string ClearLine = new string(' ', 70) + '\r';
        private static string _lastBatchAction;

        private static void ShowProgress(TransferState state)
        {
            if (_displayLevel == DisplayLevel.None)
                return;

            // Section
            if (_displayLevel != DisplayLevel.Progress)
            {
                if (_lastBatchAction != state.CurrentBatchAction)
                {
                    _lastBatchAction = state.CurrentBatchAction;
                    Console.Write(ClearLine);
                    Console.WriteLine($"------------ {state.CurrentBatchAction.ToUpper()} ------------");
                }
            }

            // Content
            if (_displayLevel == DisplayLevel.Verbose ||
                (_displayLevel == DisplayLevel.Errors && state.State.Action == WriterAction.Failed))
            {
                Console.Write(ClearLine);
                Console.WriteLine($"{state.State.Action,-8} {state.State.WriterPath}");
            }

            // Error
            if (_displayLevel != DisplayLevel.Progress && state.State.Action == WriterAction.Failed)
            {
                foreach (var message in state.State.Messages)
                    Console.WriteLine(
                        $"         {message.Replace("The server returned an error (HttpStatus: InternalServerError): ", "")}");
            }

            // Progress
            Console.Write($"{state.CurrentBatchAction} {state.Percent,5:F1}%  " +
                          $"({state.CurrentCount}/{state.TotalCount} errors:{state.ErrorCount})                     \r");
        }
    }
}
