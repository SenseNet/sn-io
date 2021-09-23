using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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

        public async Task RunAsync(Action<TransferState> progressCallback)
        {
            _logger.LogInformation(this.ParamsToDisplay());

            var progress = new Progress<TransferState>(progressCallback);
            try
            {
                await _contentFlow.TransferAsync(progress);
            }
            catch (Exception e)
            {
                _logger.LogError(e, e.Message);
            }
        }
    }
}
