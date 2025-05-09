using System;

namespace JARVIS.Config
{
    /// <summary>
    /// Binds to top-level settings in appsettings.json
    /// </summary>
    public class AppSettings
    {
        /// <summary>
        /// Settings for LocalAI (BaseUrl, ModelId etc.)
        /// </summary>
        public LocalAISettings LocalAI { get; set; }

        /// <summary>
        /// Timeout before going to sleep, in seconds
        /// </summary>
        public int SleepTimeoutSeconds { get; set; } = 15;

        /// <summary>
        /// Default city name for weather and localization
        /// </summary>
        public string CityName { get; set; }

        /// <summary>
        /// Nested music-related settings
        /// </summary>
        public MusicSettings Music { get; set; }
    }

    /// <summary>
    /// Configuration section for music playback
    /// </summary>
    public class MusicSettings
    {
        /// <summary>
        /// Directory path containing music tracks
        /// </summary>
        public string MusicDirectory { get; set; }
    }
}
