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

namespace PosKernel.AI.Common.Abstractions;

/// <summary>
/// Clean abstraction for Large Language Model interactions
/// ARCHITECTURAL PRINCIPLE: Unified interface for OpenAI, Ollama, and other LLMs
/// </summary>
public interface ILargeLanguageModel
{
    /// <summary>
    /// Generate a response from the LLM
    /// </summary>
    /// <param name="prompt">The prompt to send to the LLM</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The LLM's response</returns>
    Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate a response from the LLM with function calling support
    /// ARCHITECTURAL ENHANCEMENT: Native function calling for providers that support it
    /// </summary>
    /// <param name="prompt">The prompt to send to the LLM</param>
    /// <param name="tools">Available tools for function calling</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The LLM's response including any tool calls</returns>
    Task<LLMResponse> GenerateWithToolsAsync(string prompt, IReadOnlyList<LLMTool> tools, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if the LLM is available and ready
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if available, false otherwise</returns>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get information about this LLM provider
    /// </summary>
    LLMProviderInfo ProviderInfo { get; }

    /// <summary>
    /// Whether this provider supports native function calling
    /// </summary>
    bool SupportsFunctionCalling { get; }
}

/// <summary>
/// Information about an LLM provider
/// </summary>
public class LLMProviderInfo
{
    public required string Name { get; init; }
    public required string Model { get; init; }
    public required string Provider { get; init; }
    public bool IsLocal { get; init; }
    public bool HasRateLimits { get; init; }
    public string? Description { get; init; }
}

/// <summary>
/// Enhanced response from LLM that may include function calls
/// ARCHITECTURAL ENHANCEMENT: Supports both text responses and structured tool calls
/// </summary>
public class LLMResponse
{
    /// <summary>
    /// The text content of the response
    /// </summary>
    public string Content { get; set; } = "";

    /// <summary>
    /// Any tool calls the LLM wants to make
    /// </summary>
    public List<LLMToolCall> ToolCalls { get; set; } = new();

    /// <summary>
    /// Whether the LLM made any tool calls
    /// </summary>
    public bool HasToolCalls => ToolCalls.Count > 0;
}

/// <summary>
/// Represents a tool/function that can be called by the LLM
/// ARCHITECTURAL ENHANCEMENT: Standardized tool definition across providers
/// </summary>
public class LLMTool
{
    /// <summary>
    /// The name of the tool/function
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Description of what the tool does
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// JSON schema for the tool parameters
    /// </summary>
    public object Parameters { get; set; } = new();
}

/// <summary>
/// Represents a specific tool call requested by the LLM
/// ARCHITECTURAL ENHANCEMENT: Structured tool call with proper typing
/// </summary>
public class LLMToolCall
{
    /// <summary>
    /// Unique identifier for this tool call
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    /// The name of the function to call
    /// </summary>
    public string FunctionName { get; set; } = "";

    /// <summary>
    /// The arguments to pass to the function (as JSON)
    /// </summary>
    public string Arguments { get; set; } = "";

    /// <summary>
    /// Parsed arguments as a dictionary
    /// </summary>
    public Dictionary<string, object> ParsedArguments { get; set; } = new();
}
