using System;
using System.Threading;
using System.Threading.Tasks;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using JARVIS.Config;
using JARVIS.Controllers;
using JARVIS.Core;
using JARVIS.UserSettings;

namespace JARVIS.Services
{
    /// <summary>
    /// Background service that manages the main interaction loop of JARVIS:
    /// sleep/wake detection, command handling, and conversation flow.
    /// </summary>
    public class InteractionLoopBackgroundService : BackgroundService
    {
        private readonly ILogger<InteractionLoopBackgroundService> _logger;
        private readonly SpeechSynthesizer _synthesizer;
        private readonly SpeechRecognitionEngine _recognizer;
        private readonly VisualizerSocketServer _visualizerServer;
        private readonly WeatherController _weatherCollector;
        private readonly PersonaController _personaController;
        private readonly SuggestionEngine _suggestionEngine;
        private readonly CommandHandler _commandHandler;
        private readonly UserPermissionManager _permissionManager;
        private readonly int _sleepTimeoutSeconds;
        private PermissionLevel _permissionLevel = PermissionLevel.Guest;
        private WakeWordListener _wakeListener;
        private bool _isAwake;
        private DateTime _lastInputTime;
        private string _userInput;

        public InteractionLoopBackgroundService(
            ILogger<InteractionLoopBackgroundService> logger,
            SpeechSynthesizer synthesizer,
            SpeechRecognitionEngine recognizer,
            VisualizerSocketServer visualizerServer,
            WeatherController weatherCollector,
            PersonaController personaController,
            SuggestionEngine suggestionEngine,
            CommandHandler commandHandler,
            UserPermissionManager permissionManager,
            
            IOptions<AppSettings> opts)
        {
            _logger = logger;
            _synthesizer = synthesizer;
            _recognizer = recognizer;
            _visualizerServer = visualizerServer;
            _weatherCollector = weatherCollector;
            _personaController = personaController;
            _suggestionEngine = suggestionEngine;
            _commandHandler = commandHandler;
            _permissionManager = permissionManager;
            _sleepTimeoutSeconds = opts.Value.SleepTimeoutSeconds;
            
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            

        // Initialize and start the wake-word listener
        _wakeListener = StartupEngine.InitializeWakeWord("hey jarvis you there", OnWakeDetected);
            _wakeListener.Start();

            _logger.LogInformation("JARVIS is sleeping. Listening for wake word...");
            _synthesizer.Speak("System online, sir. Awaiting activation.");
            _visualizerServer.Broadcast("Idle");
            _lastInputTime = DateTime.Now;

            // Subscribe to speech-recognized events for capturing user input
            _recognizer.SpeechRecognized += (s, e) =>
            {
                _userInput = e.Result?.Text;
                if (!string.IsNullOrWhiteSpace(_userInput))
                {
                    _lastInputTime = DateTime.Now;
                    _logger.LogDebug("Recognized input: {Text}", _userInput);
                    _personaController.AdjustToneBasedOnAttitude(_userInput);
                    _visualizerServer.Broadcast("Processing");
                }
            };

            return base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // If awake but no input for timeout, go back to sleep
                if (_isAwake && (DateTime.Now - _lastInputTime).TotalSeconds > _sleepTimeoutSeconds)
                {
                    _logger.LogInformation("No input received; returning to sleep.");
                    _synthesizer.Speak($"No input received for {_sleepTimeoutSeconds} seconds. Returning to sleep mode, sir.");
                    ResetRecognition();
                    continue;
                }

                // Wait until wake word detected and user input captured
                if (!_isAwake || string.IsNullOrWhiteSpace(_userInput))
                {
                    await Task.Delay(500, stoppingToken);
                    continue;
                }

                // Process the command
                if (await _commandHandler.Handle(_userInput))
                {
                    _userInput = string.Empty;
                    continue;
                }

                // TODO: Add conversation, LLM calls, suggestions, and speak-back logic here

                // Reset for the next activation
                ResetRecognition();
            }
        }

        private void OnWakeDetected()
        {
            _isAwake = true;
            _wakeListener.Stop();
            _logger.LogInformation("Wake word detected. Switching to active listening...");
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
        }

        private void ResetRecognition()
        {
            _isAwake = false;
            _userInput = string.Empty;
            UserSessionManager.Reset();
            _permissionLevel = PermissionLevel.Guest;
            _wakeListener.Start();
            _visualizerServer.Broadcast("Idle");
        }
    }
}
