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
        Description = $"OpenAI {_model} via direct API with native function calling"
    };

    /// <summary>
    /// OpenAI supports native function calling
    /// </summary>
    public bool SupportsFunctionCalling => true;

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

    /// <summary>
    /// Generate response with native OpenAI function calling support
    /// ARCHITECTURAL ENHANCEMENT: Uses OpenAI's native function calling for reliable tool integration
    /// </summary>
    public async Task<LLMResponse> GenerateWithToolsAsync(string prompt, IReadOnlyList<LLMTool> tools, CancellationToken cancellationToken = default)
    {
        try
        {
            // Build the request with function calling support
            var requestBody = new
            {
                model = _model,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                },
                temperature = 0.3,
                max_tokens = 1000,
                tools = tools.Select(tool => new
                {
                    type = "function",
                    function = new
                    {
                        name = tool.Name,
                        description = tool.Description,
                        parameters = tool.Parameters
                    }
                }).ToArray(),
                tool_choice = "auto" // Let AI decide when to call functions
            };

            var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogDebug("Calling OpenAI API with {ToolCount} tools: {Model}", tools.Count, _model);

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

            // Extract text content
            var responseContent = "";
            if (message.TryGetProperty("content", out var contentElement) &&
                contentElement.ValueKind == JsonValueKind.String)
            {
                responseContent = contentElement.GetString() ?? "";
            }

            // Extract tool calls
            var toolCalls = new List<LLMToolCall>();
            if (message.TryGetProperty("tool_calls", out var toolCallsElement) &&
                toolCallsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var toolCallElement in toolCallsElement.EnumerateArray())
                {
                    var id = toolCallElement.GetProperty("id").GetString() ?? "";
                    var function = toolCallElement.GetProperty("function");
                    var functionName = function.GetProperty("name").GetString() ?? "";
                    var arguments = function.GetProperty("arguments").GetString() ?? "";

                    // Parse arguments into dictionary
                    var parsedArgs = new Dictionary<string, object>();
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(arguments))
                        {
                            using var argsDoc = JsonDocument.Parse(arguments);
                            foreach (var prop in argsDoc.RootElement.EnumerateObject())
                            {
                                parsedArgs[prop.Name] = prop.Value.ToString();
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse tool call arguments: {Arguments}", arguments);
                    }

                    toolCalls.Add(new LLMToolCall
                    {
                        Id = id,
                        FunctionName = functionName,
                        Arguments = arguments,
                        ParsedArguments = parsedArgs
                    });
                }
            }

            var result = new LLMResponse
            {
                Content = responseContent,
                ToolCalls = toolCalls
            };

            _logger.LogDebug("OpenAI response: {CharCount} characters, {ToolCallCount} tool calls",
                responseContent.Length, toolCalls.Count);

            if (toolCalls.Any())
            {
                _logger.LogInformation("OpenAI requested {ToolCallCount} native function calls: {Functions}",
                    toolCalls.Count, string.Join(", ", toolCalls.Select(tc => tc.FunctionName)));
            }

            return result;
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
