using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using JARVIS.Config;
using JARVIS.Controllers;

namespace JARVIS.Controllers
{
    /// <summary>
    /// Uses WeatherAPI.com to provide current, specific-date, and weekly forecasts.
    /// </summary>
    public class WeatherController : IWeatherCollector
    {
        private readonly HttpClient _http;
        private readonly WeatherSettings _settings;

        public WeatherController(HttpClient http, IOptions<WeatherSettings> opts)
        {
            _http = http;
            _settings = opts.Value;
        }

        /// <summary>
        /// Gets current weather (today) via WeatherAPI.com
        /// </summary>
        public async Task<string> GetWeatherAsync()
        {
            var city = Uri.EscapeDataString(_settings.City);
            var url = $"forecast.json?key={_settings.ApiKey}&q={city}&days=1&aqi=no&alerts=no";
            var resp = await _http.GetAsync(url);
            resp.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var location = doc.RootElement.GetProperty("location").GetProperty("name").GetString();
            var current = doc.RootElement.GetProperty("current");
            var tempF = current.GetProperty("temp_f").GetDouble();
            var desc = current.GetProperty("condition").GetProperty("text").GetString();

            return $"It is currently {tempF:F1}°F and {desc} in {location}.";
        }

        /// <summary>
        /// Gets forecast for a specific date (tomorrow or a weekday) via WeatherAPI.com
        /// </summary>
        public async Task<string> GetForecastByDateAsync(DateTime date)
        {
            // Determine days parameter (WeatherAPI allows up to 10)
            int daysAhead = Math.Clamp((date.Date - DateTime.Today).Days + 1, 1, 10);
            var city = Uri.EscapeDataString(_settings.City);
            var url = $"forecast.json?key={_settings.ApiKey}&q={city}&days={daysAhead}&aqi=no&alerts=no";
            var resp = await _http.GetAsync(url);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var forecastArray = doc.RootElement
                .GetProperty("forecast")
                .GetProperty("forecastday")
                .EnumerateArray();

            // pick the last element which corresponds to our target date
            var dayElem = forecastArray.Last();
            var fcDate = DateTime.Parse(dayElem.GetProperty("date").GetString());
            var day = dayElem.GetProperty("day");
            var desc = day.GetProperty("condition").GetProperty("text").GetString();
            var maxF = day.GetProperty("maxtemp_f").GetDouble();
            var minF = day.GetProperty("mintemp_f").GetDouble();
            var location = doc.RootElement.GetProperty("location").GetProperty("name").GetString();

            return $"{fcDate:dddd, MMM d}: {desc}, high {maxF:F0}°, low {minF:F0}° in {location}.";
        }

        /// <summary>
        /// Gets a 7-day forecast via WeatherAPI.com
        /// </summary>
        public async Task<string> GetWeeklyForecastAsync()
        {
            var city = Uri.EscapeDataString(_settings.City);
            var url = $"forecast.json?key={_settings.ApiKey}&q={city}&days=7&aqi=no&alerts=no";
            var resp = await _http.GetAsync(url);
            resp.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var sb = new StringBuilder("7-day forecast:\n");

            foreach (var dayElem in doc.RootElement
                .GetProperty("forecast")
                .GetProperty("forecastday")
                .EnumerateArray())
            {
                var fcDate = DateTime.Parse(dayElem.GetProperty("date").GetString());
                var day = dayElem.GetProperty("day");
                var desc = day.GetProperty("condition").GetProperty("text").GetString();
                var maxF = day.GetProperty("maxtemp_f").GetDouble();
                var minF = day.GetProperty("mintemp_f").GetDouble();
                sb.AppendLine($"{fcDate:dddd}: {desc}, H {maxF:F0}°, L {minF:F0}°");
            }

            return sb.ToString().TrimEnd();
        }
    }
}
