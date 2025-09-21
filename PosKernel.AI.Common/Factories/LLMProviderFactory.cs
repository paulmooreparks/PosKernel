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
using PosKernel.AI.Common.Providers;

namespace PosKernel.AI.Common.Factories;

/// <summary>
/// Factory for creating LLM implementations based on provider parameters
/// ARCHITECTURAL PRINCIPLE: Factory accepts parameters from calling code, does not read environment variables
/// </summary>
public static class LLMProviderFactory
{
    /// <summary>
    /// Creates an LLM implementation based on provider name and configuration parameters
    /// ARCHITECTURAL PRINCIPLE: Environment variables are read by CLIENT code and passed as parameters
    /// </summary>
    /// <param name="provider">Provider name (e.g., "Ollama", "OpenAI")</param>
    /// <param name="model">Model name (e.g., "llama3.1:8b", "gpt-4o-mini")</param>
    /// <param name="baseUrl">Base URL for the provider API</param>
    /// <param name="apiKey">API key (required for OpenAI, ignored for Ollama)</param>
    /// <param name="logger">Logger instance</param>
    /// <returns>Configured ILargeLanguageModel instance</returns>
    /// <exception cref="InvalidOperationException">Thrown when provider is not supported</exception>
    public static ILargeLanguageModel CreateLLM(
        string provider,
        string model,
        string baseUrl,
        string? apiKey,
        ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new ArgumentException("Provider name cannot be null or empty", nameof(provider));
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new ArgumentException("Model name cannot be null or empty", nameof(model));
        }

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new ArgumentException("Base URL cannot be null or empty", nameof(baseUrl));
        }

        // FACTORY PATTERN: Switch on provider name to create appropriate implementation
        return provider.ToUpperInvariant() switch
        {
            "OLLAMA" => CreateOllamaLLM(model, baseUrl, logger),
            "OPENAI" => CreateOpenAILLM(model, baseUrl, apiKey, logger),
            _ => throw new InvalidOperationException(
                $"DESIGN DEFICIENCY: Unsupported AI provider '{provider}'. " +
                $"Supported providers: 'OpenAI', 'Ollama'. " +
                $"Check provider configuration in calling code.")
        };
    }

    private static ILargeLanguageModel CreateOllamaLLM(
        string model,
        string baseUrl,
        ILogger logger)
    {
        var httpClient = new HttpClient();
        var ollamaLogger = CreateTypedLogger<OllamaLLM>(logger);
        
        return new OllamaLLM(httpClient, ollamaLogger, baseUrl, model);
    }

    private static ILargeLanguageModel CreateOpenAILLM(
        string model,
        string baseUrl,
        string? apiKey,
        ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                $"DESIGN DEFICIENCY: OpenAI provider requires API key. " +
                $"Ensure calling code provides valid OPENAI_API_KEY value.");
        }

        var httpClient = new HttpClient();
        var openAILogger = CreateTypedLogger<OpenAILLM>(logger);
        
        return new OpenAILLM(httpClient, openAILogger, apiKey, model, baseUrl);
    }

    private static ILogger<T> CreateTypedLogger<T>(ILogger logger)
    {
        // Create a typed logger wrapper
        return new TypedLoggerWrapper<T>(logger);
    }

    /// <summary>
    /// Simple wrapper to convert ILogger to ILogger<T>
    /// </summary>
    private class TypedLoggerWrapper<T> : ILogger<T>
    {
        private readonly ILogger _logger;

        public TypedLoggerWrapper(ILogger logger)
        {
            _logger = logger;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => _logger.BeginScope(state);
        public bool IsEnabled(LogLevel logLevel) => _logger.IsEnabled(logLevel);
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _logger.Log(logLevel, eventId, state, exception, formatter);
        }
    }
}
