using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
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
            //var reader = new RepositoryTreeReader("https://localhost:44362", "/Root/System/Settings", 10);
            //var reader = new RepositoryTreeReader("https://localhost:44362", "/Root/System/Schema", 10);

            var reader = new FsReader(@"D:\dev\_sn-io-test\FsReader", "/Root");
            //var reader = new FsReader(@"D:\dev\_sn-io-test\FsReader", "/Root/GyebiTesztel");
            //var reader = new FsReader(@"D:\dev\_sn-io-test\FsReader", "/Root/(apps)");
            //var reader = new FsReader(@"D:\dev\_sn-io-test\FsReader", "/Root/IMS");
            //var reader = new FsReader(@"D:\dev\_sn-io-test\FsReader", "/Root/IMS/BuiltIn/Portal/Admin");
            //var reader = new FsReader(@"D:\dev\_sn-io-test\FsReader", "/Root/System/Settings");
            //var reader = new FsReader(@"D:\dev\_sn-io-test\FsReader", "/Root/System/Schema");

            /*
            using (var writer = new StreamWriter(@"D:\dev\_sn-io-test\FsWriter\paths.txt"))
                while (await reader.ReadAsync())
                {
                    Console.WriteLine("{0,-20}: {1,-20} {2}", reader.Content.Name, reader.Content.Type, reader.RelativePath);
                    writer.WriteLine("{0,-40} {1,-20} {2}", reader.Content.Name, reader.Content.Type, reader.RelativePath);
                }
            */

            var writer = new FsWriter(@"D:\dev\_sn-io-test\FsWriter");
            //var writer = new FsWriter(@"D:\dev\_sn-io-test\FsWriter", "/Root", "XXX");

            var flow = new ContentFlow(reader, writer);
            //var progress = new Progress(state =>
            //{
            //    Console.Write("Transferring... {0,5:F1}%\r", state.Percent);
            //});
            var progress = new Progress(state =>
            {
                Console.Write("                             \r");
                Console.WriteLine(state.Path);
                Console.Write("Transferring... {0,5:F1}%\r", state.Percent);
            });
            await flow.TransferAsync(progress);
            Console.WriteLine();
            Console.WriteLine("Done.");
        }
    }

    class Progress : IProgress<(string Path, double Percent)>
    {
        private readonly Action<(string Path, double Percent)> _callback;
        public Progress(Action<(string Path, double Percent)> callback) { _callback = callback; }
        public void Report((string Path, double Percent) value) { _callback(value); }
    }
}
