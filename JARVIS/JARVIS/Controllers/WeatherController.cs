using System.Text.Json;
using Microsoft.Extensions.Options;
using JARVIS.Config;

namespace JARVIS.Controllers
{
    public class WeatherController
    {
        private readonly HttpClient _http;
        private readonly WeatherSettings _settings;

        public WeatherController(HttpClient http, IOptions<WeatherSettings> opts)
        {
            _http = http;
            _settings = opts.Value;
        }

        public async Task<string> GetWeatherAsync()
        {
            // Ensure City is set
            var city = _settings.City;
            if (string.IsNullOrWhiteSpace(city))
                throw new InvalidOperationException("OpenWeather:City must be configured");

            city = Uri.EscapeDataString(city);

            // Compose the relative path including API key
            var path = $"weather?q={city}&units=imperial&appid={_settings.ApiKey}";

            // Let HttpClient.BaseAddress + this path form the full URL
            var relative = $"weather?q={city}&units=imperial&appid={_settings.ApiKey}";
            var fullUri = new Uri(_http.BaseAddress, relative);
           // Console.WriteLine($"[Weather] Fetching: {fullUri}");
            var response = await _http.GetAsync(fullUri);



            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException(
                    $"Weather API returned {(int)response.StatusCode} {response.StatusCode}: {body}");
            }

            // Parse the JSON payload
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = doc.RootElement;

            var temp = root.GetProperty("main").GetProperty("temp").GetDouble();
            var description = root
                .GetProperty("weather")[0]
                .GetProperty("description")
                .GetString() ?? "unknown";
            var cityName = root.GetProperty("name").GetString() ?? city;

            return $"It is currently {temp:F1}°F and {description} in {cityName}.";
        }
    }
}
