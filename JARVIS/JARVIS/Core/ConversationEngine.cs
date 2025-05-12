using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JARVIS.Models;

namespace JARVIS.Core
{
    /// <summary>
    /// Manages the conversation history and handles sending user messages to the LLM via PromptEngine.
    /// Maintains context across turns, avoids repeated inputs, and enforces a maximum history length.
    /// </summary>
    public class ConversationEngine
    {
        private readonly List<Message> _messages = new List<Message>();
        private const int MaxMessages = 20;
        private readonly PromptEngine _promptEngine;
        private string _lastUserMessage = string.Empty;
        private string _lastAssistantResponse = string.Empty;

        public ConversationEngine(PromptEngine promptEngine)
        {
            _promptEngine = promptEngine;
            // Seed with an initial system prompt for context
            var systemPrompt = _promptEngine.BuildPrompt(_messages);
            _messages.Add(new Message { Role = "system", Content = systemPrompt });

        }

        /// <summary>
        /// Clears the conversation and reseeds the initial system prompt.
        /// </summary>
        public void Reset()
        {
            _messages.Clear();
            var systemPrompt = _promptEngine.BuildPrompt(_messages);
            _messages.Add(new Message { Role = "system", Content = systemPrompt });
            _lastUserMessage = string.Empty;
            _lastAssistantResponse = string.Empty;
        }

        /// <summary>
        /// Sends the user message to the LLM, tracking history and avoiding duplicate inputs.
        /// </summary>
        public async Task<string> ProcessAsync(string userMessage)
        {
            // Avoid re-processing the same input
            if (_lastUserMessage.Equals(userMessage, StringComparison.OrdinalIgnoreCase))
                return _lastAssistantResponse;

            // Add user message to history
            _messages.Add(new Message { Role = "user", Content = userMessage });
            TrimIfNeeded();

            // Send full history to the LLM
            var assistantReply = await _promptEngine.SendPromptAsync(_messages);

            // Add assistant reply to history
            _messages.Add(new Message { Role = "assistant", Content = assistantReply });
            TrimIfNeeded();

            // Update last-turn trackers
            _lastUserMessage = userMessage;
            _lastAssistantResponse = assistantReply;

            return assistantReply;
        }

        /// <summary>
        /// Ensures the history does not exceed MaxMessages, preserving the initial system prompt.
        /// </summary>
        private void TrimIfNeeded()
        {
            while (_messages.Count > MaxMessages)
            {
                // Remove the oldest non-system message (index 1 preserves system prompt)
                _messages.RemoveAt(1);
            }
        }

        /// <summary>
        /// Gets the current conversation history (read-only).
        /// </summary>
        public IReadOnlyList<Message> GetMessages() => _messages.AsReadOnly();

        /// <summary>
        /// Gets the count of messages in the current conversation history.
        /// </summary>
        public int MessageCount => _messages.Count;
    }
}
