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

        // ARCHITECTURAL FIX: Set longer timeout for training scenarios
        // Training often involves longer, more complex prompts that take time to process
        _httpClient.Timeout = TimeSpan.FromMinutes(3); // Increased from 60 seconds to 3 minutes
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

    /// <summary>
    /// Ollama uses text-based function calling (no native support)
    /// </summary>
    public bool SupportsFunctionCalling => false;

    public async Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
    {
        const int maxRetries = 3;
        const int baseDelayMs = 1000;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
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

                _logger.LogDebug("Calling Ollama generate API: {Model} at {BaseUrl} (attempt {Attempt}/{MaxRetries})",
                    _model, _baseUrl, attempt, maxRetries);

                var response = await _httpClient.PostAsync($"{_baseUrl}/api/generate", content, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);

                    if (attempt == maxRetries)
                    {
                        throw new InvalidOperationException(
                            $"DESIGN DEFICIENCY: Ollama API call failed with status {response.StatusCode} after {maxRetries} attempts. " +
                            $"Response: {errorContent}. " +
                            $"Ensure Ollama is running and model '{_model}' is available.");
                    }

                    _logger.LogWarning("Ollama API call failed (attempt {Attempt}/{MaxRetries}): {Status} - {Error}",
                        attempt, maxRetries, response.StatusCode, errorContent);

                    // Wait before retry with exponential backoff
                    await Task.Delay(baseDelayMs * attempt, cancellationToken);
                    continue;
                }

                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                var ollamaResponse = JsonSerializer.Deserialize<OllamaGenerateResponse>(responseJson, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                if (string.IsNullOrEmpty(ollamaResponse?.Response))
                {
                    if (attempt == maxRetries)
                    {
                        throw new InvalidOperationException(
                            $"DESIGN DEFICIENCY: Ollama returned invalid response format after {maxRetries} attempts. " +
                            $"Expected 'response' field but got: {responseJson}");
                    }

                    _logger.LogWarning("Ollama returned empty response (attempt {Attempt}/{MaxRetries})", attempt, maxRetries);
                    await Task.Delay(baseDelayMs * attempt, cancellationToken);
                    continue;
                }

                _logger.LogDebug("Ollama response received: {CharCount} characters (attempt {Attempt})",
                    ollamaResponse.Response.Length, attempt);
                return ollamaResponse.Response;
            }
            catch (HttpRequestException ex)
            {
                if (attempt == maxRetries)
                {
                    throw new InvalidOperationException(
                        $"DESIGN DEFICIENCY: Cannot connect to Ollama at {_baseUrl} after {maxRetries} attempts. " +
                        $"Ensure Ollama is running and accessible. " +
                        $"Error: {ex.Message}", ex);
                }

                _logger.LogWarning(ex, "HTTP request failed (attempt {Attempt}/{MaxRetries}): {Error}",
                    attempt, maxRetries, ex.Message);
                await Task.Delay(baseDelayMs * attempt, cancellationToken);
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                if (attempt == maxRetries)
                {
                    throw new InvalidOperationException(
                        $"DESIGN DEFICIENCY: Ollama request timed out after {maxRetries} attempts. " +
                        $"Model '{_model}' may be slow to respond or not loaded. " +
                        $"Consider using a faster model or increasing timeout. " +
                        $"Error: {ex.Message}", ex);
                }

                _logger.LogWarning("Ollama request timed out (attempt {Attempt}/{MaxRetries})", attempt, maxRetries);
                await Task.Delay(baseDelayMs * attempt, cancellationToken);
            }
        }

        throw new InvalidOperationException($"DESIGN DEFICIENCY: All {maxRetries} attempts to call Ollama failed.");
    }

    /// <summary>
    /// Generate response with text-based function calling for Ollama
    /// ARCHITECTURAL FALLBACK: Uses text-based tool calls since Ollama doesn't have native function calling
    /// </summary>
    public async Task<LLMResponse> GenerateWithToolsAsync(string prompt, IReadOnlyList<LLMTool> tools, CancellationToken cancellationToken = default)
    {
        // For Ollama, we fall back to text-based tool calling
        // Build a prompt that instructs the model to use TOOL_CALL format
        var toolsDescription = string.Join("\n", tools.Select(t => $"- {t.Name}: {t.Description}"));
        var enhancedPrompt = $@"You have access to these tools:
{toolsDescription}

To call a tool, use this exact format:
TOOL_CALL: tool_name {{""param"": ""value""}}

User request: {prompt}";

        // Get text response from Ollama
        var textResponse = await GenerateAsync(enhancedPrompt, cancellationToken);

        // Parse tool calls from text response (reuse existing logic)
        var toolCalls = ExtractToolCallsFromText(textResponse, tools);

        return new LLMResponse
        {
            Content = textResponse,
            ToolCalls = toolCalls
        };
    }

    /// <summary>
    /// Extract tool calls from text response for Ollama
    /// ARCHITECTURAL PRINCIPLE: Reuse existing text parsing logic for consistency
    /// </summary>
    private List<LLMToolCall> ExtractToolCallsFromText(string response, IReadOnlyList<LLMTool> availableTools)
    {
        var toolCalls = new List<LLMToolCall>();

        if (string.IsNullOrEmpty(response))
        {
            return toolCalls;
        }

        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("TOOL_CALL:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                // Parse: "TOOL_CALL: tool_name {"param": "value"}"
                var toolCallText = trimmed.Substring("TOOL_CALL:".Length).Trim();
                var spaceIndex = toolCallText.IndexOf(' ');

                string toolName;
                string jsonArgs;

                if (spaceIndex == -1)
                {
                    // Handle case where there's only a tool name, no arguments
                    toolName = toolCallText;
                    jsonArgs = "{}";
                }
                else
                {
                    toolName = toolCallText.Substring(0, spaceIndex).Trim();
                    jsonArgs = toolCallText.Substring(spaceIndex + 1).Trim();
                }

                // Validate tool exists
                if (!availableTools.Any(t => t.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogWarning("Unknown tool called: {ToolName}", toolName);
                    continue;
                }

                // Parse JSON arguments
                var parsedArgs = new Dictionary<string, object>();
                try
                {
                    if (!string.IsNullOrWhiteSpace(jsonArgs) && jsonArgs != "()" && jsonArgs != "(empty)" && jsonArgs != "none")
                    {
                        using var argsDoc = JsonDocument.Parse(jsonArgs);
                        foreach (var prop in argsDoc.RootElement.EnumerateObject())
                        {
                            parsedArgs[prop.Name] = prop.Value.ToString();
                        }
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Failed to parse tool call arguments: {JsonArgs}", jsonArgs);
                    continue;
                }

                toolCalls.Add(new LLMToolCall
                {
                    Id = Guid.NewGuid().ToString(),
                    FunctionName = toolName,
                    Arguments = jsonArgs,
                    ParsedArguments = parsedArgs
                });

                _logger.LogDebug("Successfully parsed tool call: {ToolName} with {ArgCount} arguments", toolName, parsedArgs.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse tool call from line: {Line}", trimmed);
            }
        }

        return toolCalls;
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
