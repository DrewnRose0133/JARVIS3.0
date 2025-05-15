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
using JARVIS.Devices;
using System.Configuration;
using System.Net.Http.Headers;
using JARVIS.Devices.CommandHandlers;
using JARVIS.Services.Handlers;

namespace JARVIS.Services
{
    public static class JarvisServiceExtensions
    {
        public static IServiceCollection AddJarvisServices(this IServiceCollection services, IConfiguration config)
        {
            // Register core singleton services

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

            // Configure and validate Weather Controller
            services.Configure<WeatherSettings>(config.GetSection("WeatherSettings"));

            services.AddHttpClient<IWeatherCollector, WeatherController>(c =>
            {
                c.BaseAddress = new Uri("http://api.weatherapi.com/v1/");
            });
            //services.AddSingleton<IWeatherCollector, WeatherController>();

            services.AddSingleton<VisualizerSocketServer>(sp =>
            {
                var cfg = sp.GetRequiredService<IConfiguration>();
                // read these from your appsettings.json under a "Visualizer" section:
                var address = cfg["Visualizer:ListenAddress"] ?? "ws://0.0.0.0";
                var port = int.TryParse(cfg["Visualizer:Port"], out var p) ? p : 8181;
                return new VisualizerSocketServer();
            });

            services.AddSingleton(sp =>
            {
                var recognizer = new SpeechRecognitionEngine();
                // TODO: load your grammar, set input device, etc.
                return recognizer;
            });

            services.AddSingleton<DJModeManager>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<AppSettings>>();
                var lightsSvc = sp.GetRequiredService<ILightsService>();
                var beatDetector = sp.GetRequiredService<IBeatDetector>();
                return new DJModeManager(options, lightsSvc, beatDetector);
            });


            services.Configure<SmartThingsSettings>(config.GetSection("SmartThings"));

            services.AddHttpClient<ISmartThingsService, SmartThingsService>((sp, client) => {
                var settings = sp.GetRequiredService<IOptions<SmartThingsSettings>>().Value;
                client.BaseAddress = new Uri("https://api.smartthings.com/v1/");
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", settings.PersonalAccessToken);
            });

            services.AddSingleton<PersonaController>();
            services.AddSingleton<ConversationEngine>();
            services.AddSingleton<AudioEngine>();
            services.AddSingleton<SmartHomeController>();
            services.AddSingleton<MemoryEngine>();
            services.AddSingleton<StatusReporter>();
            services.AddSingleton<VisualizerSocketServer>();
            services.AddHostedService<StartupEngine>();
            services.AddSingleton<SuggestionEngine>();
            services.AddSingleton<VoiceStyleController>();
            services.AddSingleton<SceneManager>();
            services.AddSingleton<UserPermissionManager>();            
            services.AddSingleton<WakeAudioBuffer>();
            services.AddHostedService<WakeWordListener>();
            services.AddSingleton<SpeechSynthesizer>();
            services.AddHostedService<StartupHostedService>();
            services.AddSingleton<VoiceAuthenticator>();
            services.AddSingleton<DJModeManager>();
            services.AddSingleton<IBeatDetector, BeatDetector>();
            services.AddSingleton<ILightsService, MqttLightsService>();
            services.AddSingleton<PromptSettings>();            
            services.AddSingleton<ConversationEngine>();
            services.AddSingleton<ICommandHandler, WeatherCommandHandler>();
            services.AddSingleton<ICommandHandler, LightsCommandHandler>();
            services.AddSingleton<ICommandHandler, ElectronicsCommandHandler>();
            services.AddSingleton<ICommandHandler, MusicCommandHandler>();
            services.AddSingleton<ICommandHandler, StatusCommandHandler>();
            services.AddSingleton<ICommandHandler, SceneCommandHandler>();

            // Always the last ICommandHander
            services.AddSingleton<ICommandHandler, ChatFallbackHandler>();
            


            services.AddSingleton<CommandHandler>();

            services.AddHttpClient<PromptEngine>((sp, client) =>
            {
                var settings = sp.GetRequiredService<IOptions<LocalAISettings>>().Value;
                client.BaseAddress = new Uri(settings.BaseUrl);
            });

            services.AddSingleton<PersonaController>();
            




/**
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
                    sp.GetRequiredService<SmartThingsService>(),
                    opts.CityName
                );
            });
**/
            return services;
        }
    }
}
