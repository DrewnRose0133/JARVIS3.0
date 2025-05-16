using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace JARVIS.Devices.Interfaces
{
    public interface ISmartThingsService
    {
        Task<bool> TurnOnAsync(string deviceName);
        Task<bool> TurnOffAsync(string deviceName);
        Task<bool> SetThermostatAsync(string deviceName, double temperature);
        Task<bool> LaunchAppAsync(string deviceName, string appName);
        Task<bool> MediaCommandAsync(string deviceName, string command); // play/pause/etc.
        Task<string?> LookupDeviceIdAsync(string label);

        Task<JsonDocument> GetDeviceStatusAsync(string deviceName);
    }

    public class SmartThingsService : ISmartThingsService
    {
        private readonly HttpClient _http;
        public SmartThingsService(HttpClient http) => _http = http;


        // helper: find deviceId by friendly label
        public async Task<string?> LookupDeviceIdAsync(string label)
        {
            var resp = await _http.GetAsync("devices");
            
            resp.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            foreach (var dev in doc.RootElement.GetProperty("items").EnumerateArray())
            {
                if (string.Equals(
                    dev.GetProperty("label").GetString(),
                    label,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return dev.GetProperty("deviceId").GetString();
                }
            }
            return null;
        }

        public async Task<JsonDocument> GetDeviceStatusAsync(string deviceName)
        {
            var deviceId = await LookupDeviceIdAsync(deviceName);
            if (deviceId == null)
                throw new InvalidOperationException($"Device '{deviceName}' not found.");

            var resp = await _http.GetAsync($"/v1/devices/{deviceId}/status");
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            return JsonDocument.Parse(json);
        }

        public async Task<bool> TurnOnAsync(string deviceName)
        {
            var id = await LookupDeviceIdAsync(deviceName);
            if (id == null) return false;

            var cmd = new
            {
                commands = new[] {
                    new {
                        component = "main",
                        capability = "switch",
                        command = "on",
                        arguments = Array.Empty<object>()
                    }
                }
            };
            var resp = await _http.PostAsJsonAsync($"devices/{id}/commands", cmd);
            return resp.IsSuccessStatusCode;
        }

        public async Task<bool> TurnOffAsync(string deviceName)
        {
            var id = await LookupDeviceIdAsync(deviceName);
            if (id == null) return false;

            var cmd = new
            {
                commands = new[] {
                    new {
                        component = "main",
                        capability = "switch",
                        command = "off",
                        arguments = Array.Empty<object>()
                    }
                }
            };
            var resp = await _http.PostAsJsonAsync($"devices/{id}/commands", cmd);
            return resp.IsSuccessStatusCode;
        }

        public async Task<bool> SetThermostatAsync(string deviceName, double temperature)
        {
            var id = await LookupDeviceIdAsync(deviceName);
            if (id == null) return false;

            // SmartThings thermostat capability uses "setHeatingSetpoint"
            var cmd = new
            {
                commands = new[] {
                    new {
                        component = "main",
                        capability = "thermostatHeatingSetpoint",
                        command = "setHeatingSetpoint",
                        arguments = new object[]{ Math.Round(temperature, 1) }
                    }
                }
            };
            var resp = await _http.PostAsJsonAsync($"devices/{id}/commands", cmd);
            return resp.IsSuccessStatusCode;
        }

        public async Task<bool> LaunchAppAsync(string deviceName, string appName)
        {
            var id = await LookupDeviceIdAsync(deviceName);
            if (id == null) return false;

            var cmd = new
            {
                commands = new[] {
                new {
                    component  = "main",
                    capability = "mediaInputSource",
                    command    = "setMediaInputSource",
                    arguments  = new[] { appName }
                }
            }
            };
            var r = await _http.PostAsJsonAsync($"devices/{id}/commands", cmd);
            return r.IsSuccessStatusCode;
        }

        public async Task<bool> MediaCommandAsync(string deviceName, string command)
        {
            var id = await LookupDeviceIdAsync(deviceName);
            if (id == null) return false;

            var cmd = new
            {
                commands = new[] {
                new {
                    component  = "main",
                    capability = "mediaPlayback",
                    command    = command,
                    arguments  = Array.Empty<object>()
                }
            }
            };
            var r = await _http.PostAsJsonAsync($"devices/{id}/commands", cmd);
            return r.IsSuccessStatusCode;
        }
    }
}
