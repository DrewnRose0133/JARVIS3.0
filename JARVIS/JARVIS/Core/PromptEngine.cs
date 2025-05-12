// PromptEngine.cs
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using JARVIS.Models;
using JARVIS.Controllers;
using Microsoft.Extensions.Options;
using JARVIS.Config; // namespace where LocalAISettings lives

namespace JARVIS.Core
{
    public class PromptEngine
    {
        private readonly HttpClient _http;
        private readonly PersonaController _personaController;
        private readonly string _modelId;
        private const string AssistantName = "J.A.R.V.I.S.";

        public PromptEngine(
            HttpClient http,
            PersonaController personaController,
            IOptions<LocalAISettings> localSettings)
        {
            _http = http;
            _personaController = personaController;
            _modelId = localSettings.Value.ModelId;
        }

        /// <summary>
        /// Builds the LLM prompt using the conversation history and current persona settings.
        /// </summary>
        public string BuildPrompt(List<Message> messages)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"You are {AssistantName}, an intelligent AI assistant modeled after the Iron Man films. You speak like a composed British butler with subtle humor and logic.");
            sb.AppendLine(_personaController.DescribePersona());
            sb.AppendLine("Always reply in this format:");
            //sb.AppendLine("Thought: <your reasoning>");
           // sb.AppendLine("Action: <what you\'re doing>");
            sb.AppendLine("<what to say to the user>");
            sb.AppendLine();

            if (_personaController.CurrentMood == Mood.Lighthearted)
                sb.AppendLine("Maintain a charming and humorous lighthearted tone.");

            if (_personaController.CurrentMood == Mood.Emergency)
                sb.AppendLine("Emergency mode: Speak seriously, directly, and without humor.");

            if (_personaController.SarcasmEnabled)
                sb.AppendLine("Use subtle, dry sarcasm where appropriate.");

            sb.AppendLine();
            foreach (var message in messages)
            {
                switch (message.Role)
                {
                    case "user":
                        sb.AppendLine($"User: {message.Content}");
                        break;
                    case "assistant":
                        sb.AppendLine($"{AssistantName}: {message.Content}");
                        break;
                    case "system":
                        sb.AppendLine(message.Content);
                        break;
                }
            }

            sb.AppendLine($"{AssistantName}:");
            return sb.ToString();
        }

        /// <summary>
        /// Sends the built prompt to the LLM endpoint and returns the assistant's reply.
        /// </summary>
        public async Task<string> SendPromptAsync(List<Message> messages)
        {
            // 1) Build the prompt string
            var prompt = BuildPrompt(messages);

            // 2) Construct the request body for chat completion, using modelId from configuration
            var body = new
            {
                model = _modelId,
                messages = messages
            };

            // 3) POST to the chat completions endpoint
            var response = await _http.PostAsJsonAsync("/v1/chat/completions", body);
            response.EnsureSuccessStatusCode();

            // 4) Parse the JSON and extract the assistant message
            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return content.Trim();
        }
    }
}