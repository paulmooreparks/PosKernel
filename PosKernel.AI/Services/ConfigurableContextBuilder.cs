using System.Text.Json;
using PosKernel.AI.Models;
using PosKernel.Configuration;
using Microsoft.Extensions.Logging;

namespace PosKernel.AI.Services
{
    /// <summary>
    /// ARCHITECTURAL PRINCIPLE: Configuration-driven context system
    /// Eliminates hardcoding by using JSON config to map states to prompt files
    /// </summary>
    public class ConfigurableContextBuilder
    {
        private readonly PersonalityType _personalityType;
        private readonly ILogger<ConfigurableContextBuilder> _logger;
        private readonly ContextConfiguration _config;

        public ConfigurableContextBuilder(PersonalityType personalityType, ILogger<ConfigurableContextBuilder> logger)
        {
            _personalityType = personalityType;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = LoadConfiguration();
        }

        /// <summary>
        /// ARCHITECTURAL PRINCIPLE: Build context using configuration-driven approach
        /// No hardcoded state-to-prompt mappings - all driven by JSON config
        /// </summary>
        public string BuildContext(string transactionState, Dictionary<string, object> variables)
        {
            try
            {
                var personalityConfig = _config.GetPersonalityConfig(_personalityType.ToString());
                if (personalityConfig == null)
                {
                    throw new InvalidOperationException(
                        $"DESIGN DEFICIENCY: No context configuration found for personality {_personalityType}. " +
                        $"Add configuration to context-config.json in ai_config directory.");
                }

                // Load base context files
                var baseContext = LoadPromptFiles(personalityConfig.BaseContext);

                // Load state-specific context files
                var stateContext = LoadStateContext(personalityConfig, transactionState);

                // Load context template
                var template = LoadContextTemplate(personalityConfig);

                // Build variable replacements
                var replacements = BuildReplacements(baseContext, stateContext, variables);

                // Replace all placeholders in template
                var result = ReplaceVariables(template, replacements);

                _logger.LogDebug("Built context for {PersonalityType} in state {State}: {Length} characters",
                    _personalityType, transactionState, result.Length);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to build context for {PersonalityType} in state {State}",
                    _personalityType, transactionState);
                throw;
            }
        }

        /// <summary>
        /// Load configuration from JSON file with fail-fast behavior
        /// </summary>
        private ContextConfiguration LoadConfiguration()
        {
            try
            {
                var configPath = Path.Combine(PosKernelConfiguration.ConfigDirectory, "ai_config", "context-config.json");

                if (!File.Exists(configPath))
                {
                    throw new FileNotFoundException(
                        $"DESIGN DEFICIENCY: Context configuration not found at {configPath}. " +
                        $"Configuration-driven context system requires context-config.json file. " +
                        $"Create the file with personality configurations and state mappings.");
                }

                var jsonContent = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<ContextConfiguration>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (config == null)
                {
                    throw new InvalidOperationException(
                        $"DESIGN DEFICIENCY: Failed to deserialize context configuration from {configPath}. " +
                        $"Verify JSON syntax and structure.");
                }

                return config;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load context configuration");
                throw;
            }
        }

        /// <summary>
        /// Load and combine multiple prompt files into single context block
        /// </summary>
        private string LoadPromptFiles(List<string> fileNames)
        {
            if (fileNames == null || fileNames.Count == 0)
            {
                return "";
            }

            var contextParts = new List<string>();

            foreach (var fileName in fileNames)
            {
                try
                {
                    var content = AiPersonalityFactory.LoadPrompt(_personalityType,
                        Path.GetFileNameWithoutExtension(fileName));
                    contextParts.Add(content);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to load prompt file {FileName} for {PersonalityType}: {Error}",
                        fileName, _personalityType, ex.Message);
                }
            }

            return string.Join("\n\n", contextParts);
        }

        /// <summary>
        /// Load state-specific context with fallback to default
        /// </summary>
        private string LoadStateContext(PersonalityConfiguration personalityConfig, string transactionState)
        {
            // Try to get state-specific context
            if (personalityConfig.StateContexts != null &&
                personalityConfig.StateContexts.TryGetValue(transactionState, out var stateFiles))
            {
                return LoadPromptFiles(stateFiles);
            }

            // Fallback to global default
            if (_config.GlobalDefaults?.FallbackStateContext != null)
            {
                return LoadPromptFiles(new List<string> { _config.GlobalDefaults.FallbackStateContext });
            }

            _logger.LogWarning("No state context configured for {PersonalityType} state {State}",
                _personalityType, transactionState);
            return "";
        }

        /// <summary>
        /// Load context template with fallback to global default
        /// </summary>
        private string LoadContextTemplate(PersonalityConfiguration personalityConfig)
        {
            var templateName = personalityConfig.ContextTemplate ??
                              _config.GlobalDefaults?.ContextTemplate ??
                              "context-template";

            try
            {
                return AiPersonalityFactory.LoadPrompt(_personalityType,
                    Path.GetFileNameWithoutExtension(templateName));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"DESIGN DEFICIENCY: Context template '{templateName}' not found for {_personalityType}. " +
                    $"Create the template file or update context-config.json. Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Build dictionary of all variable replacements
        /// </summary>
        private Dictionary<string, string> BuildReplacements(string baseContext, string stateContext, Dictionary<string, object> variables)
        {
            var replacements = new Dictionary<string, string>
            {
                ["BaseContext"] = baseContext,
                ["StateContext"] = stateContext,
                ["InventoryContext"] = variables.ContainsKey("InventoryContext") ? variables["InventoryContext"].ToString() ?? "" : "",
                ["ToolsContext"] = variables.ContainsKey("ToolsContext") ? variables["ToolsContext"].ToString() ?? "" : "You have access to tools for managing transactions, searching products, and processing payments."
            };

            // Add all provided variables
            foreach (var kvp in variables)
            {
                replacements[kvp.Key] = kvp.Value?.ToString() ?? "";
            }

            return replacements;
        }

        /// <summary>
        /// Replace all {{Variable}} placeholders in template
        /// </summary>
        private string ReplaceVariables(string template, Dictionary<string, string> replacements)
        {
            var result = template;

            foreach (var kvp in replacements)
            {
                var placeholder = $"{{{{{kvp.Key}}}}}";
                result = result.Replace(placeholder, kvp.Value);
            }

            return result;
        }
    }

    /// <summary>
    /// Configuration structure for context building
    /// </summary>
    public class ContextConfiguration
    {
        public Dictionary<string, PersonalityConfiguration> Personalities { get; set; } = new();
        public GlobalDefaults? GlobalDefaults { get; set; }

        public PersonalityConfiguration? GetPersonalityConfig(string personalityType)
        {
            Personalities.TryGetValue(personalityType, out var config);
            return config;
        }
    }

    public class PersonalityConfiguration
    {
        public List<string> BaseContext { get; set; } = new();
        public Dictionary<string, List<string>>? StateContexts { get; set; }
        public string? ContextTemplate { get; set; }
        public Dictionary<string, string>? Variables { get; set; }
    }

    public class GlobalDefaults
    {
        public string? ContextTemplate { get; set; }
        public List<string>? BaseContext { get; set; }
        public string? FallbackStateContext { get; set; }
    }
}
