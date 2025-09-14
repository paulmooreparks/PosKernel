//
// Copyright 2025 Paul Moore Parks and contributors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace PosKernel.AI.Services
{
    /// <summary>
    /// Simplified OpenAI client for real AI integration with ChatGPT/GPT-4.
    /// Uses HttpClient directly without dependency injection complexity.
    /// </summary>
    public class SimpleOpenAiClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<SimpleOpenAiClient> _logger;
        private readonly string _apiKey;
        private readonly string _model;
        private readonly double _temperature;

        public SimpleOpenAiClient(ILogger<SimpleOpenAiClient> logger, string apiKey, string model = "gpt-4o", double temperature = 0.2)
        {
            _httpClient = new HttpClient();
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _model = model;
            _temperature = temperature;

            // Configure HttpClient
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "PosKernel/0.4.0");
        }

        public async Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = new
                {
                    model = _model,
                    messages = new[]
                    {
                        new { role = "system", content = "You are an AI assistant for a Point-of-Sale system. Provide helpful, concise responses for retail operations. Focus on being practical and direct." },
                        new { role = "user", content = prompt }
                    },
                    temperature = _temperature,
                    max_tokens = 500
                };

                var jsonRequest = JsonSerializer.Serialize(request);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                _logger.LogDebug("Sending OpenAI request: {Prompt}", prompt);

                var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content, cancellationToken);
                response.EnsureSuccessStatusCode();

                var jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
                using var document = JsonDocument.Parse(jsonResponse);
                
                var choices = document.RootElement.GetProperty("choices");
                var firstChoice = choices.EnumerateArray().FirstOrDefault();
                var message = firstChoice.GetProperty("message");
                var result = message.GetProperty("content").GetString() ?? "No response from AI";

                _logger.LogDebug("Received OpenAI response: {Response}", result);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling OpenAI API");
                return $"AI service temporarily unavailable: {ex.Message}";
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
