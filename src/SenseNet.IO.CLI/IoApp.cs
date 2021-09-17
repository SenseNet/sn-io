using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SenseNet.IO.CLI
{
    public class IoApp
    {
        private readonly ILogger<IoApp> _logger;
        public IContentReader Reader { get; }
        public IContentWriter Writer { get; }

        public IoApp(IContentReader reader, IContentWriter writer, ILogger<IoApp> logger)
        {
            Reader = reader;
            Writer = writer;
            _logger = logger;
        }

        public async Task RunAsync(Action<TransferState> progressCallback)
        {
            var flow = ContentFlow.Create(Reader, Writer);
            flow.WriteLogHead(this.ParamsToDisplay());

            var progress = new Progress<TransferState>(progressCallback);
            try
            {
                await flow.TransferAsync(progress);
            }
            catch (Exception e)
            {
                _logger.LogError(e, e.Message);
            }
        }
    }
}
