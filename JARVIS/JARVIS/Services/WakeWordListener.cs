using System;
using System.Linq;
using System.Globalization;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using JARVIS.Python;
using JARVIS.UserSettings;
using JARVIS.Controllers;
using JARVIS.Services;
using JARVIS.Config;
using JARVIS.Devices.Interfaces;
using JARVIS.Audio;

namespace JARVIS.Services
{
    /// <summary>
    /// Hosted service that listens for a wake word or console command,
    /// handles authentication, and switches to active recognition.
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
            // Start both wake-listening methods
            StartWakeRecognition();
            StartConsoleWakeListener();
            return Task.CompletedTask;
        }

        private void StartWakeRecognition()
        {
            _wakeBuffer.StartBuffering();

            _wakeRecognizer = new SpeechRecognitionEngine(CultureInfo.CurrentCulture);
            _wakeRecognizer.SetInputToDefaultAudioDevice();

            var choices = new Choices(new[] { _wakeWord });
            var gb = new GrammarBuilder(choices);
            var grammar = new Grammar(gb);

            _wakeRecognizer.LoadGrammar(grammar);
            _wakeRecognizer.SpeechRecognized += OnWakeRecognized;
            _wakeRecognizer.RecognizeAsync(RecognizeMode.Multiple);

            //_logger.LogInformation("[WakeWordListener] Listening for wake word...");
            Console.WriteLine("[WakeWordListener] Listening for wake word...");
        }

        private void StartConsoleWakeListener()
        {
            Task.Run(() =>
            {
                //_logger.LogInformation("[WakeWordListener] Console wake enabled. Type the wake word to activate.");
                Console.WriteLine("[WakeWordListener] Console wake enabled. Type the wake word to activate.");
                while (true)
                {
                    var line = Console.ReadLine()?.Trim().ToLowerInvariant();
                    if (line == _wakeWord)
                    {
                        //_logger.LogInformation("[WakeWordListener] Console wake phrase detected.");
                        Console.WriteLine("[WakeWordListener] Console wake phrase detected.");
                        ProcessWake();
                    }
                }
            });
        }

        private void OnWakeRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            if (!e.Result.Text.ToLowerInvariant().Contains(_wakeWord))
                return;
            //_logger.LogInformation("[WakeWordListener] Wake word detected via voice.");
            Console.WriteLine("[WakeWordListener] Wake word detected via voice.");
            ProcessWake();
        }

        private void ProcessWake()
        {
            // Stop further wake-word recognition
            try { _wakeRecognizer.RecognizeAsyncStop(); } catch { }
            _wakeRecognizer?.Dispose();

            // Stop and save buffered audio
            _wakeBuffer.StopBuffering();
            var wavPath = Path.Combine(AppContext.BaseDirectory, "wake_word.wav");
            _wakeBuffer.SaveBufferedAudio(wavPath);

            // Authenticate user
            var raw = _voiceAuth.IdentifyUserFromWav(wavPath);
            _logger.LogDebug("[WakeWordListener] Raw voice-auth result: {Raw}", raw);
            var userId = raw.Split('\n').Last().Trim().ToLowerInvariant();
            var permissionLevel = _permissionManager.GetPermission(userId);
            UserSessionManager.Authenticate(userId, permissionLevel);

            if (userId == "unknown" || permissionLevel == PermissionLevel.Guest)
            {
                //_logger.LogWarning("Voice authentication failed. User: {UserId}, Permission: {Permission}", userId, permissionLevel);
                Console.WriteLine("Voice authentication failed. User: {UserId}, Permission: {Permission}", userId, permissionLevel);
                _synthesizer.Speak("Access denied. Please authenticate.");
                // Restart wake recognition
                StartWakeRecognition();
                return;
            }

           // _logger.LogInformation("Voice authentication succeeded. User: {UserId}, Permission: {Permission}", userId, permissionLevel);
            Console.WriteLine("Voice authentication succeeded. User: {UserId}, Permission: {Permission}", userId, permissionLevel);
            _synthesizer.Speak(_personaController.GetPreamble());

            // Begin active recognition
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
