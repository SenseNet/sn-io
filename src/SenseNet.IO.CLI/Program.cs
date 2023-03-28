using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SenseNet.Extensions.DependencyInjection;
using SenseNet.IO.Implementations;
using Serilog;

namespace SenseNet.IO.CLI
{
    public class Program
    {
        internal static ColoredConsoleSupport Color = new();

        private static async Task Main(string[] args)
        {
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

            _displayLevel = app.DisplaySettings.DisplayLevel;
            Console.WriteLine(app.HeadToDisplay());

            try
            {
                await app.RunAsync(ShowProgress);
                await Task.Delay(1000);
                Console.WriteLine();
                Console.WriteLine("Done.");
            }
            catch (Exception e)
            {
                using (Color.Error())
                    Console.Write($" ERROR ");
                Console.WriteLine();
                Console.WriteLine(e.GetAllMessages());
            }
        }
        public static IoApp CreateApp(string[] args, Stream settingsFile = null)
        {
            var host = CreateHost(args, settingsFile);
            var app = ActivatorUtilities.CreateInstance<IoApp>(host.Services);

            app.InitializeAsync().GetAwaiter().GetResult();

            return app;
        }
        private static IHost CreateHost(string[] args, Stream settingsFile = null)
        {
            var appArguments = new ArgumentParser().Parse(args);

            var host = Host.CreateDefaultBuilder()
                // ReSharper disable once UnusedParameter.Local
                .ConfigureAppConfiguration((hostBuilderContext, configurationBuilder) =>
                {
                    configurationBuilder.SetBasePath(Directory.GetCurrentDirectory());
                    if (settingsFile != null)
                        configurationBuilder.AddJsonStream(settingsFile);
                    else
                        configurationBuilder.AddJsonFile("providerSettings.json", true);
                    configurationBuilder
                        .AddCommandLine(args);
                })
                .ConfigureServices((hostBuilderContext, serviceCollection) =>
                {
                    serviceCollection.Configure<DisplaySettings>(hostBuilderContext.Configuration.GetSection("display"));
                    switch (appArguments.Verb)
                    {
                        case Verb.Export:
                            var exportArgs = (ExportArguments) appArguments;
                            serviceCollection
                                .AddSenseNetIO(configureRepositoryReader: readerArgs =>
                                    {
                                        hostBuilderContext.Configuration.GetSection("repositoryReader")
                                            .Bind(readerArgs);
                                        exportArgs.ReaderArgs.RewriteSettings(readerArgs);
                                    },
                                    configureFilesystemWriter: writerArgs =>
                                    {
                                        hostBuilderContext.Configuration.GetSection("fsWriter").Bind(writerArgs);
                                        exportArgs.WriterArgs.RewriteSettings(writerArgs);
                                    })
                                .AddContentFlow<RepositoryReader, FsWriter>()
                                .ConfigureSenseNetRepository("source", options =>
                                {
                                    hostBuilderContext.Configuration.GetSection("repositoryReader").Bind(options);
                                    //update from command line
                                    exportArgs.ReaderArgs.RewriteSettings(options);
                                })
                                ;
                            break;
                        case Verb.Import:
                            var importArgs = (ImportArguments) appArguments;
                            serviceCollection
                                .AddSenseNetIO(configureFilesystemReader: readerArgs =>
                                    {
                                        hostBuilderContext.Configuration.GetSection("fsReader")
                                            .Bind(readerArgs);
                                        importArgs.ReaderArgs.RewriteSettings(readerArgs);
                                    },
                                    configureRepositoryWriter: writerArgs =>
                                    {
                                        hostBuilderContext.Configuration.GetSection("repositoryWriter").Bind(writerArgs);
                                        importArgs.WriterArgs.RewriteSettings(writerArgs);
                                    })
                                .AddContentFlow<FsReader, RepositoryWriter>()
                                .ConfigureSenseNetRepository("target", options =>
                                {
                                    hostBuilderContext.Configuration.GetSection("repositoryWriter").Bind(options);
                                    //update from command line
                                    importArgs.WriterArgs.RewriteSettings(options);
                                })
                                ;
                            break;
                        case Verb.Copy:
                            var copyArgs = (CopyArguments) appArguments;
                            serviceCollection
                                .AddSenseNetIO(configureFilesystemReader: readerArgs =>
                                    {
                                        hostBuilderContext.Configuration.GetSection("fsReader")
                                            .Bind(readerArgs);
                                        copyArgs.ReaderArgs.RewriteSettings(readerArgs);
                                    },
                                    configureFilesystemWriter: writerArgs =>
                                    {
                                        hostBuilderContext.Configuration.GetSection("fsWriter").Bind(writerArgs);
                                        copyArgs.WriterArgs.RewriteSettings(writerArgs);
                                    })
                                .AddContentFlow<FsReader, FsWriter>()
                                ;
                            break;
                        case Verb.Sync:
                            var syncArgs = (SyncArguments) appArguments;
                            serviceCollection
                                .AddSenseNetIO(configureRepositoryReader: readerArgs =>
                                    {
                                        hostBuilderContext.Configuration.GetSection("repositoryReader")
                                            .Bind(readerArgs);
                                        syncArgs.ReaderArgs.RewriteSettings(readerArgs);
                                    },
                                    configureRepositoryWriter: writerArgs =>
                                    {
                                        hostBuilderContext.Configuration.GetSection("repositoryWriter").Bind(writerArgs);
                                        syncArgs.WriterArgs.RewriteSettings(writerArgs);
                                    })
                                .AddContentFlow<RepositoryReader, RepositoryWriter>()
                                .ConfigureSenseNetRepository("source", options =>
                                {
                                    hostBuilderContext.Configuration.GetSection("repositoryReader").Bind(options);
                                    //update from command line
                                    syncArgs.ReaderArgs.RewriteSettings(options);
                                })
                                .ConfigureSenseNetRepository("target", options =>
                                {
                                    hostBuilderContext.Configuration.GetSection("repositoryWriter").Bind(options);
                                    //update from command line
                                    syncArgs.WriterArgs.RewriteSettings(options);
                                })
                                ;
                            break;
                        case Verb.Transfer:
                            throw new NotSupportedException("'Transfer' is not supported in this version.");
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                })
                .ConfigureLogging((hostBuilder, loggingBuilder) =>
                {
                    loggingBuilder.ClearProviders();
                    loggingBuilder.AddSerilog(new LoggerConfiguration()
                        .ReadFrom.Configuration(hostBuilder.Configuration)
                        .CreateLogger());
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
            if (!args.Any(x => x.Equals("-HELP", SC) || x == "-?" || x == "?"))
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

        private static readonly Dictionary<string, string> AtomicArguments = new()
        {
            {"FsReader", @"    [-PATH] <Fully qualified path of the filesystem entry to read.>"},
            {"FsWriter", @"    [-PATH] <Fully qualified path of a target filesystem directory.>
    [-NAME] [Name of the target tree root if it is different from the source name.]
    [-FLATTEN] Boolean switch. If exists, all content will be written into the target directory without any folder structure."},
            {"RepositoryReader", @"    [-URL] <Url of the source sensenet repository e.g. 'https://example.sensenet.cloud'.>
    [-PATH] [Repository path of the root content of the tree to transfer. Default: '/Root'.]
    [-FILTER] [Filter in Content Query Language e.g. ""+TypeIs:File +ModificationDate:>@@Today-2@@""]
    [-BLOCKSIZE] [Count of items in one request. Default: 10.]"},
            {"RepositoryWriter", @"    [-URL] <Url of the target sensenet repository e.g. 'https://example.sensenet.cloud'.>
    [-PATH] [Repository path of the target container. Default: '/'.]
    [-NAME] [Name of the target tree root if it is different from the source name.]"},
        };

        private static readonly Dictionary<string, string> HelpArguments = new()
        {
            {"General", ""},
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

        private static readonly Dictionary<string, string> HelpHeads = new()
        {
            {"General", @$"Manages content transfer in the sensenet ecosystem.
USAGE: SnIO <VERB> [-SOURCE [Source arguments]] [-TARGET [Target arguments]]
       SnIO <VERB> [?|-?|-help]
       SnIO [?|-?|-help]

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
            {"EXPORT", @"Transfers content tree from a sensenet repository to a filesystem directory.
USAGE: SnIO EXPORT [-SOURCE [Source arguments]] [-TARGET [Target arguments]]
       SnIO EXPORT [?|-?|-help]
Arguments"},
            {"IMPORT", @"Transfers content tree from a filesystem entry to a sensenet repository.
USAGE: SnIO IMPORT [-SOURCE [Source arguments]] [-TARGET [Target arguments]]
       SnIO IMPORT [?|-?|-help]
Arguments"},
            {"COPY", @"Transfers content tree from a filesystem entry to another filesystem directory.
USAGE: SnIO COPY [-SOURCE [Source arguments]] [-TARGET [Target arguments]]
       SnIO COPY [?|-?|-help]
Arguments"},
            {"SYNC", @"Transfers content tree from a sensenet repository to another sensenet repository.
USAGE: SnIO SYNC [-SOURCE [Source arguments]] [-TARGET [Target arguments]]
       SnIO SYNC [?|-?|-help]
Arguments"},
        };

        #endregion

        /* ========================================================================== Display progress */

        private static DisplayLevel _displayLevel;
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
