using JARVIS.Memory;
using JARVIS.Controllers;
using JARVIS.Config;
using JARVIS.Core;
using JARVIS.UserSettings;
using JARVIS.Audio;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System.Speech.Synthesis;
using System.Speech.Recognition;
using JARVIS.Python;
using JARVIS.Devices.Interfaces;

namespace JARVIS.Services
{
    public static class JarvisServiceExtensions
    {
        public static IServiceCollection AddJarvisServices(this IServiceCollection services, IConfiguration config)
        {
            // Configure and validate LocalAI settings
            services
                .AddOptions<LocalAISettings>()
                .Bind(config.GetSection("LocalAI"))
                .ValidateDataAnnotations()
                .Validate(s => !string.IsNullOrWhiteSpace(s.BaseUrl), "BaseUrl is required")
                .Validate(s => !string.IsNullOrWhiteSpace(s.ModelId), "ModelId is required")
                .ValidateOnStart();

            // Bind general settings
            services.Configure<AppSettings>(config);

            // Register a typed HTTP client for LocalAI
            services.AddSingleton(sp =>
            {
                var settings = sp.GetRequiredService<IOptions<LocalAISettings>>().Value;
                return new HttpClient { BaseAddress = new Uri(settings.BaseUrl) };
            });

            // Register core singleton services
            services.AddSingleton<PersonaController>();
            
            services.AddSingleton<AudioEngine>();
            services.AddSingleton<SmartHomeController>();
            services.AddSingleton<MemoryEngine>();
            services.AddSingleton<StatusReporter>();
            services.AddSingleton<StartupEngine>();
            services.AddSingleton<SuggestionEngine>();
            services.AddSingleton<VoiceStyleController>();
            services.AddSingleton<SceneManager>();
            services.AddSingleton<UserPermissionManager>();
            services.AddSingleton<DJModeManager>();
            services.AddHostedService<WakeWordBackgroundService>();
            services.AddSingleton<SpeechSynthesizer>();
            services.AddHostedService<StartupHostedService>();
            services.AddHostedService<InteractionLoopBackgroundService>();
            services.AddSingleton<VoiceAuthenticator>();
            services.AddSingleton<IBeatDetector, BeatDetector>();




            services.AddSingleton<WeatherController>(sp =>
            {
                var city = sp
                    .GetRequiredService<IOptions<AppSettings>>()
                    .Value
                    .CityName;
                return new WeatherController(city);
            });


            services.AddSingleton(sp =>
            {
                var recognizer = new SpeechRecognitionEngine();
                // TODO: load your grammar, set input device, etc.
                return recognizer;
            });

            services.AddSingleton<CommandHandler>(sp =>
            {
                var opts = sp.GetRequiredService<IOptions<AppSettings>>().Value;
                return new CommandHandler(
                    sp.GetRequiredService<PersonaController>(),                 
                    sp.GetRequiredService<MemoryEngine>(),
                    sp.GetRequiredService<WeatherController>(),
                    sp.GetRequiredService<SceneManager>(),
                    sp.GetRequiredService<SpeechSynthesizer>(),
                    sp.GetRequiredService<VoiceStyleController>(),
                    sp.GetRequiredService<StatusReporter>(),
                    sp.GetRequiredService<DJModeManager>(),
                    sp.GetRequiredService<UserPermissionManager>(),
                    opts.CityName
                );
            });

            return services;
        }
    }
}
