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

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace PosKernel.Configuration
{
    /// <summary>
    /// Centralized configuration system for PosKernel and all its modules.
    /// Supports XferLang configuration files with environment variable overrides.
    /// </summary>
    public class PosKernelConfiguration
    {
        private readonly Dictionary<string, object> _configuration = new();
        private readonly List<string> _configSources = new();

        /// <summary>
        /// Gets the configuration directory path (~/.poskernel).
        /// </summary>
        public static string ConfigDirectory
        {
            get
            {
                var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(homeDir, ".poskernel");
            }
        }

        /// <summary>
        /// Gets the secrets file path (~/.poskernel/.env).
        /// </summary>
        public static string SecretsFilePath => Path.Combine(ConfigDirectory, ".env");

        /// <summary>
        /// Gets the main configuration file path (~/.poskernel/config.xfer).
        /// </summary>
        public static string ConfigFilePath => Path.Combine(ConfigDirectory, "config.xfer");

        /// <summary>
        /// Gets the AI-specific configuration file path (~/.poskernel/ai.xfer).
        /// </summary>
        public static string AiConfigFilePath => Path.Combine(ConfigDirectory, "ai.xfer");

        /// <summary>
        /// Initializes the configuration system by loading from multiple sources.
        /// </summary>
        public static PosKernelConfiguration Initialize()
        {
            var config = new PosKernelConfiguration();
            config.LoadConfiguration();
            return config;
        }

        /// <summary>
        /// Gets a configuration value with optional type conversion.
        /// </summary>
        public T? GetValue<T>(string key, T? defaultValue = default)
        {
            if (_configuration.TryGetValue(key, out var value))
            {
                try
                {
                    if (value is T directValue)
                    {
                        return directValue;
                    }

                    // Handle string to other type conversions
                    if (value is string stringValue && typeof(T) != typeof(string))
                    {
                        return ConvertStringValue<T>(stringValue);
                    }
                    
                    // Handle JSON element conversions (from XferLang parsing)
                    if (value is JsonElement jsonElement)
                    {
                        return ConvertJsonElement<T>(jsonElement);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Failed to convert config value '{key}': {ex.Message}");
                }
            }

            return defaultValue;
        }

        /// <summary>
        /// Gets a configuration section as a dictionary.
        /// </summary>
        public Dictionary<string, object> GetSection(string sectionKey)
        {
            var section = new Dictionary<string, object>();
            var prefix = sectionKey + ":";

            foreach (var kvp in _configuration)
            {
                if (kvp.Key.StartsWith(prefix))
                {
                    var subKey = kvp.Key.Substring(prefix.Length);
                    section[subKey] = kvp.Value;
                }
            }

            return section;
        }

        /// <summary>
        /// Sets a configuration value (useful for testing or runtime overrides).
        /// </summary>
        public void SetValue(string key, object value)
        {
            _configuration[key] = value;
        }

        /// <summary>
        /// Gets information about which sources were loaded.
        /// </summary>
        public IReadOnlyList<string> ConfigSources => _configSources.AsReadOnly();

        private void LoadConfiguration()
        {
            // 1. Load default configuration
            LoadDefaults();

            // 2. Ensure config directory exists
            EnsureConfigDirectory();

            // 3. Load main configuration file (XferLang)
            LoadXferLangConfig(ConfigFilePath, "Main Config");

            // 4. Load AI-specific configuration (XferLang)
            LoadXferLangConfig(AiConfigFilePath, "AI Config");

            // 5. Load environment variables from .env file
            LoadEnvFile();

            // 6. Load environment variables (highest priority)
            LoadEnvironmentVariables();
        }

        private void LoadDefaults()
        {
            // Core PosKernel defaults
            _configuration["PosKernel:LogLevel"] = "Information";
            _configuration["PosKernel:CultureCode"] = "en-US";
            _configuration["PosKernel:DefaultCurrency"] = "USD";
            
            // ARCHITECTURAL PRINCIPLE: NO AI defaults - require explicit configuration
            // AI configuration must be provided via environment variables to enforce fail-fast
            
            // Security defaults
            _configuration["Security:EnableAuditLogging"] = true;
            _configuration["Security:LogLevel"] = "High";
            
            _configSources.Add("Defaults");
        }

        private void EnsureConfigDirectory()
        {
            if (!Directory.Exists(ConfigDirectory))
            {
                Directory.CreateDirectory(ConfigDirectory);
                Console.WriteLine($"Created PosKernel config directory: {ConfigDirectory}");
            }
        }

        private void LoadXferLangConfig(string filePath, string sourceName)
        {
            if (File.Exists(filePath))
            {
                try
                {
                    var content = File.ReadAllText(filePath);
                    var config = ParseXferLang(content);
                    
                    foreach (var kvp in config)
                    {
                        _configuration[kvp.Key] = kvp.Value;
                    }
                    
                    _configSources.Add($"{sourceName} ({filePath})");
                }
                catch (Exception ex)
                {
                    // ARCHITECTURAL PRINCIPLE: Fail fast on configuration loading errors
                    throw new InvalidOperationException(
                        $"DESIGN DEFICIENCY: Failed to load {sourceName} configuration from {filePath}. " +
                        $"Configuration loading must succeed for system to operate correctly. " +
                        $"Error: {ex.Message}", ex);
                }
            }
        }

        private void LoadEnvFile()
        {
            if (File.Exists(SecretsFilePath))
            {
                try
                {
                    var lines = File.ReadAllLines(SecretsFilePath);
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                        {
                            continue;
                        }

                        var parts = trimmed.Split('=', 2);
                        if (parts.Length == 2)
                        {
                            var key = parts[0].Trim();
                            var value = parts[1].Trim().Trim('"', '\'');
                            _configuration[key] = value;
                        }
                    }
                    
                    _configSources.Add($"Environment File ({SecretsFilePath})");
                }
                catch (Exception ex)
                {
                    // ARCHITECTURAL PRINCIPLE: Fail fast on secrets loading errors
                    throw new InvalidOperationException(
                        $"DESIGN DEFICIENCY: Failed to load secrets from {SecretsFilePath}. " +
                        $"Secrets file must be readable for system to operate correctly. " +
                        $"Error: {ex.Message}", ex);
                }
            }
        }

        private void LoadEnvironmentVariables()
        {
            // Load well-known environment variables
            var envVars = new[]
            {
                "OPENAI_API_KEY",
                "STORE_AI_PROVIDER",
                "STORE_AI_MODEL", 
                "STORE_AI_BASE_URL",
                "TRAINING_AI_PROVIDER",
                "TRAINING_AI_MODEL",
                "TRAINING_AI_BASE_URL",
                "POSKERNEL_LOG_LEVEL",
                "POSKERNEL_CULTURE",
                "POSKERNEL_CURRENCY",
                "USE_MOCK_AI"
            };

            foreach (var envVar in envVars)
            {
                var value = Environment.GetEnvironmentVariable(envVar);
                if (!string.IsNullOrEmpty(value))
                {
                    _configuration[envVar] = value;
                }
            }
            
            _configSources.Add("Environment Variables");
        }

        private Dictionary<string, object> ParseXferLang(string content)
        {
            // For now, we'll use a simple JSON parser as a placeholder for XferLang
            // TODO: Replace with actual XferLang parser when available
            
            var result = new Dictionary<string, object>();
            
            try
            {
                // Try to parse as JSON first (for compatibility)
                var jsonDocument = JsonDocument.Parse(content);
                FlattenJsonElement(jsonDocument.RootElement, result, "");
            }
            catch (JsonException)
            {
                // If JSON parsing fails, try simple key-value parsing
                ParseKeyValueFormat(content, result);
            }
            
            return result;
        }

        private void FlattenJsonElement(JsonElement element, Dictionary<string, object> result, string prefix)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        var key = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}:{property.Name}";
                        FlattenJsonElement(property.Value, result, key);
                    }
                    break;
                    
                case JsonValueKind.Array:
                    for (int i = 0; i < element.GetArrayLength(); i++)
                    {
                        var key = $"{prefix}[{i}]";
                        FlattenJsonElement(element[i], result, key);
                    }
                    break;
                    
                default:
                    result[prefix] = element;
                    break;
            }
        }

        private void ParseKeyValueFormat(string content, Dictionary<string, object> result)
        {
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith("//"))
                {
                    continue;
                }

                var parts = trimmed.Split('=', 2);
                if (parts.Length == 2)
                {
                    var key = parts[0].Trim();
                    var value = parts[1].Trim().Trim('"', '\'');
                    result[key] = value;
                }
            }
        }

        private T? ConvertStringValue<T>(string stringValue)
        {
            var targetType = typeof(T);
            
            if (targetType == typeof(bool) || targetType == typeof(bool?))
            {
                if (bool.TryParse(stringValue, out var boolValue))
                {
                    return (T)(object)boolValue;
                }
            }
            else if (targetType == typeof(int) || targetType == typeof(int?))
            {
                if (int.TryParse(stringValue, out var intValue))
                {
                    return (T)(object)intValue;
                }
            }
            else if (targetType == typeof(double) || targetType == typeof(double?))
            {
                if (double.TryParse(stringValue, out var doubleValue))
                {
                    return (T)(object)doubleValue;
                }
            }
            
            return default;
        }

        private T? ConvertJsonElement<T>(JsonElement jsonElement)
        {
            var targetType = typeof(T);
            
            if (targetType == typeof(string))
            {
                return (T)(object)jsonElement.GetString()!;
            }
            else if (targetType == typeof(bool) || targetType == typeof(bool?))
            {
                return (T)(object)jsonElement.GetBoolean();
            }
            else if (targetType == typeof(int) || targetType == typeof(int?))
            {
                return (T)(object)jsonElement.GetInt32();
            }
            else if (targetType == typeof(double) || targetType == typeof(double?))
            {
                return (T)(object)jsonElement.GetDouble();
            }
            
            return default;
        }
    }

    /// <summary>
    /// AI-specific configuration helper that uses the centralized configuration system.
    /// </summary>
    public class AiConfiguration
    {
        private readonly PosKernelConfiguration _config;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiConfiguration"/> class.
        /// </summary>
        /// <param name="config">The underlying configuration system.</param>
        public AiConfiguration(PosKernelConfiguration config)
        {
            _config = config;
        }

        /// <summary>
        /// Gets the AI provider name for store operations.
        /// Uses STORE_AI_PROVIDER environment variable.
        /// </summary>
        public string Provider => 
            _config.GetValue<string>("STORE_AI_PROVIDER") ?? 
            throw new InvalidOperationException(
                "DESIGN DEFICIENCY: Store AI provider not configured. " +
                "Set STORE_AI_PROVIDER environment variable in ~/.poskernel/.env file. " +
                "Cannot decide provider defaults.");

        /// <summary>
        /// Gets the AI model name for store operations.
        /// Uses STORE_AI_MODEL environment variable.
        /// </summary>
        public string Model => 
            _config.GetValue<string>("STORE_AI_MODEL") ?? 
            throw new InvalidOperationException(
                "DESIGN DEFICIENCY: Store AI model not configured. " +
                "Set STORE_AI_MODEL environment variable in ~/.poskernel/.env file. " +
                "Cannot decide model defaults.");

        /// <summary>
        /// Gets the base URL for the AI service.
        /// Uses STORE_AI_BASE_URL environment variable.
        /// </summary>
        public string BaseUrl => 
            _config.GetValue<string>("STORE_AI_BASE_URL") ?? 
            throw new InvalidOperationException(
                "DESIGN DEFICIENCY: Store AI base URL not configured. " +
                "Set STORE_AI_BASE_URL environment variable in ~/.poskernel/.env file. " +
                "Cannot decide base URL defaults.");

        /// <summary>
        /// Gets the temperature setting for AI responses.
        /// </summary>
        public double Temperature => _config.GetValue("AI:Temperature", 0.3);

        /// <summary>
        /// Gets the maximum tokens for AI responses.
        /// </summary>
        public int MaxTokens => _config.GetValue("AI:MaxTokens", 1000);

        /// <summary>
        /// Gets the timeout in seconds for AI requests.
        /// </summary>
        public int TimeoutSeconds => _config.GetValue("AI:TimeoutSeconds", 30);

        /// <summary>
        /// Gets the API key for the AI service.
        /// </summary>
        public string? ApiKey => _config.GetValue<string>("OPENAI_API_KEY");

        /// <summary>
        /// Gets a value indicating whether to use mock AI instead of real AI.
        /// </summary>
        public bool UseMockAi => _config.GetValue("USE_MOCK_AI", "false")!.Equals("true", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Gets whether the configured provider is a local LLM (Ollama).
        /// </summary>
        public bool IsLocalProvider => Provider.Equals("Ollama", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Gets the completion endpoint path - different for Ollama vs OpenAI
        /// </summary>
        public string CompletionEndpoint => IsLocalProvider ? "api/generate" : "chat/completions";

        /// <summary>
        /// Gets the MCP configuration for the AI client.
        /// ARCHITECTURAL PRINCIPLE: Store AI configuration uses STORE_AI_* environment variables
        /// </summary>
        public McpConfiguration ToMcpConfiguration()
        {
            var mcpConfig = new McpConfiguration
            {
                BaseUrl = BaseUrl,
                CompletionEndpoint = CompletionEndpoint,
                TimeoutSeconds = TimeoutSeconds,
                DefaultParameters = new Dictionary<string, object>
                {
                    ["model"] = Model,
                    ["temperature"] = Temperature,
                    ["max_tokens"] = MaxTokens
                }
            };

            // ARCHITECTURAL PRINCIPLE: Configure API key based on provider
            if (IsLocalProvider)
            {
                mcpConfig.ApiKey = "local";
            }
            else
            {
                mcpConfig.ApiKey = ApiKey;
                
                if (string.IsNullOrEmpty(mcpConfig.ApiKey))
                {
                    throw new InvalidOperationException(
                        "DESIGN DEFICIENCY: OpenAI API key not configured for store AI. " +
                        "Set OPENAI_API_KEY environment variable or switch to local provider with STORE_AI_PROVIDER=Ollama");
                }
            }

            return mcpConfig;
        }
    }

    /// <summary>
    /// Configuration for MCP client connections and behavior.
    /// Moved here to avoid circular dependencies.
    /// </summary>
    public class McpConfiguration
    {
        /// <summary>
        /// Gets or sets the base URL for the MCP server.
        /// </summary>
        public string BaseUrl { get; set; } = "https://api.openai.com/v1";
        
        /// <summary>
        /// Gets or sets the completion endpoint path.
        /// </summary>
        public string CompletionEndpoint { get; set; } = "chat/completions";
        
        /// <summary>
        /// Gets or sets the API key for authentication.
        /// </summary>
        public string? ApiKey { get; set; }
        
        /// <summary>
        /// Gets or sets the request timeout in seconds.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;
        
        /// <summary>
        /// Gets or sets the default parameters sent with requests.
        /// </summary>
        public Dictionary<string, object> DefaultParameters { get; set; } = new();
        
        /// <summary>
        /// Gets or sets the JSON serialization options.
        /// </summary>
        public JsonSerializerOptions JsonOptions { get; set; } = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }
}
