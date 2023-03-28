using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SenseNet.Extensions.DependencyInjection;
using SenseNet.IO;
using Serilog;

public class Program
{
    private static IHost _host;

    private static async Task Main(string[] args)
    {
        _host = CreateHost();

        // SIMPLE IO =========================================================================
        // Get flows by the global configuration
        // ===================================================================================

        // Use this method when you have a static global configuration and do not need or want
        // to set import/export targets or sources dynamically.
        // (although it is possible to set config values dynamically here too)

        //var importFlow = _host.Services.GetRequiredService<IImportContentFlow>();
        //await importFlow.TransferAsync(new Progress<TransferState>());

        //var exportFlow = _host.Services.GetRequiredService<IExportContentFlow>();
        //await exportFlow.TransferAsync(new Progress<TransferState>());

        //var copyFlow = _host.Services.GetRequiredService<ICopyContentFlow>();
        //await copyFlow.TransferAsync(new Progress<TransferState>());

        //var synchFlow = _host.Services.GetRequiredService<ISynchronizeContentFlow>();
        //await synchFlow.TransferAsync(new Progress<TransferState>());

        // ADVANCED IO =======================================================================
        // Create flows dynamically
        // ===================================================================================

        // Use this method when you want to start multiple export/import
        // processes in parallel.

        var sw = Stopwatch.StartNew();
        
        await ImportAsync();
        //await ExportAsync();

        sw.Stop();

        Console.WriteLine();
        Console.WriteLine("==============================================");
        Console.WriteLine($"Elapsed time: {sw.Elapsed}");
        Console.WriteLine("==============================================");
    }

    private static IHost CreateHost()
    {
        var host = Host.CreateDefaultBuilder()
            .UseSerilog((context, configuration) =>
            {
                configuration.ReadFrom.Configuration(context.Configuration);
            })
            .ConfigureServices((context, services) =>
            {
                // Configure the necessary reader and writer only. We configure them
                // all here for demo purposes.

                services
                    .AddSenseNetIO(fsReaderArgs =>
                        {
                            context.Configuration.GetSection("sensenet:fssource").Bind(fsReaderArgs);
                        },
                        repoReaderArgs =>
                        {
                            context.Configuration.GetSection("sensenet:reposource").Bind(repoReaderArgs);
                        },
                        fsWriterArgs =>
                        {
                            context.Configuration.GetSection("sensenet:fstarget").Bind(fsWriterArgs);
                        },
                        repoWriterArgs =>
                        {
                            context.Configuration.GetSection("sensenet:repotarget").Bind(repoWriterArgs);
                        });
            }).Build();

        return host;
    }

    private static async Task ImportAsync()
    {
        var count = 1;
        var states = new TransferState[count];

        var flowFactory = _host.Services.GetRequiredService<IImportFlowFactory>();
        var logger = _host.Services.GetRequiredService<ILogger<Program>>();
        var timer = Stopwatch.StartNew();

        // start multiple tasks in parallel if necessary
        var tasks = Enumerable.Range(1, count).Select(i =>
        {
            return Task.Run(async () =>
            {
                var flow = flowFactory.Create(configureWriter: writerArgs =>
                {
                    // provide a unique name for the target repo folder
                    writerArgs.Name = $"ex{i}";
                });

                await flow.TransferAsync(new Progress<TransferState>(state =>
                {
                    // log state if necessary
                    var index = i-1;
                    states[index] = state;
                }));
            });
        }).ToList();

        // complete all tasks
        await Task.WhenAll(tasks);

        var elapsed = timer.Elapsed;
        var contentCount = states.Sum(s => s.CurrentCount);
        logger.LogInformation($"IMPORT FINISHED: " +
                              $"parallelism: {count}, " +
                              $"imported content: {contentCount}, " +
                              $"errors: {states.Sum(s => s.ErrorCount)}, " +
                              $"duration: {elapsed}");
        logger.LogInformation($"IMPORT SPEED: {contentCount / elapsed.TotalSeconds} CPS.");
    }

    private static async Task ExportAsync()
    {
        var flowFactory = _host.Services.GetRequiredService<IExportFlowFactory>();

        // start multiple tasks in parallel if necessary
        var tasks = Enumerable.Range(1, 5).Select(i =>
        {
            return Task.Run(async () =>
            {
                var flow = flowFactory.Create(readerArgs =>
                    {
                        // export from different folders
                        readerArgs.Path += i.ToString();
                    });

                await flow.TransferAsync(new Progress<TransferState>(state =>
                {
                    // log state if necessary
                }));
            });
        }).ToList();

        // complete all tasks
        await Task.WhenAll(tasks);
    }
}