using System;
using System.Reactive.Subjects;
using System.Threading;
using NAudio.Wave;

namespace JARVIS.Audio
{
    /// <summary>
    /// A simple beat detector that polls an AudioFileReader for amplitude peaks and emits beat events.
    /// </summary>
    public class BeatDetector : IBeatDetector
    {
        private readonly Subject<TimeSpan> _beats = new();
        private AudioFileReader _reader;
        private Timer _timer;
        private const float Threshold = 0.3f;
        private const int PollIntervalMs = 300;

        /// <summary>
        /// Observable sequence of beat timestamps.
        /// </summary>
        public IObservable<TimeSpan> OnBeat => _beats;

        /// <summary>
        /// Attach the detector to an audio reader to start beat detection.
        /// </summary>
        public void Attach(AudioFileReader reader)
        {
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
            // Set up a timer to poll the audio buffer at regular intervals
            _timer = new Timer(_ => PollAudio(), null, TimeSpan.Zero, TimeSpan.FromMilliseconds(PollIntervalMs));
        }

        private void PollAudio()
        {
            if (_reader == null) return;
            try
            {
                var buffer = new float[1024];
                int read = _reader.Read(buffer, 0, buffer.Length);
                float max = 0;
                for (int i = 0; i < read; i++)
                {
                    var abs = Math.Abs(buffer[i]);
                    if (abs > max) max = abs;
                }
                if (max >= Threshold)
                {
                    _beats.OnNext(_reader.CurrentTime);
                }
            }
            catch
            {
                // silently ignore poll errors
            }
        }

        /// <summary>
        /// Dispose of resources used by the detector.
        /// </summary>
        public void Dispose()
        {
            _timer?.Dispose();
            _beats.OnCompleted();
            _beats.Dispose();
        }
    }
}