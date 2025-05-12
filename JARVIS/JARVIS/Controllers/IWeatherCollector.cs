// IWeatherCollector.cs
using System;
using System.Threading.Tasks;

namespace JARVIS.Controllers
{
    public interface IWeatherCollector
    {
        /// <summary>
        /// Current weather (today).
        /// </summary>
        Task<string> GetWeatherAsync();

        /// <summary>
        /// Forecast for a specific date (tomorrow or up to 10 days ahead).
        /// </summary>
        Task<string> GetForecastByDateAsync(DateTime date);

        /// <summary>
        /// 7-day forecast.
        /// </summary>
        Task<string> GetWeeklyForecastAsync();
    }
}
