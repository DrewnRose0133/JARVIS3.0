using System.Threading.Tasks;
using JARVIS.Core;
using JARVIS.Devices.Interfaces;
using JARVIS.Services.Handlers;

namespace JARVIS.Services.Handlers
{
    /// <summary>
    /// Handles commands related to reporting system or environment status.
    /// </summary>
    public class StatusCommandHandler : ICommandHandler
    {
        private readonly StatusReporter _statusReporter;
        

        public StatusCommandHandler(StatusReporter statusReporter)
        {
            _statusReporter = statusReporter;
        }

        public async Task<string?> HandleAsync(string input)
        {
            var lower = input.ToLowerInvariant();

            if (lower.Contains("status") || lower.Contains("report") || lower.Contains("system check"))
            {
                var report = await _statusReporter.GetSystemStatusAsync();
                return report;
            }

            if (lower.Contains("cpu usage"))
            {
                var cpu = SystemMonitor.GetCpuUsageAsync().Result;
                var response = cpu >= 0 ? $"Current CPU usage is {cpu:F1} percent." : "Unable to retrieve CPU usage, sir.";

                return response;
            }

            if (lower.Contains("memory usage"))
            {
                var memory = SystemMonitor.GetMemoryUsage();
                var response = memory >= 0 ? $"Current memory usage is {memory:F1} percent." : "Unable to retrieve memory usage, sir.";

                return response;
            }

            if (lower.Contains("internet status") || lower.Contains("network status"))
            {
                var response = SystemMonitor.GetInternetStatusAsync().Result;
                
                return response;
            }


            return null;
        }
    }
}
