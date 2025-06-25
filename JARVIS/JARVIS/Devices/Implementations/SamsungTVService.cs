using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using JARVIS.Devices.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using JARVIS.Devices.CommandHandlers;

namespace JARVIS.Devices.Implementations
{
    /// <summary>
    /// Implements ISamsungTVService to control a Samsung TV over WebSocket.
    /// </summary>
    public class SamsungTVService : ISamsungTVService, IAsyncDisposable
    {
        private readonly SamsungTvOptions _opts;
        private readonly ILogger<SamsungTVService> _logger;
        private ClientWebSocket? _socket;
        private string? _token;
        private bool _connected;

        public SamsungTVService(IOptions<SamsungTvOptions> opts, ILogger<SamsungTVService> logger)
        {
            _opts = opts.Value;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task ConnectAsync()
        {
            if (_connected && _socket?.State == WebSocketState.Open)
                return;

            _socket = new ClientWebSocket();
            if (_opts.UseSsl)
                _socket.Options.RemoteCertificateValidationCallback = (_, __, ___, ____) => true;
            _socket.Options.SetRequestHeader("Origin", "http://localhost");

            var scheme = _opts.UseSsl ? "wss" : "ws";
            var nameB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(_opts.RemoteName));
            var uriBuilder = new StringBuilder($"{scheme}://{_opts.IpAddress}:{_opts.Port}/api/v2/channels/samsung.remote.control?name={Uri.EscapeDataString(nameB64)}");
            if (!string.IsNullOrEmpty(_token))
                uriBuilder.Append($"&token={Uri.EscapeDataString(_token)}");

            var uri = new Uri(uriBuilder.ToString());
            _logger.LogDebug("Connecting to TV at {Uri}", uri);
            await _socket.ConnectAsync(uri, CancellationToken.None);

            // Handshake: wait for ms.channel.connect and extract token
            var buffer = new byte[8192];
            while (true)
            {
                var result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                    throw new WebSocketException("Handshake closed by TV");

                var msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                using var doc = JsonDocument.Parse(msg);
                if (doc.RootElement.TryGetProperty("event", out var ev) && ev.GetString() == "ms.channel.connect")
                {
                    if (doc.RootElement.TryGetProperty("data", out var data) &&
                        data.TryGetProperty("clients", out var clients) && clients.GetArrayLength() > 0)
                    {
                        var client = clients[0];
                        if (client.TryGetProperty("token", out var tok))
                            _token = tok.GetString();
                    }
                    _connected = true;
                    _logger.LogInformation("Samsung TV paired and connected");
                    break;
                }
            }
        }

        /// <inheritdoc />
        public async Task DisconnectAsync()
        {
            if (_socket == null)
                return;
            try
            {
                await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
                _logger.LogDebug("Disconnected from TV");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disconnecting");
            }
            finally
            {
                _socket.Dispose();
                _socket = null;
                _connected = false;
                _token = null;
            }
        }

        private void EnsureConnected()
        {
            if (!_connected || _socket?.State != WebSocketState.Open)
                throw new InvalidOperationException("Not connected. Call ConnectAsync() first.");
        }

        /// <inheritdoc />
        public async Task SendCommandAsync(string keyName)
        {
            EnsureConnected();
            var cmd = new
            {
                method = "ms.remote.control",
                @params = new { Cmd = "Click", DataOfCmd = keyName, Option = false, TypeOfRemote = "SendRemoteKey" }
            };
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(cmd));
            _logger.LogDebug("Sending command {Command}", keyName);
            await _socket!.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        /// <inheritdoc />
        public async Task LaunchAppAsync(string appId)
        {
            EnsureConnected();
            var launch = new
            {
                method = "ms.channel.emit",
                @params = new { @event = "ed.apps.launch", data = new { appId, action_type = "DEEP_LINK" } }
            };
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(launch));
            _logger.LogDebug("Launching app {AppId}", appId);
            await _socket!.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<SamsungAppInfo>> GetInstalledAppsAsync()
        {
            EnsureConnected();
            var req = new { method = "ms.channel.emit", @params = new { @event = "ed.installedApp.get", to = "host" } };
            var reqBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(req));
            _logger.LogDebug("Requesting installed app list");
            await _socket!.SendAsync(new ArraySegment<byte>(reqBytes), WebSocketMessageType.Text, true, CancellationToken.None);

            var apps = new List<SamsungAppInfo>();
            var buffer = new byte[8192];
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            while (!cts.IsCancellationRequested)
            {
                var result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                if (result.MessageType == WebSocketMessageType.Close)
                    throw new WebSocketException("Socket closed before app list returned");

                var msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                using var doc = JsonDocument.Parse(msg);
                if (doc.RootElement.TryGetProperty("payload", out var payload) &&
                    payload.GetProperty("data").TryGetProperty("list", out var list))
                {
                    foreach (var e in list.EnumerateArray())
                    {
                        apps.Add(new SamsungAppInfo
                        {
                            AppId = e.GetProperty("appId").GetString()!,
                            Name = e.GetProperty("name").GetString()!
                        });
                    }
                    _logger.LogInformation("Retrieved {Count} installed apps", apps.Count);
                    break;
                }
            }

            return apps;
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync() => await DisconnectAsync();
    }
}
