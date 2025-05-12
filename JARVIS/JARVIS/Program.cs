using Microsoft.Extensions.Hosting;
using JARVIS.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using JARVIS.Audio;
using JARVIS.UserSettings;
using JARVIS.Core;
using System.Speech.Synthesis;

namespace JARVIS
{
    class Program
    {
        static async Task Main(string[] args)
        {
            bool isAwake = false;

            var builder = Host.CreateApplicationBuilder(args);

            builder.Services.AddJarvisServices(builder.Configuration);
            builder.Services.AddHostedService<JarvisHostedService>();

            var app = builder.Build();
            

            var host = Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((ctx, cfg) =>
                {
                    cfg.SetBasePath(AppContext.BaseDirectory)
                       .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                })
                .ConfigureServices((ctx, svc) =>
                    svc.AddJarvisServices(ctx.Configuration))
                .Build();

            var commandHandler = host.Services.GetRequiredService<CommandHandler>();

            // If you need a scope:
            using var scope = host.Services.CreateScope();
            var sp = scope.ServiceProvider;

            var synthesizer = sp.GetRequiredService<SpeechSynthesizer>();

            await host.RunAsync();


            _ = Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(TimeSpan.FromMinutes(2)); // 🕒 Suggest every 2 minutes

                    if (isAwake && UserSessionManager.CurrentPermission == PermissionLevel.Admin)
                    {
                        var suggestionEngine = new SuggestionEngine();
                        var suggestion = suggestionEngine.GetSuggestion();

                        if (!string.IsNullOrEmpty(suggestion))
                        {
                            Console.WriteLine($"JARVIS (proactive): {suggestion}");
                            synthesizer.Speak(suggestion);
                        }
                    }
                }
            });
        }
    }
}