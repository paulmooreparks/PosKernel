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
using PosKernel.Configuration;

namespace PosKernel.AI.Services
{
    /// <summary>
    /// Model Context Protocol (MCP) client for structured AI tool calling.
    /// Provides type-safe function calling interface between AI and POS operations.
    /// </summary>
    public class McpClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<McpClient> _logger;
        private readonly McpConfiguration _config;

        /// <summary>
        /// Initializes a new instance of the McpClient.
        /// </summary>
        public McpClient(ILogger<McpClient> logger, McpConfiguration config)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config ?? throw new ArgumentNullException(nameof(config));

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.ApiKey}");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "PosKernel-MCP/0.5.0");
            _httpClient.Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds);
        }

        /// <summary>
        /// Sends an MCP completion request with tool calling capabilities.
        /// </summary>
        public async Task<McpResponse> CompleteWithToolsAsync(string prompt, IReadOnlyList<McpTool> availableTools, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = new
                {
                    model = _config.DefaultParameters.GetValueOrDefault("model", "gpt-4o"),
                    messages = new[]
                    {
                        new { role = "system", content = "You are an AI assistant for a Point-of-Sale system. Use the provided tools to help with transactions and customer service. Only use tools when necessary to complete the user's request." },
                        new { role = "user", content = prompt }
                    },
                    tools = availableTools.Select(tool => new
                    {
                        type = "function",
                        function = new
                        {
                            name = tool.Name,
                            description = tool.Description,
                            parameters = tool.Parameters
                        }
                    }).ToArray(),
                    tool_choice = "auto",
                    temperature = _config.DefaultParameters.GetValueOrDefault("temperature", 0.3),
                    max_tokens = _config.DefaultParameters.GetValueOrDefault("max_tokens", 1000)
                };

                var jsonRequest = JsonSerializer.Serialize(request, _config.JsonOptions);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                _logger.LogDebug("Sending MCP request with {ToolCount} tools: {Prompt}", availableTools.Count, prompt);

                var url = $"{_config.BaseUrl.TrimEnd('/')}/{_config.CompletionEndpoint}";
                var response = await _httpClient.PostAsync(url, content, cancellationToken);
                response.EnsureSuccessStatusCode();

                var jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
                using var document = JsonDocument.Parse(jsonResponse);
                
                return ParseMcpResponse(document);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling MCP API");
                return new McpResponse
                {
                    Content = $"AI service error: {ex.Message}",
                    ToolCalls = new List<McpToolCall>()
                };
            }
        }

        private McpResponse ParseMcpResponse(JsonDocument document)
        {
            var choices = document.RootElement.GetProperty("choices");
            var firstChoice = choices.EnumerateArray().FirstOrDefault();
            var message = firstChoice.GetProperty("message");
            
            var content = message.TryGetProperty("content", out var contentProp) ? contentProp.GetString() : "";
            var toolCalls = new List<McpToolCall>();

            if (message.TryGetProperty("tool_calls", out var toolCallsProp))
            {
                foreach (var toolCall in toolCallsProp.EnumerateArray())
                {
                    var function = toolCall.GetProperty("function");
                    var functionName = function.GetProperty("name").GetString()!;
                    var argumentsJson = function.GetProperty("arguments").GetString()!;
                    
                    toolCalls.Add(new McpToolCall
                    {
                        Id = toolCall.GetProperty("id").GetString()!,
                        FunctionName = functionName,
                        Arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(argumentsJson) ?? new()
                    });
                }
            }

            return new McpResponse
            {
                Content = content ?? "",
                ToolCalls = toolCalls
            };
        }

        /// <summary>
        /// Disposes the HTTP client and releases all resources.
        /// </summary>
        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    /// <summary>
    /// Represents an MCP tool/function definition for AI tool calling.
    /// </summary>
    public class McpTool
    {
        /// <summary>
        /// Gets or sets the tool name.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Gets or sets the tool description.
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// Gets or sets the JSON schema for the tool parameters.
        /// </summary>
        public object Parameters { get; set; } = new();
    }

    /// <summary>
    /// Represents a tool call request from the AI.
    /// </summary>
    public class McpToolCall
    {
        /// <summary>
        /// Gets or sets the tool call ID.
        /// </summary>
        public string Id { get; set; } = "";

        /// <summary>
        /// Gets or sets the function name to call.
        /// </summary>
        public string FunctionName { get; set; } = "";

        /// <summary>
        /// Gets or sets the function arguments.
        /// </summary>
        public Dictionary<string, JsonElement> Arguments { get; set; } = new();
    }

    /// <summary>
    /// Represents an MCP response from the AI.
    /// </summary>
    public class McpResponse
    {
        /// <summary>
        /// Gets or sets the AI's text response.
        /// </summary>
        public string Content { get; set; } = "";

        /// <summary>
        /// Gets or sets any tool calls the AI wants to make.
        /// </summary>
        public List<McpToolCall> ToolCalls { get; set; } = new();
    }
}
