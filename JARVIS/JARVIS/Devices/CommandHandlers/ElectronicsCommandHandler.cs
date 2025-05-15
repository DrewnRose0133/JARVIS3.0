using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JARVIS.Devices.Interfaces;

namespace JARVIS.Devices.CommandHandlers
{
    public class ElectronicsCommandHandler : ICommandHandler
    {
        private readonly ISmartThingsService _service;

        public ElectronicsCommandHandler(ISmartThingsService smartThingsService)
        {
            _service = smartThingsService;
        }

        public async Task<string?> HandleAsync(string input)
        {
            var lower = input.ToLowerInvariant();

            // TV on/off
            if (lower.StartsWith("turn on ") || lower.StartsWith("turn off ") && input.Contains("roses tv"))
            {
                var on = lower.StartsWith("turn on ");
                var device = input.Substring(on ? 8 : 9).Trim();
                var ok = on
                    ? await _service.TurnOnAsync(device)
                    : await _service.TurnOffAsync(device);
                return ok
                    ? $"Okay, turned {(on ? "on" : "off")} {device}."                  
                    : $"I couldn’t find a device named {device}.";

            }

            if (lower.StartsWith("turn on ") || lower.StartsWith("turn off ") && input.Contains("big one"))
            {
                var on = lower.StartsWith("turn on ");
                var device = input.Substring(on ? 8 : 9).Trim();
                var ok = on
                    ? await _service.TurnOnAsync(device)
                    : await _service.TurnOffAsync(device);
                return ok
                    ? $"Okay, turned {(on ? "on" : "off")} {device}."
                    : $"I couldn’t find a device named {device}.";

            }


            // no match → pass along
            return null;
        }
    }
}
