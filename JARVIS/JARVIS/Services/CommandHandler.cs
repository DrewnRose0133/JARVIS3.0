// Services/CommandHandler.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using JARVIS.Devices.Interfaces;
using JARVIS.Devices.CommandHandlers;

namespace JARVIS.Services
{
    public class CommandHandler
    {
        private readonly IList<ICommandHandler> _handlers;

        public CommandHandler(IEnumerable<ICommandHandler> handlers)
        {
            // Order matters: Lights → Weather → … → ChatFallback
            _handlers = new List<ICommandHandler>(handlers);
        }
        public async Task<string?> HandleAsync(string input)
        {
            foreach (var h in _handlers)
            {
                var result = await h.HandleAsync(input);
                if (result != null)
                    return result;
            }
            return null; // shouldn’t happen if ChatFallback is last
        }
    }
}
