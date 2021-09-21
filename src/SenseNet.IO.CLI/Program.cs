using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SenseNet.IO.Implementations;

namespace SenseNet.IO.CLI
{
    public class Program
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

        /*
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

            // =================================================================================== TEST CASES

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

            // ===================================================================================

            _displayLevel = DisplayLevel.Verbose;
            var flow = ContentFlow.Create(reader, writer);
            var progress = new Progress<TransferState>(ShowProgress);
            await flow.TransferAsync(progress);

            await Task.Delay(1000);

            Console.WriteLine();
            Console.WriteLine("Done.");
        }
        */














        //UNDONE =======================================================================================
        //UNDONE =======================================================================================

        private class RepositoryWriterForRepositoryReaderTests : RepositoryWriter, ISnRepositoryWriter
        {
            private Dictionary<string, WriterState> _states = new Dictionary<string, WriterState>
            {
                {"/Root/System/Settings/Logging.settings", new WriterState {BrokenReferences = new string[0], RetryPermissions = true}},
                {"/Root/IMS/Public", new WriterState {BrokenReferences = new[] {"ModifiedBy", "CreatedBy"}, RetryPermissions = true}},
                {"/Root/Content", new WriterState {BrokenReferences = new[] {"Owner"}, RetryPermissions = false}},
            };

            public RepositoryWriterForRepositoryReaderTests(IOptions<RepositoryWriterArgs> args) : base (args) { }

            public override async Task<WriterState> WriteAsync(string path, IContent content, CancellationToken cancel = default)
            {
                await Task.Delay(10);

                if (_states.TryGetValue(path, out var found))
                {
                    _states.Remove(path);
                    found.Messages = new string[0];
                    found.Action = WriterAction.Updating;
                    return found;
                }

                return new WriterState
                {
                    WriterPath = path,
                    RetryPermissions = false,
                    BrokenReferences = new string[0],
                    Messages = new string[0],
                    Action = WriterAction.Updated
                };
            }
        }

        //UNDONE =======================================================================================
        //UNDONE =======================================================================================



















        internal static ColoredConsoleSupport Color = new ColoredConsoleSupport();

        private static async Task Main(string[] args)
        {
            //args = new[] { "COPY", "-SOURCE", @"D:\_sn-io-test\source", "-TARGET", @"D:\_sn-io-test", "target" };
            //args = new[] { "EXPORT", "-SOURCE", "https://localhost1", "\"/Root/Content\"", "-TARGET", @"D:\_sn-io-test", "old-contents" };
            //args = new[] { "EXPORT", "-SOURCE", "-PATH", "\"/Root/Content\"", "-TARGET", @"D:\_sn-io-test", "old-contents" };
            //args = new[] { "IMPORT", "-SOURCE", @"D:\_sn-io-test\old-contents", "-TARGET", "https://localhost1" };

            //args = new[] { "?" };
            //args = new[] { "-help" };
            //args = new[] { "export", "-help" };
            //args = new[] { "import", "-help" };
            //args = new[] { "copy", "-help" };
            //args = new[] { "sync", "-help" };
            //args = new string[0];
            //args = new[] { "fake" };
            //args = new[] { "EXPORT" };
            //args = new[] { "COPY", "-TARGET", @"D:\_sn-io-test\localhost_44362_backup" };
            //args = new[] { "COPY", "-SOURCE", @"D:\_sn-io-test\localhost_44362\Root\System\Settings", 
            //                       "-TARGET", @"D:\_sn-io-test\localhost_44362_backup", "Settings_backup" };
            //args = new[] { "IMPORT" };
            args = new[] { "IMPORT", "-SOURCE", @"D:\_sn-io-test\localhost_44362_backup\Settings_backup",
                          "-TARGET", "-PATH", "/Root/System", "-NAME", "Settings"};

            //args = new[] { "SYNC" };
            
            if (IsHelpRequested(args))
                return;

            IoApp app;
            try
            {
                app = CreateApp(args);
            }
            catch (Exception e)
            {
                Console.WriteLine("Cannot create the application.");
                Console.WriteLine(e.Message);
                return;
            }

            Console.WriteLine(app.ParamsToDisplay());
            //UNDONE:LOG: Write 'app.ParamsToDisplay()' to log after the final logger integration.
            await app.RunAsync(ShowProgress);

            await Task.Delay(1000);

            Console.WriteLine();
            Console.WriteLine("Done.");
        }

        public static IoApp CreateApp(string[] args, Stream settingsFile = null)
        {
            var host = CreateHost(args, settingsFile);
            var app = ActivatorUtilities.CreateInstance<IoApp>(host.Services);
            return app;
        }

