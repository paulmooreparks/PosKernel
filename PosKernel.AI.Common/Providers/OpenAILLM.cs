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
/// OpenAI implementation of ILargeLanguageModel
/// ARCHITECTURAL PRINCIPLE: Direct OpenAI API implementation without MCP dependency
/// </summary>
public class OpenAILLM : ILargeLanguageModel
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAILLM> _logger;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _baseUrl;

    public OpenAILLM(
        HttpClient httpClient,
        ILogger<OpenAILLM> logger,
        string apiKey,
        string model,
        string baseUrl = "https://api.openai.com/v1")
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));

        // Configure HTTP client for OpenAI
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "PosKernel-AI/1.0.0");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public LLMProviderInfo ProviderInfo => new()
    {
        Name = "OpenAI",
        Model = _model,
        Provider = "OpenAI API",
        IsLocal = false,
        HasRateLimits = true,
        Description = $"OpenAI {_model} via direct API"
    };

    public async Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
    {
        try
        {
            var requestBody = new
            {
                model = _model,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                },
                temperature = 0.3,
                max_tokens = 1000
            };

            var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogDebug("Calling OpenAI API: {Model}", _model);

            var response = await _httpClient.PostAsync($"{_baseUrl}/chat/completions", content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    throw new InvalidOperationException(
                        $"DESIGN DEFICIENCY: OpenAI API authentication failed. " +
                        $"Check OPENAI_API_KEY configuration. " +
                        $"Error: {errorContent}");
                }
                
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    throw new InvalidOperationException(
                        $"DESIGN DEFICIENCY: OpenAI API rate limit exceeded. " +
                        $"Consider using local LLM or upgrading OpenAI plan. " +
                        $"Error: {errorContent}");
                }
                
                throw new InvalidOperationException(
                    $"DESIGN DEFICIENCY: OpenAI API call failed with status {response.StatusCode}. " +
                    $"Response: {errorContent}");
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(responseJson);
            
            var choices = document.RootElement.GetProperty("choices");
            var firstChoice = choices.EnumerateArray().FirstOrDefault();
            var message = firstChoice.GetProperty("message");
            var responseContent = message.GetProperty("content").GetString();

            if (string.IsNullOrEmpty(responseContent))
            {
                throw new InvalidOperationException(
                    $"DESIGN DEFICIENCY: OpenAI returned empty response. " +
                    $"Check model access and API configuration for {_model}.");
            }

            _logger.LogDebug("OpenAI response received: {CharCount} characters", responseContent.Length);
            return responseContent;
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                $"DESIGN DEFICIENCY: Failed to connect to OpenAI API. " +
                $"Check network connectivity and API endpoint. " +
                $"Error: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            throw new InvalidOperationException(
                $"DESIGN DEFICIENCY: OpenAI API request timed out. " +
                $"Network or service may be slow. " +
                $"Error: {ex.Message}", ex);
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Test with a minimal prompt to check availability
            var testResponse = await GenerateAsync("test", cancellationToken);
            return !string.IsNullOrEmpty(testResponse);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenAI not available");
            return false;
        }
    }
}
