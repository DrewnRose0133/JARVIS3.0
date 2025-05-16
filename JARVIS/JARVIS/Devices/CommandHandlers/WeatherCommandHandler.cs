using JARVIS.Controllers;
using JARVIS.Devices.Interfaces;

namespace JARVIS.Devices.CommandHandlers
{
    public class WeatherCommandHandler : ICommandHandler
    {
        private readonly IWeatherCollector _weather;
        public WeatherCommandHandler(IWeatherCollector weather) => _weather = weather;

        public async Task<string?> HandleAsync(string input)
        {
            var lower = input.ToLowerInvariant();
            if (lower.Contains("weather") || lower.Contains("forecast"))
            {
                if (lower.Contains("tomorrow"))
                    return await _weather.GetForecastByDateAsync(DateTime.Today.AddDays(1));

                if (lower.Contains("weekly") || lower.Contains("this week"))
                    return await _weather.GetWeeklyForecastAsync();

                return await _weather.GetWeatherAsync();
            }
            return null;
        }
    }
}
