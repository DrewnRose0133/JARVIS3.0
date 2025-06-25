using JARVIS.Devices.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class SamsungTvStartupHostedService : IHostedService
{
    private readonly ISamsungTVService _tv;
    private readonly ILogger<SamsungTvStartupHostedService> _log;

    public SamsungTvStartupHostedService(
        ISamsungTVService tv,
        ILogger<SamsungTvStartupHostedService> log)
    {
        _tv = tv;
        _log = log;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _log.LogInformation("Connecting to Samsung TV…");

        try
        {
            // 1) Pair & connect
            await _tv.ConnectAsync();
            var apps = await _tv.GetInstalledAppsAsync();
            foreach (var app in apps)
                _log.LogInformation(" ▸ {Name} ({AppId})", app.Name, app.AppId);
            await _tv.DisconnectAsync();

        }
        catch (System.Exception ex)
        {
            _log.LogError(ex, "Failed to talk to Samsung TV.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
