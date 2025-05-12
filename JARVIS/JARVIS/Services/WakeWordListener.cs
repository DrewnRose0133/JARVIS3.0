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
    /// Includes idle timeout, speaker validation, and command echo.
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

        // Track the authorized user after wake authentication
        private string _authorizedUserId;

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

            _sleepTimeoutMs = options.Value.SleepTimeoutSeconds * 1000;
            _idleTimer = new System.Timers.Timer(_sleepTimeoutMs) { AutoReset = false };
            _idleTimer.Elapsed += (s, e) =>
            {
                Console.WriteLine("[WakeWordListener] Idle timeout reached. Returning to sleep.");
                _synthesizer.Speak("Going back to sleep.");
                try { _activeRecognizer.RecognizeAsyncCancel(); } catch { }
                _authorizedUserId = null;
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
            // Clear previous authorization when going back to wake mode
            _authorizedUserId = null;
            _wakeBuffer.StartBuffering();

            _wakeRecognizer?.Dispose();
            _wakeRecognizer = new SpeechRecognitionEngine(CultureInfo.CurrentCulture);
            _wakeRecognizer.SetInputToDefaultAudioDevice();

            var choices = new Choices(new[] { _wakeWord });
            var grammar = new Grammar(new GrammarBuilder(choices));

            _wakeRecognizer.LoadGrammar(grammar);
            _wakeRecognizer.SpeechRecognized += OnWakeRecognized;
            _wakeRecognizer.RecognizeAsync(RecognizeMode.Multiple);

            Console.WriteLine("[WakeWordListener] Listening for wake word...");
        }

        private void StartConsoleWakeListener()
        {
            Task.Run(async () =>
            {
                Console.WriteLine("[WakeWordListener] Console input enabled. Type commands anytime.");
                while (true)
                {
                    Console.Write("> ");
                    var line = Console.ReadLine()?.Trim();
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    Console.WriteLine($"> Typed command: \"{line}\"");
                    bool handled = await _commandHandler.Handle(line);
                    string response = handled
                        ? ""
                        : await _conversationEngine.ProcessAsync(line);

                    Console.WriteLine($"[JARVIS] → {response}");
                    _synthesizer.Speak(response);
                    _idleTimer.Stop();
                    _idleTimer.Start();
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
            try { _wakeRecognizer.RecognizeAsyncStop(); } catch { }
            _wakeRecognizer?.Dispose();

            _wakeBuffer.StopBuffering();
            var wavPath = Path.Combine(AppContext.BaseDirectory, "wake_word.wav");
            _wakeBuffer.SaveBufferedAudio(wavPath);

            // authenticate wake
            string raw = null;
            try
            {
                raw = _voiceAuth.IdentifyUserFromWav(wavPath);
                Console.WriteLine($"[WakeWordListener] Raw auth: '{raw}'");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WakeWordListener] Auth error: {ex}");
                _synthesizer.Speak("Authentication error.");
                StartWakeRecognition();
                return;
            }

            var lines = (raw ?? string.Empty)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .ToArray();
            var userLine = lines.FirstOrDefault(l => l.StartsWith("user:", StringComparison.OrdinalIgnoreCase));
            string userId = !string.IsNullOrEmpty(userLine)
                ? userLine.Split(':', 2)[1].Trim().ToLowerInvariant()
                : lines.LastOrDefault(l => !l.Contains(':'))?.Trim().ToLowerInvariant() ?? "unknown";

            var perm = _permissionManager.GetPermission(userId);
            UserSessionManager.Authenticate(userId, perm);
            if (userId == "unknown" || perm == PermissionLevel.Guest)
            {
                Console.WriteLine($"[WakeWordListener] Auth failed. user='{userId}', perm={perm}");
                _synthesizer.Speak("Access denied.");
                StartWakeRecognition();
                return;
            }

            // store authorized user
            _authorizedUserId = userId;
            Console.WriteLine($"[WakeWordListener] Auth succeeded. user='{userId}', perm={perm}");
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
                    var text = args.Result?.Text?.Trim();
                    Console.WriteLine($"> Command received: \"{text}\"");
                    if (string.IsNullOrWhiteSpace(text)) return;

                    // 1) grab the raw audio you just spoke
                    _wakeBuffer.StopBuffering();
                    var cmdPath = Path.Combine(AppContext.BaseDirectory, "command.wav");
                    _wakeBuffer.SaveBufferedAudio(cmdPath);
                    _wakeBuffer.StartBuffering();  // immediately start buffering the next input

                    // 2) re-authenticate the speaker against that WAV
                    string authRaw = _voiceAuth.IdentifyUserFromWav(cmdPath);
                    var authUser = authRaw
                        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                        .Last().Trim().ToLowerInvariant();
                    if (authUser != _authorizedUserId)
                    {
                        Console.WriteLine("[WakeWordListener] Speaker mismatch, ignoring command.");
                        return;
                    }

                    // 3) now handle the command
                    _idleTimer.Stop();
                    bool handled = await _commandHandler.Handle(text);
                    string response = handled
                        ? ""
                        : await _conversationEngine.ProcessAsync(text);

                    Console.WriteLine($"[JARVIS] → {response}");
                    _synthesizer.Speak(response);

                    // 4) restart idle timer
                    _idleTimer.Start();
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
