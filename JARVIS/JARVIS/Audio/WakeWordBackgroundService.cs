using System;
using System.Linq;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using JARVIS.Config;
using JARVIS.Controllers;
using JARVIS.Services;
using JARVIS.Python;
using JARVIS.UserSettings;

namespace JARVIS.Audio
{
    /// <summary>
    /// Background service to listen for the wake-word and handle voice authentication.
    /// </summary>
    public class WakeWordBackgroundService : BackgroundService
    {
        private readonly VoiceAuthenticator _voiceAuth;
        private readonly UserPermissionManager _permissionManager;
        private readonly SpeechSynthesizer _synthesizer;
        private readonly PersonaController _personaController;
        private readonly VisualizerSocketServer _visualizerServer;
        private readonly SpeechRecognitionEngine _recognizer;
        private readonly WakeAudioBuffer _wakeBuffer;
        private readonly CommandHandler _commandHandler;
        private readonly ILogger<WakeWordBackgroundService> _logger;

        public WakeWordBackgroundService(
            VoiceAuthenticator voiceAuth,
            UserPermissionManager permissionManager,
            SpeechSynthesizer synthesizer,
            PersonaController personaController,
            VisualizerSocketServer visualizerServer,
            SpeechRecognitionEngine recognizer,
            WakeAudioBuffer wakeBuffer,
            CommandHandler commandHandler,
            ILogger<WakeWordBackgroundService> logger)
        {
            _voiceAuth = voiceAuth;
            _permissionManager = permissionManager;
            _synthesizer = synthesizer;
            _personaController = personaController;
            _visualizerServer = visualizerServer;
            _recognizer = recognizer;
            _wakeBuffer = wakeBuffer;
            _commandHandler = commandHandler;
            _logger = logger;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Use static method for wake-word initialization
            StartupEngine.InitializeWakeWord("hey jarvis you there", () =>
            {
                _wakeBuffer.SaveBufferedAudio("wake_word.wav");

                _logger.LogInformation("Wake word detected, processing authentication...");
                Console.WriteLine("Checking user voiceprint");
                var userId = _voiceAuth.IdentifyUserFromWav("wake_word.wav")
                    .Split('\n').Last().Trim().ToLowerInvariant();

                Console.WriteLine("Checking for user authorization");
                var permissionLevel = _permissionManager.GetPermission(userId);
                UserSessionManager.Authenticate(userId, permissionLevel);

                if (userId == "unknown" || permissionLevel == PermissionLevel.Guest)
                {
                    _logger.LogWarning("Voice authentication failed. User: {UserId}, Permission: {Permission}", userId, permissionLevel);
                }
                else
                {
                    _logger.LogInformation("Voice authentication succeeded. User: {UserId}, Permission: {Permission}", userId, permissionLevel);
                }

                Console.WriteLine($"Access level for {userId}: {permissionLevel}");
                Console.WriteLine($"Recognized speaker: {userId}");

                // Begin active listening
                _visualizerServer.Broadcast("Speaking");
                _synthesizer.Speak(_personaController.GetPreamble());
                _visualizerServer.Broadcast("Listening");

                try
                {
                    _recognizer.RecognizeAsync(RecognizeMode.Multiple);
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogDebug(ex, "Recognizer was already running.");
                }
            });

            return Task.CompletedTask;
        }
    }
}
