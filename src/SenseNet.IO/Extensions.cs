using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using SenseNet.IO;
using SenseNet.IO.Implementations;

// ReSharper disable once CheckNamespace
namespace SenseNet.Extensions.DependencyInjection
{
    public static class Extensions
    {
        public static IServiceCollection AddContentFlow<TReader, TWriter>(this IServiceCollection services)
            where TReader : class, IContentReader
            where TWriter : class, IContentWriter
        {
            services.AddSingleton<IContentReader, TReader>();
            services.AddSingleton<IContentWriter, TWriter>();
            if (typeof(TWriter).GetInterfaces().Any(t => t == typeof(ISnRepositoryWriter)))
                services.AddSingleton<IContentFlow, SemanticContentFlow>();
            else
                services.AddSingleton<IContentFlow, SimpleContentFlow>();

            return services;
        }

        // ReSharper disable once InconsistentNaming
        /// <summary>
        /// Registers the sensenet IO feature in the service collection.
        /// </summary>
        /// <remarks>
        /// To use the feature, you can either request one of the flows directly from the service
        /// collection (e.g. <see cref="IImportContentFlow"/> or <see cref="IExportContentFlow"/>), or get a factory instance
        /// (<see cref="IImportFlowFactory"/> or <see cref="IExportFlowFactory"/>) and create flow instances through it.
        /// </remarks>
        /// <param name="services">The IServiceCollection to add the feature to.</param>
        /// <param name="configureFilesystemReader">Configuration method for filesystem reader options.</param>
        /// <param name="configureRepositoryReader">Configuration method for repository reader options.</param>
        /// <param name="configureFilesystemWriter">Configuration method for filesystem writer options.</param>
        /// <param name="configureRepositoryWriter">Configuration method for repository writer options.</param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        public static IServiceCollection AddSenseNetIO(this IServiceCollection services,
            Action<FsReaderArgs> configureFilesystemReader = null, 
            Action<RepositoryReaderArgs> configureRepositoryReader = null,
            Action<FsWriterArgs> configureFilesystemWriter = null,
            Action<RepositoryWriterArgs> configureRepositoryWriter = null)
        {
            services
                .AddSenseNetClient()

                .AddTransient<IFilesystemReader, FsReader>()
                .AddTransient<IFilesystemWriter, FsWriter>()
                .AddTransient<ISnRepositoryReader, RepositoryReader>()
                .AddTransient<ISnRepositoryWriter, RepositoryWriter>()

                .AddTransient<IImportContentFlow, ImportContentFlow>()
                .AddTransient<IExportContentFlow, ExportContentFlow>()
                .AddTransient<ICopyContentFlow, CopyContentFlow>()
                .AddTransient<ISynchronizeContentFlow, SynchronizeContentFlow>()
                
                .AddSingleton<Func<Action<FsReaderArgs>, Action<RepositoryWriterArgs>, IImportContentFlow>>(
                    providers => 
                        (cr, cw) => (IImportContentFlow)providers.CreateImportFlow(cr, cw))
                .AddSingleton<Func<Action<RepositoryReaderArgs>, Action<FsWriterArgs>, IExportContentFlow>>(
                    providers =>
                        (cr, cw) => (IExportContentFlow)providers.CreateExportFlow(cr, cw))
                .AddSingleton<Func<Action<FsReaderArgs>, Action<FsWriterArgs>, ICopyContentFlow>>(
                    providers =>
                        (cr, cw) => (ICopyContentFlow)providers.CreateCopyFlow(cr, cw))
                .AddSingleton<Func<Action<RepositoryReaderArgs>, Action<RepositoryWriterArgs>, ISynchronizeContentFlow>>(
                    providers =>
                        (cr, cw) => (ISynchronizeContentFlow)providers.CreateSynchronizeFlow(cr, cw))

                .AddSingleton<IImportFlowFactory, ImportFlowFactory>()
                .AddSingleton<IExportFlowFactory, ExportFlowFactory>()
                .AddSingleton<ICopyFlowFactory, CopyFlowFactory>()
                .AddSingleton<ISynchronizeFlowFactory, SynchronizeFlowFactory>()

                .Configure<FsReaderArgs>(o => configureFilesystemReader?.Invoke(o))
                .Configure<RepositoryReaderArgs>(o => configureRepositoryReader?.Invoke(o))
                .Configure<FsWriterArgs>(o => configureFilesystemWriter?.Invoke(o))
                .Configure<RepositoryWriterArgs>(o => configureRepositoryWriter?.Invoke(o));

            return services;
        }

        /// <summary>
        /// Gets an import flow instance from the service collection and optionally configures its options.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configureReader">Configuration method for filesystem reader options.</param>
        /// <param name="configureWriter">Configuration method for repository writer options.</param>
        /// <returns>A content flow for the import scenario.</returns>
        public static IContentFlow CreateImportFlow(this IServiceProvider services, 
            Action<FsReaderArgs> configureReader = null, 
            Action<RepositoryWriterArgs> configureWriter = null)
        {
            var flow = services.GetRequiredService<IImportContentFlow>();

            configureReader?.Invoke(flow.Reader.ReaderOptions);
            configureWriter?.Invoke(flow.Writer.WriterOptions);

            return flow;
        }
        /// <summary>
        /// Gets an export flow instance from the service collection and optionally configures its options.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configureReader">Configuration method for repository reader options.</param>
        /// <param name="configureWriter">Configuration method for filesystem writer options.</param>
        /// <returns>A content flow for the export scenario.</returns>
        public static IContentFlow CreateExportFlow(this IServiceProvider services,
            Action<RepositoryReaderArgs> configureReader = null,
            Action<FsWriterArgs> configureWriter = null)
        {
            var flow = services.GetRequiredService<IExportContentFlow>();

            configureReader?.Invoke(flow.Reader.ReaderOptions);
            configureWriter?.Invoke(flow.Writer.WriterOptions);

            return flow;
        }
        /// <summary>
        /// Gets a copy flow instance from the service collection and optionally configures its options.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configureReader">Configuration method for filesystem reader options.</param>
        /// <param name="configureWriter">Configuration method for filesystem writer options.</param>
        /// <returns>A content flow for the copy scenario.</returns>
        public static IContentFlow CreateCopyFlow(this IServiceProvider services,
            Action<FsReaderArgs> configureReader = null,
            Action<FsWriterArgs> configureWriter = null)
        {
            var flow = services.GetRequiredService<ICopyContentFlow>();

            configureReader?.Invoke(flow.Reader.ReaderOptions);
            configureWriter?.Invoke(flow.Writer.WriterOptions);

            return flow;
        }
        /// <summary>
        /// Gets a synchronizer flow instance from the service collection and optionally configures its options.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configureReader">Configuration method for repository reader options.</param>
        /// <param name="configureWriter">Configuration method for repository writer options.</param>
        /// <returns>A content flow for the synchronize scenario.</returns>
        public static IContentFlow CreateSynchronizeFlow(this IServiceProvider services,
            Action<RepositoryReaderArgs> configureReader = null,
            Action<RepositoryWriterArgs> configureWriter = null)
        {
            var flow = services.GetRequiredService<ISynchronizeContentFlow>();

            configureReader?.Invoke(flow.Reader.ReaderOptions);
            configureWriter?.Invoke(flow.Writer.WriterOptions);

            return flow;
        }
    }
}
