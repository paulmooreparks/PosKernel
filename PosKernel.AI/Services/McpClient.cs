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
using PosKernel.AI.Common.Abstractions;
using PosKernel.Abstractions;

namespace PosKernel.AI.Services
{
    /// <summary>
    /// Model Context Protocol (MCP) client for structured AI tool calling.
    /// ARCHITECTURAL FIX: Now uses shared ILargeLanguageModel interface instead of direct HTTP calls
    /// </summary>
    public class McpClient : IDisposable
    {
        private readonly ILargeLanguageModel _llm;
        private readonly ILogger<McpClient> _logger;
        private readonly McpConfiguration _config;

        /// <summary>
        /// Initializes a new instance of the McpClient.
        /// ARCHITECTURAL PRINCIPLE: Uses shared LLM interface instead of hardcoded HTTP client
        /// </summary>
        public McpClient(ILogger<McpClient> logger, McpConfiguration config, ILargeLanguageModel llm)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        }

        /// <summary>
        /// Pure tool completion with minimal infrastructure - no prompt building.
        /// ARCHITECTURAL PRINCIPLE: AI knows how to use tools naturally.
        /// </summary>
        public async Task<McpResponse> CompleteWithToolsAsync(string prompt, IReadOnlyList<McpTool> availableTools, CancellationToken cancellationToken = default)
        {
            try
            {
                // ARCHITECTURAL PRINCIPLE: Pure context injection - AI has native tool understanding
                var toolsData = availableTools.Any() ? JsonSerializer.Serialize(availableTools, new JsonSerializerOptions { WriteIndented = true }) : "";
                var enhancedInput = $"TOOLS: {toolsData}\nUSER: {prompt}";

                // ARCHITECTURAL FIX: Use shared LLM interface for all providers
                var textResponse = await _llm.GenerateAsync(enhancedInput, cancellationToken);

                _logger.LogDebug("LLM response received: {Length} characters from {Provider}", textResponse.Length, _llm.ProviderInfo.Name);

                // ARCHITECTURAL FIX: Pure tool extraction - no interpretation logic
                var toolCalls = ExtractToolCallsFromText(textResponse, availableTools);

                if (toolCalls.Any())
                {
                    _logger.LogInformation("Extracted {ToolCount} tool calls from {Provider} response", toolCalls.Count, _llm.ProviderInfo.Name);
                    foreach (var tool in toolCalls)
                    {
                        _logger.LogDebug("Tool call: {FunctionName} with {ArgCount} arguments", tool.FunctionName, tool.Arguments.Count);
                    }
                }

                // Pure text cleaning - remove infrastructure markers only
                var sanitizedContent = SanitizeAssistantContent(textResponse);

                return new McpResponse
                {
                    Content = sanitizedContent,
                    ToolCalls = toolCalls
                };
            }
            catch (Exception ex)
            {
                // ARCHITECTURAL PRINCIPLE: Fail fast - don't provide fallback responses
                _logger.LogError(ex, "Critical error calling LLM via shared interface");
                throw new InvalidOperationException(
                    "DESIGN DEFICIENCY: LLM service call failed and system cannot continue without AI response. " +
                    $"Error: {ex.Message}. " +
                    "Check LLM provider configuration and service availability.", ex);
            }
        }

