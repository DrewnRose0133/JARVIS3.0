using JARVIS.Core;
using JARVIS.Devices.Interfaces;
using JARVIS.Memory;

namespace JARVIS.Devices.CommandHandlers
{
    public class SceneCommandHandler : ICommandHandler
    {
        private readonly SceneManager _sceneManager;
        private readonly MemoryEngine _memoryEngine;

        public SceneCommandHandler(SceneManager sceneManager)
        {
            _sceneManager = sceneManager;
            _memoryEngine = new MemoryEngine();
        }

        public async Task<string?> HandleAsync(string input)
        {
            var lower = input.ToLowerInvariant();

            if (lower.StartsWith("remember scene"))
            {
                var trimmed = input.Replace("remember scene", "", StringComparison.OrdinalIgnoreCase).Trim();
                var parts = trimmed.Split(" is ", 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    _memoryEngine.Remember($"scene:{parts[0].Trim()}", parts[1].Trim());
                   // _synthesizer.Speak($"Scene {parts[0].Trim()} saved, sir.");
                    return "scene saved";
                }
            }

            return null;
        }
    }
}
