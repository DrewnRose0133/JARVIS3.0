// Services/SamsungTvManager.cs
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JARVIS.Config;
using JARVIS.Services;
using Microsoft.Extensions.Options;

public class SamsungTvManager : ISamsungTvService
{
    private readonly Dictionary<string, SamsungTvClient> _clients;

    public SamsungTvManager(IOptions<AppSettings> options)
    {
        _clients = options.Value.SamsungTvs
            .ToDictionary(
                tv => tv.Name,
                tv => new SamsungTvClient(
                    ipAddress: tv.IpAddress,
                    appName: options.Value.AppName
                // macAddress: tv.MacAddress   // ← now provided
                )
            );
    }

    public Task ConnectAllAsync()
        => Task.WhenAll(_clients.Values.Select(c => c.ConnectAsync()));

    public Task SendCommandAsync(string name, RemoteCommand cmd)
    {
        if (!_clients.TryGetValue(name, out var client))
            throw new KeyNotFoundException($"TV '{name}' not found");
        return client.SendCommandAsync(cmd);
    }
}