        /// <summary>
        /// AI AGENT VERSION: Pure context injection without prompt building.
        /// ARCHITECTURAL REDESIGN: AI has persistent knowledge and natural tool access.
        /// ENHANCED: Now uses native function calling when provider supports it.
        /// </summary>
        public async Task<McpResponse> CompleteWithContextAsync(string userInput, IReadOnlyList<McpTool> availableTools, object? persistentContext = null, CancellationToken cancellationToken = default)
        {
            try
            {
                // ARCHITECTURAL ENHANCEMENT: Use native function calling if provider supports it
                if (_llm.SupportsFunctionCalling && availableTools.Any())
                {
                    return await CompleteWithNativeFunctionCallingAsync(userInput, availableTools, persistentContext, cancellationToken);
                }
                else
                {
                    return await CompleteWithTextBasedToolsAsync(userInput, availableTools, persistentContext, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error in AI agent completion");
                throw new InvalidOperationException(
                    "DESIGN DEFICIENCY: AI agent completion failed. " +
                    $"Error: {ex.Message}. " +
                    "Check LLM provider configuration and service availability.", ex);
            }
        }

        /// <summary>
        /// Use native function calling (OpenAI-style)
        /// ARCHITECTURAL ENHANCEMENT: Better reliability and structured responses
        /// </summary>
        private async Task<McpResponse> CompleteWithNativeFunctionCallingAsync(string userInput, IReadOnlyList<McpTool> availableTools, object? persistentContext, CancellationToken cancellationToken)
        {
            // ARCHITECTURAL PRINCIPLE: Minimal context injection - AI has native tool understanding
            var contextData = persistentContext != null ? JsonSerializer.Serialize(persistentContext, new JsonSerializerOptions { WriteIndented = true }) : "";

            // Convert MCP tools to LLM tools
            var llmTools = availableTools.Select(tool => new LLMTool
            {
                Name = tool.Name,
                Description = tool.Description,
                Parameters = tool.Parameters
            }).ToList();

            // Enhanced prompt with context
            var enhancedInput = string.IsNullOrEmpty(contextData) ? userInput : $"CONTEXT: {contextData}\nUSER: {userInput}";

            // ARCHITECTURAL FIX: Use native function calling
            var llmResponse = await _llm.GenerateWithToolsAsync(enhancedInput, llmTools, cancellationToken);

            _logger.LogDebug("Native function calling response received: {Length} characters from {Provider}",
                llmResponse.Content.Length, _llm.ProviderInfo.Name);

            // Convert LLM tool calls to MCP tool calls
            var mcpToolCalls = llmResponse.ToolCalls.Select(tc => new McpToolCall
            {
                Id = tc.Id,
                FunctionName = tc.FunctionName,
                Arguments = ParseArgumentsFromJson(tc.Arguments)
            }).ToList();

            if (mcpToolCalls.Any())
            {
                _logger.LogInformation("Native function calling: AI requested {ToolCount} tool calls", mcpToolCalls.Count);
                foreach (var tool in mcpToolCalls)
                {
                    _logger.LogDebug("Native tool call: {FunctionName} with {ArgCount} arguments", tool.FunctionName, tool.Arguments.Count);
                }
            }

            // Return clean response - tool calls execute separately
            var sanitizedContent = SanitizeAssistantContent(llmResponse.Content);

            return new McpResponse
            {
                Content = sanitizedContent,
                ToolCalls = mcpToolCalls
            };
        }

        /// <summary>
        /// Use text-based tool calling (Ollama-style)
        /// ARCHITECTURAL FALLBACK: For providers without native function calling
        /// </summary>
        private async Task<McpResponse> CompleteWithTextBasedToolsAsync(string userInput, IReadOnlyList<McpTool> availableTools, object? persistentContext, CancellationToken cancellationToken)
        {
            // ARCHITECTURAL PRINCIPLE: Minimal context injection - AI has native tool understanding
            var contextData = persistentContext != null ? JsonSerializer.Serialize(persistentContext, new JsonSerializerOptions { WriteIndented = true }) : "";

            // ARCHITECTURAL PRINCIPLE: Simple tool definitions - AI knows how to use tools
            var toolsData = availableTools.Any() ? JsonSerializer.Serialize(availableTools, new JsonSerializerOptions { WriteIndented = true }) : "";

            // ARCHITECTURAL FIX: Instruct AI on tool call format for text-based parsing
            var toolInstructions = availableTools.Any() ?
                "\n\nTo call tools, use format: TOOL_CALL: tool_name {\"param\": \"value\"}\nExample: TOOL_CALL: add_line_item {\"sku\": \"KOPI001\", \"quantity\": 1}" : "";

            // ARCHITECTURAL PRINCIPLE: Pure user input with minimal infrastructure context
            var enhancedInput = $"CONTEXT: {contextData}\nTOOLS: {toolsData}{toolInstructions}\nUSER: {userInput}";

            // ARCHITECTURAL FIX: Use shared LLM interface for all providers
            var textResponse = await _llm.GenerateAsync(enhancedInput, cancellationToken);

            _logger.LogDebug("Text-based response received: {Length} characters from {Provider}", textResponse.Length, _llm.ProviderInfo.Name);

            // ARCHITECTURAL PRINCIPLE: Pure tool extraction - no interpretation logic
            var toolCalls = ExtractToolCallsFromText(textResponse, availableTools);

            if (toolCalls.Any())
            {
                _logger.LogInformation("Text-based parsing: AI requested {ToolCount} tool calls", toolCalls.Count);
            }

            // Return clean response - tool calls execute separately
            var sanitizedContent = SanitizeAssistantContent(textResponse);

            return new McpResponse
            {
                Content = sanitizedContent,
                ToolCalls = toolCalls
            };
        }

        /// <summary>
        /// Parse JSON arguments string into dictionary format
        /// ARCHITECTURAL HELPER: Convert between LLM and MCP formats
        /// </summary>
        private Dictionary<string, JsonElement> ParseArgumentsFromJson(string jsonArgs)
        {
            var arguments = new Dictionary<string, JsonElement>();

            try
            {
                if (!string.IsNullOrWhiteSpace(jsonArgs))
                {
                    using var doc = JsonDocument.Parse(jsonArgs);
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        arguments[prop.Name] = prop.Value.Clone();
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse tool call arguments: {JsonArgs}", jsonArgs);
            }

            return arguments;
        }

        /// <summary>
        /// ARCHITECTURAL PRINCIPLE: Pure text sanitization - remove infrastructure markers only.
        /// No business logic or interpretation - just clean output for customer.
        /// </summary>
        private static string SanitizeAssistantContent(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            using var reader = new StringReader(response);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                var trim = line.TrimStart();
                // Remove only infrastructure markers - no business logic
                if (trim.StartsWith("TOOL_CALL:", StringComparison.OrdinalIgnoreCase)) { continue; }
                if (trim.StartsWith("TOOL_RESULT:", StringComparison.OrdinalIgnoreCase)) { continue; }
                if (trim.StartsWith("CONTEXT:", StringComparison.OrdinalIgnoreCase)) { continue; }
                if (trim.StartsWith("TOOLS:", StringComparison.OrdinalIgnoreCase)) { continue; }
                sb.AppendLine(line);
            }
            return sb.ToString().Trim();
        }

        /// <summary>
        /// ARCHITECTURAL FIX: Extract tool calls from text response for local providers
        /// </summary>
        private List<McpToolCall> ExtractToolCallsFromText(string response, IReadOnlyList<McpTool> availableTools)
        {
            var toolCalls = new List<McpToolCall>();

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
                        _logger.LogDebug("Tool call without arguments: {ToolName}", toolName);
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
                    Dictionary<string, JsonElement> arguments;
                    try
                    {
                        // ARCHITECTURAL FIX: Handle common invalid JSON patterns from AI responses
                        if (string.IsNullOrWhiteSpace(jsonArgs) || jsonArgs == "()" || jsonArgs == "(empty)" || jsonArgs == "none")
                        {
                            arguments = new Dictionary<string, JsonElement>();
                            _logger.LogDebug("Tool call {ToolName} has no arguments (empty/null/parentheses)", toolName);
                        }
                        else
                        {
                            arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonArgs) ?? new();
                        }
                    }
                    catch (JsonException ex)
                    {
                        // ARCHITECTURAL PRINCIPLE: FAIL FAST - Don't continue with corrupted tool calls
                        _logger.LogError(ex, "CRITICAL: Tool argument parsing failed - AI returned invalid JSON: {JsonArgs}", jsonArgs);
                        throw new InvalidOperationException(
                            $"DESIGN DEFICIENCY: AI tool calling failed due to invalid JSON in tool arguments. " +
                            $"Tool: {toolName}, Invalid JSON: '{jsonArgs}', Error: {ex.Message}. " +
                            $"This indicates a problem with AI prompt engineering or model configuration. " +
                            $"Fix the AI model configuration or prompt to generate valid JSON tool arguments.");
                    }

                    toolCalls.Add(new McpToolCall
                    {
                        Id = Guid.NewGuid().ToString(),
                        FunctionName = toolName,
                        Arguments = arguments
                    });

                    _logger.LogDebug("Successfully parsed tool call: {ToolName} with {ArgCount} arguments", toolName, arguments.Count);
                }
                catch (Exception ex)
                {
                    // ARCHITECTURAL PRINCIPLE: FAIL FAST - Don't continue with corrupted tool parsing
                    _logger.LogError(ex, "CRITICAL: Tool call parsing failed unexpectedly for line: {Line}", trimmed);
                    throw new InvalidOperationException(
                        $"DESIGN DEFICIENCY: AI tool call parsing failed unexpectedly. " +
                        $"Line: '{trimmed}', Error: {ex.GetType().Name}: {ex.Message}. " +
                        $"This indicates a fundamental problem with AI response parsing. " +
                        $"Check AI model configuration and prompt engineering.");
                }
            }

            return toolCalls;
        }

        /// <summary>
        /// Disposes resources - no HTTP client to dispose anymore
        /// </summary>
        public void Dispose()
        {
            // ARCHITECTURAL FIX: No HTTP client to dispose - LLM interface handles its own resources
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