        private static IHost CreateHost(string[] args, Stream settingsFile = null)
        {
            var appArguments = new ArgumentParser().Parse(args);

            var host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((hostBuilderContext, configurationBuilder) =>
                {
                    configurationBuilder.SetBasePath(Directory.GetCurrentDirectory());
                    if (settingsFile != null)
                        configurationBuilder.AddJsonStream(settingsFile);
                    else
                        configurationBuilder.AddJsonFile("appsettings(test).json");
                    configurationBuilder
                        .AddEnvironmentVariables()
                        .AddCommandLine(args);
                })
                .ConfigureServices((hostBuilderContext, serviceCollection) =>
                {
                    switch (appArguments.Verb)
                    {
                        case Verb.Export:
                            var exportArgs = (ExportArguments) appArguments;
                            serviceCollection
                                .AddSingleton<IContentReader, RepositoryReader>()
                                .AddSingleton<IContentWriter, FsWriter>()
                                // settings file
                                .Configure<RepositoryReaderArgs>(
                                    hostBuilderContext.Configuration.GetSection("repositoryReader"))
                                .Configure<FsWriterArgs>(hostBuilderContext.Configuration.GetSection("fsWriter"))
                                // rewrite settings
                                .Configure<RepositoryReaderArgs>(settings =>
                                    exportArgs.ReaderArgs.RewriteSettings(settings))
                                .Configure<FsWriterArgs>(settings => exportArgs.WriterArgs.RewriteSettings(settings))
                                ;
                            break;
                        case Verb.Import:
                            var importArgs = (ImportArguments) appArguments;
                            serviceCollection
                                .AddSingleton<IContentReader, FsReader>()
                                .AddSingleton<IContentWriter, RepositoryWriter>()
                                // settings file
                                .Configure<FsReaderArgs>(hostBuilderContext.Configuration.GetSection("fsReader"))
                                .Configure<RepositoryWriterArgs>(
                                    hostBuilderContext.Configuration.GetSection("repositoryWriter"))
                                // rewrite settings
                                .Configure<FsReaderArgs>(settings => importArgs.ReaderArgs.RewriteSettings(settings))
                                .Configure<RepositoryWriterArgs>(settings =>
                                    importArgs.WriterArgs.RewriteSettings(settings))
                                ;
                            break;
                        case Verb.Copy:
                            var copyArgs = (CopyArguments) appArguments;
                            serviceCollection
                                .AddSingleton<IContentReader, FsReader>()
                                .AddSingleton<IContentWriter, FsWriter>()
                                // settings file
                                .Configure<FsReaderArgs>(hostBuilderContext.Configuration.GetSection("fsReader"))
                                .Configure<FsWriterArgs>(hostBuilderContext.Configuration.GetSection("fsWriter"))
                                // rewrite settings
                                .Configure<FsReaderArgs>(settings => copyArgs.ReaderArgs.RewriteSettings(settings))
                                .Configure<FsWriterArgs>(settings => copyArgs.WriterArgs.RewriteSettings(settings))
                                ;
                            break;
                        case Verb.Sync:
                            var syncArgs = (SyncArguments) appArguments;
                            serviceCollection
                                .AddSingleton<IContentReader, RepositoryReader>()
.AddSingleton<IContentWriter, RepositoryWriterForRepositoryReaderTests /*RepositoryWriter*/>() //UNDONE:!!!!!!!!!!! Write back
                                // settings file
                                .Configure<RepositoryReaderArgs>(
                                    hostBuilderContext.Configuration.GetSection("repositoryReader"))
                                .Configure<RepositoryWriterArgs>(
                                    hostBuilderContext.Configuration.GetSection("repositoryWriter"))
                                // rewrite settings
                                .Configure<RepositoryReaderArgs>(settings =>
                                    syncArgs.ReaderArgs.RewriteSettings(settings))
                                .Configure<RepositoryWriterArgs>(settings =>
                                    syncArgs.WriterArgs.RewriteSettings(settings))
                                ;
                            break;
                        case Verb.Transfer:
                            throw new NotSupportedException("'Transfer' is not supported in this version.");
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                })
                .ConfigureLogging(loggingBuilder =>
                {
                    loggingBuilder
                        .ClearProviders()
                        .AddConsole()
                        .SetMinimumLevel(LogLevel.Information);
                })
                .Build();

            return host;
        }


        #region HelpScreen

        private static StringComparison SC = StringComparison.OrdinalIgnoreCase;

        private static bool IsHelpRequested(string[] args)
        {
            if (args.Length == 0)
                return false;
            var hasVerb = args[0].Equals("EXPORT", SC) ||
                          args[0].Equals("IMPORT", SC) ||
                          args[0].Equals("COPY", SC) ||
                          args[0].Equals("SYNC", SC);
            if (!args.Any(x => x.Equals("-HELP", SC) || x == "?"))
                return false;

            WriteHelpScreen(hasVerb ? args[0].ToUpper() : null);
            return true;
        }

        private static void WriteHelpScreen(string verb = null)
        {
            Console.WriteLine(@$"SnIO {verb} v{Assembly.GetExecutingAssembly().GetName().Version}");
            var key = verb ?? "General";
            Console.WriteLine(HelpHeads[key]);
            Console.WriteLine(HelpArguments[key]);
        }

        private static Dictionary<string, string> AtomicArguments = new Dictionary<string, string>
        {
            {"FsReader", @"    [-PATH] <Fully qualified path of the filesystem entry to read.>"},
            {"FsWriter", @"    [-PATH] <Fully qualified path of a target filesystem directory.>
    [-NAME] [Name of the target tree root if it is different from the source name.]"},
            {"RepositoryReader", @"    [-URL] <Url of the source sensenet repository e.g. 'https:example.sensenet.cloud'.>
    [-PATH] [Repository path of the root content of the tree to transfer. Default: '/Root'.]
    [-BLOCKSIZE] [Count of items in one request. Default: 10.]"},
            {"RepositoryWriter", @"    [-URL] <Url of the target sensenet repository e.g. 'https:example.sensenet.cloud'.>
    [-PATH] [Repository path of the target container. Default: '/'.]
    [-NAME] [Name of the target tree root if it is different from the source name.]"},
        };

        private static Dictionary<string, string> HelpArguments = new Dictionary<string, string>
        {
            {"General", @$""},
            {"EXPORT", $@"  -SOURCE
{AtomicArguments["RepositoryReader"]}
  -TARGET
{AtomicArguments["FsWriter"]}
"},
            {"IMPORT", $@"  -SOURCE
{AtomicArguments["FsReader"]}
  -TARGET
{AtomicArguments["RepositoryWriter"]}
"},
            {"COPY", $@"  -SOURCE
{AtomicArguments["FsReader"]}
  -TARGET
{AtomicArguments["FsWriter"]}
"},
            {"SYNC", $@"  -SOURCE
{AtomicArguments["RepositoryReader"]}
  -TARGET
{AtomicArguments["RepositoryWriter"]}
"},
        };

        private static Dictionary<string, string> HelpHeads = new Dictionary<string, string>
        {
            {"General", @$"Manages content transfer in the sensenet ecosystem.
USAGE: SnIO <VERB> [-SOURCE [Source arguments]] [-TARGET [Target arguments]]
       SnIO <VERB> [?|-help]
       SnIO [?|-help]

VERBS and operations
  EXPORT  Transfer from a sensenet repository to a filesystem directory.
  IMPORT  Transfer from a filesystem entry to a sensenet repository.
  COPY    Transfer from a filesystem entry to another filesystem directory.
  SYNC    Transfer from a sensenet repository to another sensenet repository.

EXPORT arguments
{HelpArguments["EXPORT"]}
IMPORT arguments
{HelpArguments["IMPORT"]}
COPY arguments
{HelpArguments["COPY"]}
SYNC arguments
{HelpArguments["SYNC"]}
"},
            {"EXPORT", @$"Transfers content tree from a sensenet repository to a filesystem directory.
USAGE: SnIO EXPORT [-SOURCE [Source arguments]] [-TARGET [Target arguments]]
       SnIO EXPORT [?|-help]
Arguments"},
            {"IMPORT", @$"Transfers content tree from a filesystem entry to a sensenet repository.
USAGE: SnIO IMPORT [-SOURCE [Source arguments]] [-TARGET [Target arguments]]
       SnIO IMPORT [?|-help]
Arguments"},
            {"COPY", @$"Transfers content tree from a filesystem entry to another filesystem directory.
USAGE: SnIO COPY [-SOURCE [Source arguments]] [-TARGET [Target arguments]]
       SnIO COPY [?|-help]
Arguments"},
            {"SYNC", @$"Transfers content tree from a sensenet repository to another sensenet repository.
USAGE: SnIO SYNC [-SOURCE [Source arguments]] [-TARGET [Target arguments]]
       SnIO SYNC [?|-help]
Arguments"},
        };

        #endregion

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
                    using(Color.Highlight())
                        Console.WriteLine($"------------ {state.CurrentBatchAction.ToUpper()} ------------");
                }
            }

            // Content
            if (_displayLevel == DisplayLevel.Verbose ||
                (_displayLevel == DisplayLevel.Errors && state.State.Action == WriterAction.Failed))
            {
                Console.Write(ClearLine);
                if (state.State.Action == WriterAction.Failed)
                {
                    using(Color.Error())
                        Console.Write($" {state.State.Action} ");
                    Console.WriteLine($" {state.State.WriterPath}");
                }
                else
                {
                    if (state.State.Action == WriterAction.Creating || state.State.Action == WriterAction.Updating)
                    {
                        using (Color.Warning())
                            Console.Write($"{state.State.Action}");
                        Console.WriteLine($" {state.State.WriterPath}");
                    }
                    else
                    {
                        Console.WriteLine($"{state.State.Action,-8} {state.State.WriterPath}");
                    }
                }
            }

            // Error
            if (_displayLevel != DisplayLevel.Progress && state.State.Action == WriterAction.Failed)
            {
                using (Color.Highlight())
                {
                    foreach (var message in state.State.Messages)
                        Console.WriteLine(
                            $"         {message.Replace("The server returned an error (HttpStatus: InternalServerError): ", "")}");
                }
            }

            // Progress
            Console.Write($"{state.CurrentBatchAction} {state.Percent,5:F1}%  " +
                          $"({state.CurrentCount}/({state.ContentCount}+{state.UpdateTaskCount}), " +
                          $"errors:{state.ErrorCount})                     \r");
        }


    }
}
