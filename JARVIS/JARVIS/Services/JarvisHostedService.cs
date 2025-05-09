using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JARVIS.Services
{
    public class JarvisHostedService : IHostedService
    {
        private readonly ILogger<JarvisHostedService> _logger;

        public JarvisHostedService(ILogger<JarvisHostedService> logger)
        {
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("JARVIS is starting up.");
            await Task.Delay(100); // Replace with init logic
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("JARVIS is shutting down.");
            return Task.CompletedTask;
        }
    }
}
