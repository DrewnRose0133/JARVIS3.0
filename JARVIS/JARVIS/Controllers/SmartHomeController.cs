using System;
using System.Linq;
using System.Threading.Tasks;
using JARVIS.Devices.Interfaces;

namespace JARVIS.Controllers
{
    public class SmartHomeController
    {
        private readonly ISmartThingsService _smartThingsService;

        public SmartHomeController(ISmartThingsService smartThingsService)
        {
            _smartThingsService = smartThingsService;
        }

        public async Task<string> TurnOnLightsAsync(string room)
        {
            var devices = await _smartThingsService.GetDevicesAsync();
            var device = devices.FirstOrDefault(d =>
                d.Label.Contains(room, StringComparison.OrdinalIgnoreCase) &&
                d.Label.Contains("Light", StringComparison.OrdinalIgnoreCase));

            if (device == null)
                return $"No light found for room '{room}'.";

            await _smartThingsService.SendCommandAsync(device.DeviceId, "switch", "on");
            return $"{device.Label} is now ON.";
        }

        public async Task<string> TurnOffLightsAsync(string room)
        {
            var devices = await _smartThingsService.GetDevicesAsync();
            var device = devices.FirstOrDefault(d =>
                d.Label.Contains(room, StringComparison.OrdinalIgnoreCase) &&
                d.Label.Contains("Light", StringComparison.OrdinalIgnoreCase));

            if (device == null)
                return $"No light found for room '{room}'.";

            await _smartThingsService.SendCommandAsync(device.DeviceId, "switch", "off");
            return $"{device.Label} is now OFF.";
        }

        public async Task<string> OpenGarageDoorAsync()
        {
            var devices = await _smartThingsService.GetDevicesAsync();
            var device = devices.FirstOrDefault(d =>
                d.Label.Contains("Garage Door", StringComparison.OrdinalIgnoreCase));

            if (device == null)
                return "No garage door device found.";

            await _smartThingsService.SendCommandAsync(device.DeviceId, "garageDoorControl", "open");
            return $"{device.Label} is now OPEN.";
        }

        public async Task<string> CloseGarageDoorAsync()
        {
            var devices = await _smartThingsService.GetDevicesAsync();
            var device = devices.FirstOrDefault(d =>
                d.Label.Contains("Garage Door", StringComparison.OrdinalIgnoreCase));

            if (device == null)
                return "No garage door device found.";

            await _smartThingsService.SendCommandAsync(device.DeviceId, "garageDoorControl", "close");
            return $"{device.Label} is now CLOSED.";
        }
    }
}
