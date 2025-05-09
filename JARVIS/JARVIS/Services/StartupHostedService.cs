using System;
using System.Collections.Generic;
using System.Linq;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using System.Text;
using System.Threading.Tasks;
using JARVIS.Config;
using JARVIS.Controllers;
using JARVIS.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JARVIS.Services
{
    public class StartupHostedService : IHostedService
    {

        
        private readonly WeatherController _weatherCollector;
        private readonly PersonaController _moodController;
        private readonly SpeechSynthesizer _synthesizer;
        private readonly VisualizerSocketServer _visualizerServer;
        private readonly SuggestionEngine _suggestionEngine;
        private readonly SpeechRecognitionEngine _recognizer;
        private readonly string _cityName;
        private readonly ILogger<StartupHostedService> _logger;

        public StartupHostedService(
            IOptions<AppSettings> _opts,            
            WeatherController weatherCollector,
            PersonaController moodController,
            SpeechSynthesizer synthesizer,
            VisualizerSocketServer visualizerServer,
            SuggestionEngine suggestionEngine,
            SpeechRecognitionEngine recognizer,
            ILogger<StartupHostedService> logger)
        {
            _cityName = _opts.Value.CityName;            
            _weatherCollector = weatherCollector;
            _moodController = moodController;
            _synthesizer = synthesizer;
            _visualizerServer = visualizerServer;
            _suggestionEngine = suggestionEngine;
            _recognizer = recognizer;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var city = _cityName;
            // 1) Auto-detect city if missing
            if (string.IsNullOrWhiteSpace(_cityName))
            {
              //  _logger.LogInformation("Attempting to auto-detect city…");
                city = await LocationHelper.GetCityAsync();   // static call
              //  _logger.LogInformation("Detected City: {City}", city);
                _visualizerServer.Broadcast("Speaking");
              //  _synthesizer.Speak($"Detected city as {city}");
            }

            // 2) Fetch initial weather and adjust mood
            var weather = await _weatherCollector.GetWeatherAsync();
            if (!string.IsNullOrEmpty(weather))
            {
                _moodController.AdjustMoodBasedOnWeather(weather);
               // _logger.LogInformation("Startup Weather: {Weather}", weather);
                _synthesizer.Speak(weather);

                var suggestion = _suggestionEngine.CheckForSuggestion(DateTime.Now);
                if (!string.IsNullOrEmpty(suggestion))
                {
                  //  _logger.LogInformation("Suggestion: {Suggestion}", suggestion);
                    _synthesizer.Speak(suggestion);
                }
            }

            // 3) Wire up speech-recognized event
            _recognizer.SpeechRecognized += (s, e) =>
            {
                if (e.Result?.Text is string text)
                {
                   // _logger.LogDebug("Recognized input: {Text}", text);
                    _moodController.AdjustToneBasedOnAttitude(text);
                    _visualizerServer.Broadcast("Processing");
                }
            };
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

}
