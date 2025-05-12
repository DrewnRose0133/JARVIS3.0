using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using JARVIS.Python;
using JARVIS.UserSettings;
using JARVIS.Controllers;
using JARVIS.Services;
using JARVIS.Config;
using JARVIS.Devices.Interfaces;
using JARVIS.Audio;
using JARVIS.Core;

namespace JARVIS.Services
{
    /// <summary>
    /// Hosted service that listens for a wake word or console command,
    /// handles authentication, and switches to active recognition.
    /// Includes idle timeout to return to sleep mode.
    /// </summary>
    public class WakeWordListener : BackgroundService
    {
        private readonly System.Timers.Timer _idleTimer;
        private readonly int _sleepTimeoutMs;
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
        private readonly ConversationEngine _conversationEngine;
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
            ConversationEngine conversationEngine,
            ILogger<WakeWordListener> logger,
            IOptions<AppSettings> options,
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
            _conversationEngine = conversationEngine;
            _logger = logger;

            // configure idle timeout
            _sleepTimeoutMs = options.Value.SleepTimeoutSeconds * 1000;
            _idleTimer = new System.Timers.Timer(_sleepTimeoutMs) { AutoReset = false };
            _idleTimer.Elapsed += (s, e) =>
            {
                Console.WriteLine("[WakeWordListener] Idle timeout reached. Returning to sleep.");
                _synthesizer.Speak("Going back to sleep.");
                try { _activeRecognizer.RecognizeAsyncCancel(); } catch { }
                StartWakeRecognition();
            };
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            StartWakeRecognition();
            StartConsoleWakeListener();
            return Task.CompletedTask;
        }

        private void StartWakeRecognition()
        {
            _wakeBuffer.StartBuffering();

            _wakeRecognizer?.Dispose();
            _wakeRecognizer = new SpeechRecognitionEngine(CultureInfo.CurrentCulture);
            _wakeRecognizer.SetInputToDefaultAudioDevice();

            var choices = new Choices(new[] { _wakeWord });
            var gb = new GrammarBuilder(choices);
            var grammar = new Grammar(gb);

            _wakeRecognizer.LoadGrammar(grammar);
            _wakeRecognizer.SpeechRecognized += OnWakeRecognized;
            _wakeRecognizer.RecognizeAsync(RecognizeMode.Multiple);

            Console.WriteLine("[WakeWordListener] Listening for wake word...");
        }

        private void StartConsoleWakeListener()
        {
            Task.Run(() =>
            {
                Console.WriteLine("[WakeWordListener] Console wake enabled. Type the wake word to activate.");
                while (true)
                {
                    var line = Console.ReadLine()?.Trim().ToLowerInvariant();
                    if (line == _wakeWord)
                    {
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

            Console.WriteLine("[WakeWordListener] Wake word detected via voice.");
            ProcessWake();
        }

        private void ProcessWake()
        {
            // stop wake-word recognizer
            try { _wakeRecognizer.RecognizeAsyncStop(); } catch { }
            _wakeRecognizer?.Dispose();

            // capture audio
            _wakeBuffer.StopBuffering();
            var wavPath = Path.Combine(AppContext.BaseDirectory, "wake_word.wav");
            _wakeBuffer.SaveBufferedAudio(wavPath);

            // log WAV file info
            try
            {
                var fi = new FileInfo(wavPath);
                Console.WriteLine($"[WakeWordListener] Saved wake-word WAV to {wavPath} ({fi.Length} bytes).");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WakeWordListener] ERROR reading WAV file: {ex}");
            }

            // perform authentication
            string raw = null;
            try
            {
                raw = _voiceAuth.IdentifyUserFromWav(wavPath);
                Console.WriteLine($"[WakeWordListener] Raw voice-auth result: '{raw}'");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WakeWordListener] Exception in IdentifyUserFromWav: {ex}");
                _synthesizer.Speak("Authentication error.");
                StartWakeRecognition();
                return;
            }

            // split into lines and log them
            var lines = (raw ?? string.Empty)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .ToArray();
            foreach (var line in lines)
                Console.WriteLine($"  → auth line: '{line}'");

            // parse userId (support plain username without 'user:' prefix)
            string userId;
            var userLine = lines.FirstOrDefault(l => l.StartsWith("user:", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(userLine))
                userId = userLine.Split(':', 2)[1].Trim().ToLowerInvariant();
            else
                userId = lines.LastOrDefault(l => !l.Contains(":"))?.Trim().ToLowerInvariant() ?? "unknown";

            // permission check
            var permissionLevel = _permissionManager.GetPermission(userId);
            UserSessionManager.Authenticate(userId, permissionLevel);

            if (userId == "unknown" || permissionLevel == PermissionLevel.Guest)
            {
                Console.WriteLine($"[WakeWordListener] Voice authentication failed. user='{userId}', perm={permissionLevel}");
                _synthesizer.Speak("Access denied. Please try again.");
                StartWakeRecognition();
                return;
            }

            Console.WriteLine($"[WakeWordListener] Voice authentication succeeded. user='{userId}', perm={permissionLevel}");
            _synthesizer.Speak(_personaController.GetPreamble());

            // begin active recognition
            _visualizerServer.Broadcast("Listening");
            try
            {
                _activeRecognizer.UnloadAllGrammars();
                _activeRecognizer.SetInputToDefaultAudioDevice();
                _activeRecognizer.LoadGrammar(new DictationGrammar());

                _activeRecognizer.SpeechRecognized += async (s, args) =>
                {
                    // reset idle timeout
                    _idleTimer.Stop();
                    _idleTimer.Start();

                    var text = args.Result?.Text?.Trim();
                    Console.WriteLine($"[WakeWordListener] Heard: {text}");
                    if (string.IsNullOrWhiteSpace(text)) return;

                    bool handled = await _commandHandler.Handle(text);
                    string response;
                    if (handled)
                    {
                        response = "Command executed.";
                    }
                    else
                    {
                        _conversationEngine.AddUserMessage(text);
                        response = await _conversationEngine.ProcessAsync(text);
                        _conversationEngine.AddAssistantMessage(response);
                    }

                    Console.WriteLine($"[JARVIS] → {response}");
                    _synthesizer.Speak(response);
                };

                _activeRecognizer.SpeechRecognitionRejected += (s, args) =>
                    Console.WriteLine("[WakeWordListener] No speech matched.");

                _activeRecognizer.RecognizeAsync(RecognizeMode.Multiple);
                _idleTimer.Stop();
                _idleTimer.Start();
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogDebug(ex, "Active recognizer already running.");
            }
        }
    }
}