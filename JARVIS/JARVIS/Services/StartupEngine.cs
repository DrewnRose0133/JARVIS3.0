// StartupEngine.cs
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

public class StartupEngine : BackgroundService
{
    private readonly VisualizerSocketServer _socket;

    public StartupEngine(VisualizerSocketServer socket) => _socket = socket;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _socket.Start();      // kicks off your WebSocket server
        return Task.CompletedTask;     // keep the host alive
    }
}
