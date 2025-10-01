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
    /// Check if the LLM is available and ready
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if available, false otherwise</returns>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get information about this LLM provider
    /// </summary>
    LLMProviderInfo ProviderInfo { get; }
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
