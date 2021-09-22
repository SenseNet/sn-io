using System.Linq;
using SenseNet.IO;
using SenseNet.IO.Implementations;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection
{
    public static class Extensions
    {
        public static IServiceCollection AddContentFlow<TReader, TWriter>(this IServiceCollection services)
            where TReader : class, IContentReader
            where TWriter : class, IContentWriter
        {
            services.AddSingleton<IContentReader, TReader>();
            services.AddSingleton<IContentWriter, TWriter>();
            services.AddSingleton<IContentFlow, Level1ContentFlow>();

            return services;
        }
        public static IServiceCollection AddContentFlowForRepositoryWriter<TReader, TWriter>(this IServiceCollection services)
            where TReader : class, IContentReader
            where TWriter : class, ISnRepositoryWriter
        {
            services.AddSingleton<IContentReader, TReader>();
            services.AddSingleton<ISnRepositoryWriter, TWriter>();
            services.AddSingleton<IContentFlow, Level5ContentFlow>();

            return services;
        }
    }
}
