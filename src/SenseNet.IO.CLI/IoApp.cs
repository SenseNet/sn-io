using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SenseNet.IO.Implementations;

namespace SenseNet.IO.CLI
{
    public class IoApp
    {
        private readonly ILogger<IoApp> _logger;
        private readonly IContentFlow _contentFlow;
        public DisplaySettings DisplaySettings { get; }
        public IContentReader Reader { get; }
        public IContentWriter Writer { get; }

        public IoApp(IContentFlow contentFlow, ILogger<IoApp> logger, IOptions<DisplaySettings> displaySettings)
        {
            Reader = contentFlow.Reader;
            Writer = contentFlow.Writer;
            _logger = logger;
            _contentFlow = contentFlow;
            DisplaySettings = displaySettings.Value;
        }

        public Task InitializeAsync()
        {
            return _contentFlow != null ? _contentFlow.InitializeAsync() : Task.CompletedTask;
        }

       public async Task RunAsync(Action<TransferState> progressCallback)
        {
            _logger.LogTrace("================================================== SnIO transfer session");
            _logger.LogInformation(this.HeadToLog());

            await _contentFlow.InitializeAsync().ConfigureAwait(false);

            var progress = new Progress<TransferState>(progressCallback);
            try
            {
                await _contentFlow.TransferAsync(progress);
            }
            catch (Exception e)
            {
                _logger.LogError(e, e.Message);
                throw;
            }
        }

#if DEBUG
        private const string CompileMode = "DEBUG";
#elif RELEASE
        private const string CompileMode = "RELEASE";
#endif

        public string HeadToDisplay()
        {
            return $"SnIO {GetVerb().ToString().ToUpper()} v{GetVersion()} {CompileMode}\r\n" +
                   $"  SOURCE: {GetProviderName(Reader)} ({Reader.ParamsToDisplay()}),\r\n" +
                   $"  TARGET: {GetProviderName(Writer)} ({Writer.ParamsToDisplay()})";
        }
        public string HeadToLog()
        {
            return $"SnIO {GetVerb().ToString().ToUpper()} v{GetVersion()} {CompileMode}: " +
                   $"SOURCE: {GetProviderName(Reader)} ({Reader.ParamsToDisplay()}), " +
                   $"TARGET: {GetProviderName(Writer)} ({Writer.ParamsToDisplay()})";
        }

        private string GetProviderName(object provider)
        {
            var type = provider.GetType();
            if (type.Namespace == typeof(RepositoryWriter).Namespace)
                return type.Name;
            return type.FullName;
        }

        private Verb GetVerb()
        {
            if (Reader is RepositoryReader && Writer is FsWriter)
                return Verb.Export;
            if (Reader is FsReader && Writer is RepositoryWriter)
                return Verb.Import;
            if (Reader is FsReader && Writer is FsWriter)
                return Verb.Copy;
            if (Reader is RepositoryReader && Writer is RepositoryWriter)
                return Verb.Sync;
            return Verb.Transfer;
        }

        public string GetVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version?.ToString();
        }
    }
}
