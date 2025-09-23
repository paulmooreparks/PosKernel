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
        /// Sends an MCP completion request with tool calling capabilities.
        /// ARCHITECTURAL FIX: Implements text-based tool calling for local providers like Ollama
        /// </summary>
        public async Task<McpResponse> CompleteWithToolsAsync(string prompt, IReadOnlyList<McpTool> availableTools, CancellationToken cancellationToken = default)
        {
            try
            {
                // Build the prompt with tool descriptions for local providers
                var enhancedPrompt = BuildToolAwarePrompt(prompt, availableTools);

                // ARCHITECTURAL FIX: Use shared LLM interface for all providers
                var textResponse = await _llm.GenerateAsync(enhancedPrompt, cancellationToken);
                
                _logger.LogDebug("LLM response received: {Length} characters from {Provider}", textResponse.Length, _llm.ProviderInfo.Name);

                // ARCHITECTURAL FIX: Parse tool calls from text for local providers, structured for others
                var toolCalls = ExtractToolCallsFromText(textResponse, availableTools);

                if (toolCalls.Any())
                {
                    _logger.LogInformation("Extracted {ToolCount} tool calls from {Provider} response", toolCalls.Count, _llm.ProviderInfo.Name);
                    foreach (var tool in toolCalls)
                    {
                        _logger.LogDebug("Tool call: {FunctionName} with {ArgCount} arguments", tool.FunctionName, tool.Arguments.Count);
                    }
                }

                return new McpResponse
                {
                    Content = textResponse,
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
        /// ARCHITECTURAL FIX: Build tool-aware prompt that instructs the LLM how to call tools via text
        /// </summary>
        private string BuildToolAwarePrompt(string prompt, IReadOnlyList<McpTool> availableTools)
        {
            if (!availableTools.Any())
            {
                return prompt;
            }

            var toolDescriptions = string.Join("\n", availableTools.Select(tool => 
                $"- {tool.Name}: {tool.Description}"));

            return $@"You are an AI assistant for a Point-of-Sale system. You have access to these tools:

{toolDescriptions}

CRITICAL TOOL CALLING INSTRUCTIONS:
When you need to use a tool, include a line in your response that starts with ""TOOL_CALL:"" followed by the tool name and JSON arguments.
Format: TOOL_CALL: tool_name {{""parameter"": ""value""}}

For example:
- To add an item: TOOL_CALL: add_item_to_transaction {{""item_description"": ""Kopi C"", ""quantity"": 1}}
- To search products: TOOL_CALL: search_products {{""search_term"": ""coffee""}}
- To process payment: TOOL_CALL: process_payment {{""payment_method"": ""cash""}}

CRITICAL: Use exact parameter names from tool definitions:
- add_item_to_transaction requires ""item_description"" (NOT ""product_name"")
- search_products requires ""search_term""
- process_payment requires ""payment_method""

You MUST provide a conversational response alongside any tool calls. Always respond naturally to show you understand and completed the action.

User request: {prompt}";
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
