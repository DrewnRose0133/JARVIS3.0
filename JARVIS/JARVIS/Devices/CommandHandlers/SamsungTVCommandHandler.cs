using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JARVIS.Devices.CommandHandlers
{
    using JARVIS.Devices.Interfaces;

    /// <summary>
    /// Implements ICommandHandler to parse text commands and invoke the SamsungTVService.
    /// </summary>
    public class SamsungTVCommandHandler : ICommandHandler
    {
        private readonly ISamsungTVService _tvService;

        public SamsungTVCommandHandler(ISamsungTVService tvService)
        {
            _tvService = tvService;
        }

        /// <summary>
        /// Parses a textual input and executes the matching TV control operation.
        /// Returns a user-friendly response string.
        /// </summary>
        /// <param name="input">Commands like "connect", "volume up", "launch netflix".</param>
        public async Task<string?> HandleAsync(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "No command provided.";

            var cmd = input.Trim().ToLowerInvariant();
            try
            {
                switch (cmd)
                {
                    case "connect":
                        await _tvService.ConnectAsync();
                        return "Connected to Samsung TV.";

                    case "disconnect":
                        await _tvService.DisconnectAsync();
                        return "Disconnected from Samsung TV.";

                    case "volume up":
                        await _tvService.SendCommandAsync("KEY_VOLUP");
                        return "Volume increased.";

                    case "volume down":
                        await _tvService.SendCommandAsync("KEY_VOLDOWN");
                        return "Volume decreased.";

                    case "mute":
                        await _tvService.SendCommandAsync("KEY_MUTE");
                        return "Muted.";

                    case var s when s.StartsWith("launch "):
                        var app = input.Substring(7).Trim();
                        // Map known names to IDs or assume ID passed
                        var appId = app switch
                        {
                            "netflix" => "111299001912",
                            "youtube" => "11129900191201",
                            _ => app
                        };
                        await _tvService.LaunchAppAsync(appId);
                        return $"Launching {app}.";

                    default:
                        return $"Unknown command: '{input}'";
                }
            }
            catch (Exception ex)
            {
                return $"Error executing '{input}': {ex.Message}";
            }
        }

    }
}

