using System;
using NAudio.Wave;

namespace JARVIS.Audio
{
    /// <summary>
    /// Emits events when beats are detected in an audio stream.
    /// </summary>
    public interface IBeatDetector : IDisposable
    {
        /// <summary>
        /// Fires when a beat is detected, providing the current playback position.
        /// </summary>
        IObservable<TimeSpan> OnBeat { get; }

        /// <summary>
        /// Attach the detector to an AudioFileReader so it can monitor samples.
        /// </summary>
        /// <param name="reader">The audio reader to sample for beats.</param>
        void Attach(AudioFileReader reader);
    }
}