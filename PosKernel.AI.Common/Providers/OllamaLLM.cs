/*
Copyright 2025 Paul Moore Parks and contributors

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

using Microsoft.Extensions.Logging;
using PosKernel.AI.Common.Abstractions;
using System.Text;
using System.Text.Json;

namespace PosKernel.AI.Common.Providers;

/// <summary>
/// Ollama implementation of ILargeLanguageModel
/// ARCHITECTURAL PRINCIPLE: Clean interface implementation for local LLM
/// </summary>
public class OllamaLLM : ILargeLanguageModel
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaLLM> _logger;
    private readonly string _baseUrl;
    private readonly string _model;

    public OllamaLLM(
        HttpClient httpClient,
        ILogger<OllamaLLM> logger,
        string baseUrl,
        string model)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
        _model = model ?? throw new ArgumentNullException(nameof(model));

        // ARCHITECTURAL FIX: Set appropriate timeout for local LLM calls
        _httpClient.Timeout = TimeSpan.FromSeconds(60);
    }

    public LLMProviderInfo ProviderInfo => new()
    {
        Name = "Ollama",
        Model = _model,
        Provider = "Local Ollama",
        IsLocal = true,
        HasRateLimits = false,
        Description = $"Local GPU-accelerated LLM via Ollama at {_baseUrl}"
    };

    public async Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
    {
        try
        {
            var requestBody = new OllamaGenerateRequest
            {
                Model = _model,
                Prompt = prompt,
                Stream = false
            };

            var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogDebug("Calling Ollama generate API: {Model} at {BaseUrl}", _model, _baseUrl);

            var response = await _httpClient.PostAsync($"{_baseUrl}/api/generate", content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException(
                    $"DESIGN DEFICIENCY: Ollama API call failed with status {response.StatusCode}. " +
                    $"Response: {errorContent}. " +
                    $"Ensure Ollama is running and model '{_model}' is available.");
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var ollamaResponse = JsonSerializer.Deserialize<OllamaGenerateResponse>(responseJson, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (string.IsNullOrEmpty(ollamaResponse?.Response))
            {
                throw new InvalidOperationException(
                    $"DESIGN DEFICIENCY: Ollama returned invalid response format. " +
                    $"Expected 'response' field but got: {responseJson}");
            }

            _logger.LogDebug("Ollama response received: {CharCount} characters", ollamaResponse.Response.Length);
            return ollamaResponse.Response;
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                $"DESIGN DEFICIENCY: Cannot connect to Ollama at {_baseUrl}. " +
                $"Ensure Ollama is running and accessible. " +
                $"Error: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            throw new InvalidOperationException(
                $"DESIGN DEFICIENCY: Ollama request timed out. " +
                $"Model '{_model}' may be slow to respond or not loaded. " +
                $"Error: {ex.Message}", ex);
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if Ollama server is running
            var healthResponse = await _httpClient.GetAsync(_baseUrl, cancellationToken);
            if (!healthResponse.IsSuccessStatusCode)
            {
                return false;
            }

            // ARCHITECTURAL FIX: Ensure model is loaded via API pull before use
            var pullRequest = new { name = _model };
            var pullJson = JsonSerializer.Serialize(pullRequest);
            var pullContent = new StringContent(pullJson, Encoding.UTF8, "application/json");

            _logger.LogDebug("Ensuring model {Model} is loaded in Ollama API", _model);
            var pullResponse = await _httpClient.PostAsync($"{_baseUrl}/api/pull", pullContent, cancellationToken);

            return pullResponse.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ollama server not available at {BaseUrl}", _baseUrl);
            return false;
        }
    }

    private class OllamaGenerateRequest
    {
        public string Model { get; set; } = "";
        public string Prompt { get; set; } = "";
        public bool Stream { get; set; } = false;
    }

    private class OllamaGenerateResponse
    {
        public string Model { get; set; } = "";
        public string Response { get; set; } = "";
        public bool Done { get; set; }
    }
}
