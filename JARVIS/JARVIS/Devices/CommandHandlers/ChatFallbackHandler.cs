using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JARVIS.Core;
using JARVIS.Devices.Interfaces;

namespace JARVIS.Devices.CommandHandlers
{
    public class ChatFallbackHandler : ICommandHandler
    {
        private readonly ConversationEngine _convo;
        public ChatFallbackHandler(ConversationEngine convo) => _convo = convo;

        public async Task<string?> HandleAsync(string input)
        {
            return await _convo.ProcessAsync(input);
        }
    }
}
