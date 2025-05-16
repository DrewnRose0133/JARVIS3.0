using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using JARVIS.Devices.Interfaces;
using JARVIS.Services.Handlers;

namespace JARVIS.Services.Handlers
{
    /// <summary>
    /// Handles thermostat commands via IThermostatService (SmartThings integration).
    /// Supports setting temperature, turning on heat/AC, and querying current temperature.
    /// </summary>
    public class ThermostatCommandHandler : ICommandHandler
    {
        private readonly IThermostatService _thermostatService;
        private const string DefaultZone = "Thermostat";

        public ThermostatCommandHandler(IThermostatService thermostatService)
        {
            _thermostatService = thermostatService ?? throw new ArgumentNullException(nameof(thermostatService));
        }

        public async Task<string?> HandleAsync(string input)
        {
            var lower = input.ToLowerInvariant();

            // Query current temperature
            if (lower.Contains("current temperature") || lower.Contains("what's the temperature") || lower.Contains("current temp"))
            {
                var zone = ParseZone(lower);
                var temp = await _thermostatService.GetCurrentTemperatureAsync(zone);
                if (temp.HasValue)
                    return $"The current temperature in {zone} is {temp.Value:F1}°.";
                return "I couldn't retrieve the current temperature.";
            }

            // Set thermostat to a specific temperature
            if (lower.Contains("set thermostat to") || lower.Contains("set temperature to"))
            {
                var tempValue = ExtractFirstNumber(lower);
                if (tempValue.HasValue)
                {
                    var zone = ParseZone(lower);
                    await _thermostatService.SetTemperatureAsync(zone, tempValue.Value);
                    return $"Setting thermostat in {zone} to {tempValue.Value:F1}°.";
                }
                return "I didn't catch the temperature to set. Please specify a number.";
            }

            // Turn on heat or AC explicitly
            if (lower.Contains("turn on heat to") || lower.Contains("heat to"))
            {
                var tempValue = ExtractFirstNumber(lower);
                if (tempValue.HasValue)
                {
                    await _thermostatService.TurnOnHeat(tempValue.Value);
                    return $"Turning on heat to {tempValue.Value:F1}°.";
                }
                return "Please specify a temperature for the heat.";
            }

            if (lower.Contains("turn on ac to") || lower.Contains("ac to") || lower.Contains("turn on ac to"))
            {
                var tempValue = ExtractFirstNumber(lower);
                if (tempValue.HasValue)
                {
                    await _thermostatService.TurnOnAC(tempValue.Value);
                    return $"Turning on AC to {tempValue.Value:F1}°.";
                }
                return "Please specify a temperature for the AC.";
            }

            return null;
        }

        /// <summary>
        /// Extracts the first numeric value (integer or decimal) from the input string.
        /// </summary>
        private double? ExtractFirstNumber(string input)
        {
            var match = Regex.Match(input, "\\d+(\\.\\d+)?");
            if (match.Success && double.TryParse(match.Value, out var value))
                return value;
            return null;
        }

        /// <summary>
        /// Parses a room/zone from phrases like "in the living room"; defaults if none found.
        /// </summary>
        private string ParseZone(string input)
        {
            var token = " in ";
            var idx = input.IndexOf(token, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var zone = input.Substring(idx + token.Length).Trim();
                return string.IsNullOrEmpty(zone) ? DefaultZone : zone;
            }
            return DefaultZone;
        }
    }
}
