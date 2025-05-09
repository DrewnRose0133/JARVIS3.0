using System.Globalization;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using JARVIS.Python;
using JARVIS.UserSettings;
using JARVIS.Controllers;
using JARVIS.Audio;

namespace JARVIS.Services
{
    /// <summary>
    /// Hosted service that listens for a wake word, handles authentication,
    /// and switches to active recognition on wake.
    /// </summary>
    public class WakeWordListener : BackgroundService
    {
        private readonly string _wakeWord;
        private SpeechRecognitionEngine _wakeRecognizer;
        private readonly WakeAudioBuffer _wakeBuffer;
        private readonly VoiceAuthenticator _voiceAuth;
        private readonly UserPermissionManager _permissionManager;
        private readonly SpeechSynthesizer _synthesizer;
        private readonly PersonaController _personaController;
        private readonly VisualizerSocketServer _visualizerServer;
        private readonly SpeechRecognitionEngine _activeRecognizer;
        private readonly CommandHandler _commandHandler;
        private readonly ILogger<WakeWordListener> _logger;

        public WakeWordListener(
            VoiceAuthenticator voiceAuth,
            UserPermissionManager permissionManager,
            SpeechSynthesizer synthesizer,
            PersonaController personaController,
            VisualizerSocketServer visualizerServer,
            SpeechRecognitionEngine activeRecognizer,
            WakeAudioBuffer wakeBuffer,
            CommandHandler commandHandler,
            ILogger<WakeWordListener> logger,
            string wakeWord = "hey jarvis you there")
        {
            _wakeWord = wakeWord.ToLowerInvariant();
            _voiceAuth = voiceAuth;
            _permissionManager = permissionManager;
            _synthesizer = synthesizer;
            _personaController = personaController;
            _visualizerServer = visualizerServer;
            _activeRecognizer = activeRecognizer;
            _wakeBuffer = wakeBuffer;
            _commandHandler = commandHandler;
            _logger = logger;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            StartListening();
            return Task.CompletedTask;
        }

        private void StartListening()
        {
            // Begin buffering raw audio for wake-word authentication
            _wakeBuffer.Start();

            _wakeRecognizer = new SpeechRecognitionEngine(CultureInfo.CurrentCulture);
            _wakeRecognizer.SetInputToDefaultAudioDevice();

            var choices = new Choices(new[] { _wakeWord });
            var gb = new GrammarBuilder(choices);
            var grammar = new Grammar(gb);

            _wakeRecognizer.LoadGrammar(grammar);
            _wakeRecognizer.SpeechRecognized += OnWakeRecognized;
            _wakeRecognizer.RecognizeAsync(RecognizeMode.Multiple);

            _logger.LogInformation("[WakeWordListener] Listening for wake word...");
        }
        private void OnWakeRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            if (!e.Result.Text.ToLowerInvariant().Contains(_wakeWord))
                return;

            _logger.LogInformation("[WakeWordListener] Wake word detected.");

            // Stop recognition and audio buffering
            try { _wakeRecognizer.RecognizeAsyncStop(); } catch { }
            _wakeRecognizer.Dispose();
            _wakeBuffer.Stop();

            // Save buffered audio for authentication
            var wavPath = Path.Combine(AppContext.BaseDirectory, "wake_word.wav");
            _wakeBuffer.SaveBufferedAudio(wavPath);

            // ... existing authentication logic follows ...(object sender, SpeechRecognizedEventArgs e)
            {
                if (!e.Result.Text.ToLowerInvariant().Contains(_wakeWord))
                    return;

                _logger.LogInformation("[WakeWordListener] Wake word detected.");

                // Stop wake-word listening
                try { _wakeRecognizer.RecognizeAsyncStop(); } catch { }
                _wakeRecognizer.Dispose();

                // Wait briefly to ensure buffered audio is flushed into the file
                Thread.Sleep(300);

                // Save buffered audio for authentication");
                // Stop wake-word listening
                try { _wakeRecognizer.RecognizeAsyncStop(); } catch { }
                _wakeRecognizer.Dispose();

                // Save buffered audio for authentication
                _wakeBuffer.SaveBufferedAudio("wake_word.wav");

                // Authenticate user
                var userId = _voiceAuth
                    .IdentifyUserFromWav("wake_word.wav")
                    .Split('\n').Last().Trim().ToLowerInvariant();
                var permissionLevel = _permissionManager.GetPermission(userId);
                UserSessionManager.Authenticate(userId, permissionLevel);

                if (userId == "unknown" || permissionLevel == PermissionLevel.Guest)
                {
                    _logger.LogWarning("Voice authentication failed. User: {UserId}, Permission: {Permission}", userId, permissionLevel);
                    _synthesizer.Speak("Access denied. Please authenticate.");
                    // re-start wake-word listening
                    StartListening();
                    return;
                }

                _logger.LogInformation("Voice authentication succeeded. User: {UserId}, Permission: {Permission}", userId, permissionLevel);
                _synthesizer.Speak(_personaController.GetPreamble());

                // Broadcast and enter active recognition
                _visualizerServer.Broadcast("Listening");
                try
                {
                    _activeRecognizer.SetInputToDefaultAudioDevice();
                    _activeRecognizer.LoadGrammar(new DictationGrammar());
                    _activeRecognizer.SpeechRecognized += async (s, args) =>
                    {
                        var text = args.Result?.Text;
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            await _commandHandler.Handle(text);
                        }
                    };
                    _activeRecognizer.RecognizeAsync(RecognizeMode.Multiple);
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogDebug(ex, "Active recognizer already running.");
                }
            }
        }
    }
}
