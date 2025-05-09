using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NAudio.Wave;

namespace JARVIS.Devices.Interfaces
{
    /// <summary>
    /// Emits a notification whenever a beat is detected on a playing audio stream.
    /// </summary>
    public interface IBeatDetector
    {
        /// <summary>
        /// Fires when the next beat is found in the audio.
        /// </summary>
        IObservable<TimeSpan> OnBeat { get; }

        /// <summary>
        /// Attach the detector to an NAudio playback device so it can monitor its output.
        /// </summary>
        void Attach(IWavePlayer player);
    }
}
