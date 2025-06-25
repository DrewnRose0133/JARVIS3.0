using JARVIS.Audio;
using JARVIS.Devices.Interfaces;

namespace JARVIS.Services.Handlers
{
    /// <summary>
    /// Handles music-related commands: DJ mode, Spotify playback, and house speaker volume control.
    /// </summary>
    public class MusicCommandHandler : ICommandHandler
    {
        private readonly DJModeManager _djManager;

        public MusicCommandHandler(DJModeManager djManager)
        {
            _djManager = djManager ?? throw new ArgumentNullException(nameof(djManager));
            // Initialize Spotify OAuth flow on startup if needed
            //_ = SpotifyClientManager.InitializeAsync();
        }

        public async Task<string?> HandleAsync(string input)
        {
            var lower = input.ToLowerInvariant();

            // Start DJ mode in a specified room
            if (lower.Contains("dj mode"))
            {
                var room = ParseRoom(lower);
                // Fire-and-forget playback/pulse loop
                _ = _djManager.PlayNextTrackAsync(room);
                return $"Starting DJ mode in {room}. Enjoy the beats!";
            }

            // Play a track via Spotify search
            if (lower.StartsWith("play "))
            {
                var query = input.Substring(5).Trim();
                var ok = await SpotifyClientManager.PlayTrackBySearchAsync(query);
                return ok
                    ? $"Now playing '{query}' on Spotify."
                    : $"Couldn't find track '{query}' on Spotify.";
            }

            // Set Spotify volume (e.g., "set volume to 50")
            if (lower.Contains("set volume to "))
            {
                var parts = lower.Split("set volume to ");
                if (parts.Length > 1 && int.TryParse(parts[1].Trim().TrimEnd('%'), out var vol))
                {
                    await SpotifyClientManager.SetVolumeAsync(vol);
                    return $"Spotify volume set to {vol}%.";
                }
            }

            // Not a music command—pass to next handler
            return null;
        }

        private string ParseRoom(string input)
        {
            const string token = "in ";
            var idx = input.IndexOf(token);
            if (idx >= 0)
            {
                var room = input[(idx + token.Length)..].Trim();
                return room;
            }
            // default room if none specified
            return "living room";
        }
    }
}
