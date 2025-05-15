using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JARVIS.Devices.Interfaces;

namespace JARVIS.Devices.CommandHandlers
{
    internal class LightsCommandHandler : ICommandHandler
    {
        private readonly ILightsService _lightsService;
        public LightsCommandHandler(ILightsService lights) => _lightsService = lights;

        public async Task<string?> HandleAsync(string input)
        {
            var lower = input.ToLowerInvariant();

            // Only match if it’s explicitly a light or lamp
            if (!(lower.StartsWith("turn on ") || lower.StartsWith("turn off ")))
                return null;
            if (!lower.Contains("light") && !lower.Contains("lamp"))
                return null;

            var on = lower.StartsWith("turn on ");
            var target = input.Substring(on ? 8 : 9).Trim();
            //var ok = on
            //  ? await _lightsService.TurnOnAsync(target)
            //  : await _lightsService.TurnOffAsync(target);

            // return ok
            //  ? $"Okay, turned {(on ? "on" : "off")} {target}."
            // : $"I couldn’t find any lights called “{target}.”";
            return "no";
        }
    }
}
