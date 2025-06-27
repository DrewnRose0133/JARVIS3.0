using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using JARVIS.Devices.Interfaces;

namespace JARVIS.Devices
{
    class SmartThingsService : ISmartThingsService
    {
        private readonly HttpClient _client;

        public SmartThingsService(HttpClient client)
        {
            _client = client;
        }

        public async Task<IEnumerable<DeviceInfo>> GetDevicesAsync()
        {
            // calls GET https://api.smartthings.com/v1/devices
            var resp = await _client.GetFromJsonAsync<DevicesResponse>("devices");
            return resp.Items.Select(d => new DeviceInfo(d.DeviceId, d.Label));
        }

        public async Task SendCommandAsync(string deviceId, string capability, string command, object[] args = null)
        {
            var payload = new
            {
                commands = new[]
                {
                    new
                    {
                        component = "main",
                        capability,
                        command,
                        arguments = args ?? Array.Empty<object>()
                    }
                }
            };
            // POST https://api.smartthings.com/v1/devices/{deviceId}/commands
            var resp = await _client.PostAsJsonAsync($"devices/{deviceId}/commands", payload);
            resp.EnsureSuccessStatusCode();
        }

        // You’ll need these DTOs:
        private class DevicesResponse
        {
            public IEnumerable<DeviceDto> Items { get; init; }
        }
        private class DeviceDto
        {
            public string DeviceId { get; init; }
            public string Label { get; init; }
        }
    }
}
