using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace JARVIS.Services
{
    /// <summary>
    /// Represents a Samsung TV on the LAN. Handles pairing and remote control over WebSockets.
    /// </summary>
    public class SamsungTvClient
    {
        private readonly string _ip;
        private readonly string _appName;
        private readonly string _tokenFilePath;
        private string _token = string.Empty;
        private string _encodedBaseName = string.Empty;
        private ClientWebSocket _socket = null!;

        public SamsungTvClient(string ipAddress, string appName)
        {
            _ip = ipAddress;
            _appName = appName;
            _tokenFilePath = Path.Combine(AppContext.BaseDirectory, $"{_ip}.token");
            if (File.Exists(_tokenFilePath))
                _token = File.ReadAllText(_tokenFilePath).Trim();
        }

        /// <summary>
        /// Fetches the handshake JSON from the TV (HTTPS 8002 or HTTP 8001).
        /// </summary>
        private async Task<string> FetchHandshakeJsonAsync(CancellationToken ct)
        {
            try
            {
                using var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (_, __, ___, ____) => true
                };
                using var client = new HttpClient(handler)
                {
                    BaseAddress = new Uri($"https://{_ip}:8002/")
                };
                return await client.GetStringAsync("api/v2/", ct);
            }
            catch
            {
                using var client = new HttpClient
                {
                    BaseAddress = new Uri($"http://{_ip}:8001/")
                };
                return await client.GetStringAsync("api/v2/", ct);
            }
        }

        /// <summary>
        /// Extracts and Base64-encodes the TV's name for URL usage.
        /// </summary>
        private async Task PrepareBaseNameAsync(CancellationToken ct)
        {
            var json = await FetchHandshakeJsonAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            string? name = null;

            // Tizen: device.name
            if (root.TryGetProperty("device", out var dev) &&
                dev.TryGetProperty("name", out var nm) &&
                nm.ValueKind == JsonValueKind.String)
            {
                name = nm.GetString();
            }
            // Top-level name
            if (name == null && root.TryGetProperty("name", out var top) &&
                top.ValueKind == JsonValueKind.String)
            {
                name = top.GetString();
            }
            if (string.IsNullOrEmpty(name))
                throw new InvalidOperationException($"[{_ip}] Unable to extract TV name from handshake JSON.");

            name = WebUtility.HtmlDecode(name);
            _encodedBaseName = Convert.ToBase64String(Encoding.UTF8.GetBytes(name));
        }

        /// <summary>
        /// Performs pairing: waits for user to accept prompt on TV and captures the token.
        /// </summary>
        public async Task PairAsync(CancellationToken ct = default)
        {
            await PrepareBaseNameAsync(ct);
            var uri = new Uri($"wss://{_ip}:8002/api/v2/channels/samsung.remote.control?name={_encodedBaseName}");
            using var ws = new ClientWebSocket();
            ws.Options.RemoteCertificateValidationCallback = (_, __, ___, ____) => true;
            await ws.ConnectAsync(uri, ct);

            Console.WriteLine($"[{_ip}] Please accept pairing prompt on the TV...");
            var buffer = new byte[2048];
            while (true)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                var frame = Encoding.UTF8.GetString(buffer, 0, result.Count);
                using var doc = JsonDocument.Parse(frame);
                if (doc.RootElement.TryGetProperty("data", out var data) &&
                    data.TryGetProperty("token", out var tok))
                {
                    _token = tok.GetString()!;
                    File.WriteAllText(_tokenFilePath, _token);
                    Console.WriteLine($"[{_ip}] Pairing token saved.");
                    break;
                }
            }
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Paired", ct);
        }

        /// <summary>
        /// Opens the control WebSocket channel using the pairing token.
        /// </summary>
        public async Task ConnectAsync(CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(_encodedBaseName))
                await PrepareBaseNameAsync(ct);
            if (!string.IsNullOrEmpty(_token))
                _encodedBaseName += $"&token={_token}";

            var endpoints = new[]
            {
                new Uri($"wss://{_ip}:8002/api/v2/channels/samsung.remote.control?name={_encodedBaseName}"),
                new Uri($"ws://{_ip}:8001/api/v2/channels/samsung.remote.control?name={_encodedBaseName}")
            };

            Exception? last = null;
            foreach (var uri in endpoints)
            {
                var ws = new ClientWebSocket { Options = { KeepAliveInterval = TimeSpan.FromSeconds(20) } };
                if (uri.Scheme == "wss")
                    ws.Options.RemoteCertificateValidationCallback = (_, __, ___, ____) => true;

                try
                {
                    await ws.ConnectAsync(uri, ct);
                    _socket = ws;
                    Console.WriteLine($"[{_ip}] Control channel open via {uri.Scheme}");
                    return;
                }
                catch (Exception ex)
                {
                    last = ex;
                    Console.WriteLine($"[{_ip}] {uri.Scheme} failed: {ex.Message}");
                }
            }
            throw new InvalidOperationException($"Unable to open control channel for {_ip}", last);
        }

        /// <summary>
        /// Sends a remote control command over the open WebSocket.
        /// </summary>
        public async Task SendCommandAsync(RemoteCommand cmd, CancellationToken ct = default)
        {
            if (_socket == null || _socket.State != WebSocketState.Open)
                throw new InvalidOperationException("Control channel is not open. Call ConnectAsync() first.");

            var key = cmd switch
            {
                RemoteCommand.PowerToggle => "KEY_POWER",
                RemoteCommand.VolumeUp => "KEY_VOLUP",
                RemoteCommand.VolumeDown => "KEY_VOLDOWN",
                RemoteCommand.Home => "KEY_HOME",
                RemoteCommand.Return => "KEY_RETURN",
                RemoteCommand.ArrowUp => "KEY_UP",
                RemoteCommand.ArrowDown => "KEY_DOWN",
                RemoteCommand.ArrowLeft => "KEY_LEFT",
                RemoteCommand.ArrowRight => "KEY_RIGHT",
                RemoteCommand.Enter => "KEY_ENTER",
                RemoteCommand.Mute => "KEY_MUTE",
                _ => throw new ArgumentOutOfRangeException(nameof(cmd))
            };

            var payload = new
            {
                method = "ms.remote.control",
                @params = new { Cmd = "Click", DataOfCmd = key, Option = "false", TypeOfRemote = "SendRemoteKey" }
            };

            var data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
            await _socket.SendAsync(data, WebSocketMessageType.Text, true, ct);
        }
    }


    public enum RemoteCommand
    {
        PowerToggle,
        VolumeUp,
        VolumeDown,
        Home,
        Return,
        ArrowUp,
        ArrowDown,
        ArrowLeft,
        ArrowRight,
        Enter,
        Mute
    }
}
