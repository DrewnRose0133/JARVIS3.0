using System;
using System.Reactive.Subjects;
using System.Threading;
using JARVIS.Devices.Interfaces;
using NAudio.Wave;

namespace JARVIS.Audio
{
    public class BeatDetector : IBeatDetector, IDisposable
    {
        private readonly Subject<TimeSpan> _beats = new();
        public IObservable<TimeSpan> OnBeat => _beats;
        private AudioFileReader _reader;
        private Timer _timer;

        public void Attach(IWavePlayer player)
        {
            // Assume player.Init(audioFileReader) was already called
            _reader = /* get the reader you passed to the player */;
            // Poll every 300ms (tweak as needed)
            _timer = new Timer(_ =>
            {
                float max = 0;
                // Peek a small window of samples
                var buffer = new float[1024];
                int read = _reader.Read(buffer, 0, buffer.Length);
                for (int i = 0; i < read; i++)
                    if (Math.Abs(buffer[i]) > max) max = Math.Abs(buffer[i]);

                if (max > 0.3f)  // threshold; tune this!
                    _beats.OnNext(_reader.CurrentTime);
            }, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(300));
        }

        public void Dispose()
        {
            _timer?.Dispose();
            _beats?.OnCompleted();
        }
    }
}
