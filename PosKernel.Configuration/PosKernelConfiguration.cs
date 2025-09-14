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
            
            // AI defaults
            _configuration["AI:Provider"] = "OpenAI";
            _configuration["AI:Model"] = "gpt-4o-mini";
            _configuration["AI:Temperature"] = 0.3;
            _configuration["AI:MaxTokens"] = 1000;
            _configuration["AI:TimeoutSeconds"] = 30;
            
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
                    Console.WriteLine($"Warning: Failed to load {sourceName} from {filePath}: {ex.Message}");
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
                    Console.WriteLine($"Warning: Failed to load .env file: {ex.Message}");
                }
            }
        }

        private void LoadEnvironmentVariables()
        {
            // Load well-known environment variables
            var envVars = new[]
            {
                "OPENAI_API_KEY",
                "MCP_BASE_URL",
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

        public AiConfiguration(PosKernelConfiguration config)
        {
            _config = config;
        }

        public string Provider => _config.GetValue("AI:Provider", "OpenAI")!;
        public string Model => _config.GetValue("AI:Model", "gpt-4o-mini")!;
        public double Temperature => _config.GetValue("AI:Temperature", 0.3);
        public int MaxTokens => _config.GetValue("AI:MaxTokens", 1000);
        public int TimeoutSeconds => _config.GetValue("AI:TimeoutSeconds", 30);
        public string BaseUrl => _config.GetValue("MCP_BASE_URL", "https://api.openai.com/v1/")!;
        public string? ApiKey => _config.GetValue<string>("OPENAI_API_KEY");
        public bool UseMockAi => _config.GetValue("USE_MOCK_AI", "false")!.Equals("true", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Gets the MCP configuration for the AI client.
        /// </summary>
        public McpConfiguration ToMcpConfiguration()
        {
            return new McpConfiguration
            {
                BaseUrl = BaseUrl,
                ApiKey = ApiKey,
                TimeoutSeconds = TimeoutSeconds,
                DefaultParameters = new Dictionary<string, object>
                {
                    ["model"] = Model,
                    ["temperature"] = Temperature,
                    ["max_tokens"] = MaxTokens
                }
            };
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
