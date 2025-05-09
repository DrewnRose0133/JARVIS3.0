using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NAudio.Wave;
using JARVIS.Config;
using JARVIS.Devices.Interfaces;
using JARVIS.Audio;

namespace JARVIS.Audio
{
    /// <summary>
    /// Manages DJ mode: plays music tracks and pulses lights on detected beats.
    /// </summary>
    public class DJModeManager
    {
        private readonly string _musicDirectory;
        private readonly ILightsService _lightsService;
        private readonly IBeatDetector _beatDetector;
        private readonly List<string> _trackPaths;
        private int _currentTrackIndex;

        public DJModeManager(
            IOptions<AppSettings> options,
            ILightsService lightsService,
            IBeatDetector beatDetector)
        {
            if (options?.Value?.Music?.MusicDirectory == null)
                throw new ArgumentException(
                    "Music:MusicDirectory must be configured in appsettings.json.", nameof(options));

            _musicDirectory = options.Value.Music.MusicDirectory;
            _lightsService = lightsService ?? throw new ArgumentNullException(nameof(lightsService));
            _beatDetector = beatDetector ?? throw new ArgumentNullException(nameof(beatDetector));
            _trackPaths = LoadTracks();
            _currentTrackIndex = 0;
        }

        private List<string> LoadTracks()
        {
            if (!Directory.Exists(_musicDirectory))
                throw new DirectoryNotFoundException($"Music directory not found: {_musicDirectory}");

            var files = new List<string>(Directory.GetFiles(_musicDirectory, "*.mp3"));
            files.AddRange(Directory.GetFiles(_musicDirectory, "*.wav"));
            return files;
        }

        /// <summary>
        /// Plays the next track asynchronously and pulses lights on each beat.
        /// </summary>
        /// <param name="roomId">Identifier for the lights/room to pulse.</param>
        public async Task PlayNextTrackAsync(string roomId, CancellationToken cancellationToken = default)
        {
            if (_trackPaths.Count == 0)
                return;

            var trackPath = _trackPaths[_currentTrackIndex];
            using var audioFile = new AudioFileReader(trackPath);
            using var outputDevice = new WaveOutEvent();
            outputDevice.Init(audioFile);

            // Attach beat detector to the file reader
            _beatDetector.Attach(audioFile);

            // Subscribe to beat events to pulse lights
            using var subscription = _beatDetector.OnBeat.Subscribe(_ =>
            {
                // Fire-and-forget pulse
              //  _ = _lightsService.PulseAsync(roomId);
            });

            // Start playback
            outputDevice.Play();

            // Wait for playback to finish or cancellation
            while (outputDevice.PlaybackState == PlaybackState.Playing &&
                   !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(200, cancellationToken);
            }

            // Advance to next track
            _currentTrackIndex = (_currentTrackIndex + 1) % _trackPaths.Count;
        }
    }
}
