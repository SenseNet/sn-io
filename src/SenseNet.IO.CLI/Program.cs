using System;
using System.Threading.Tasks;
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
            //var reader = new RepositoryTreeReader("https://localhost:44362", "/Root", 10);
            //var reader = new RepositoryTreeReader("https://localhost:44362", "/Root/(apps)", 10);
            //var reader = new RepositoryTreeReader("https://localhost:44362", "/Root/IMS", 10);
            //var reader = new RepositoryTreeReader("https://localhost:44362", "/Root/IMS/BuiltIn/Portal/Admin", 10);
            //var reader = new RepositoryTreeReader("https://localhost:44362", "/Root/System", 10);
            //var reader = new RepositoryTreeReader("https://localhost:44362", "/Root/System/Settings", 10);
            //var reader = new RepositoryTreeReader("https://localhost:44362", "/Root/System/Schema", 10);

            //var reader = new FsReader(@"D:\dev\_sn-io-test\FsReader", "/Root/(apps)");
            //var reader = new FsReader(@"D:\dev\_sn-io-test\FsReader", "/Root/IMS");
            //var reader = new FsReader(@"D:\dev\_sn-io-test\FsReader", "/Root/IMS/BuiltIn/Portal/Admin");
            //var reader = new FsReader(@"D:\dev\_sn-io-test\FsReader", "/Root/System/Settings");
            //var reader = new FsReader(@"D:\dev\_sn-io-test\FsReader", "/Root/System/Schema");

            /* =================================================================================== TEST CASES */

            //var reader = new RepositoryTreeReader("https://localhost:44362", "/Root", 10);
            //var writer = new FsWriter(@"D:\dev\_sn-io-test\localhost_44362_(export)");

            //var reader = new FsReader(@"D:\dev\_sn-io-test\FsReader", "/Root");
            //var writer = new FsWriter(@"D:\dev\_sn-io-test\FsWriter");

            //var reader = new FsReader(@"D:\dev\_sn-io-test\FsReader", "/Root/GyebiTesztel");
            //var writer = new FsWriter(@"D:\dev\_sn-io-test\FsWriter", "/Root", "XXX");

            //var reader = new FsReader(@"D:\dev\_sn-io-test\FsReader", "/Root");
            //var writer = new RepositoryWriter("https://localhost:44362");

            //var reader = new FsReader(@"D:\dev\_sn-io-test\FsReader", "/Root/(apps)");
            //var writer = new RepositoryWriter("https://localhost:44362", "/Root");

            //var reader = new FsReader(@"D:\dev\_sn-io-test\FsReader", "/Root/IMS");
            //var writer = new RepositoryWriter("https://localhost:44362", "/Root");

            var reader = new FsReader(@"D:\dev\_sn-io-test\FsReader", "/Root/System/Settings");
            var writer = new RepositoryWriter("https://localhost:44362", "/Root/System");




            _displayLevel = DisplayLevel.Verbose;
            var flow = new ContentFlow(reader, writer);
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
