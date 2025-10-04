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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using PosKernel.AI.Core;
using PosKernel.AI.Services;
using PosKernel.AI.Tools;
using PosKernel.AI.Models;
using PosKernel.Extensions.Restaurant.Client;
using PosKernel.Configuration;
using PosKernel.Configuration.Services;
using PosKernel.Abstractions;
using PosKernel.Abstractions.Services;
using PosKernel.AI.Common.Abstractions;
using PosKernel.AI.Common.Factories;

namespace PosKernel.AI.Training.Core;

/// <summary>
/// Factory that creates production PosAiAgent instances using the EXACT same setup as PosKernel.AI demo
/// ARCHITECTURAL PRINCIPLE: No duplication - reuse the proven service configuration pattern
/// ARCHITECTURAL PRINCIPLE: FAIL FAST - no hardcoded defaults, no silent fallbacks
/// </summary>
public static class ProductionAIServiceFactory
{
    /// <summary>
    /// Creates a ServiceProvider with the EXACT same configuration as PosKernel.AI
    /// ARCHITECTURAL PRINCIPLE: Training uses identical infrastructure as production
    /// ARCHITECTURAL PRINCIPLE: All configuration must come from services - no client defaults
    /// </summary>
    public static async Task<ServiceProvider> CreateProductionServicesAsync(
        StoreConfig storeConfig,
        AiPersonalityConfig personalityConfig,
        ILoggerProvider? customLoggerProvider = null)
    {
        // ARCHITECTURAL PRINCIPLE: Validate required configuration - FAIL FAST if missing
        if (storeConfig == null)
        {
            throw new ArgumentNullException(nameof(storeConfig),
                "DESIGN DEFICIENCY: ProductionAIServiceFactory requires StoreConfig. " +
                "Training cannot decide store configuration defaults. " +
                "Provide StoreConfig from proper configuration service.");
        }

        if (personalityConfig == null)
        {
            throw new ArgumentNullException(nameof(personalityConfig),
                "DESIGN DEFICIENCY: ProductionAIServiceFactory requires AiPersonalityConfig. " +
                "Training cannot decide personality defaults. " +
                "Provide AiPersonalityConfig from proper configuration service.");
        }

        // ARCHITECTURAL PRINCIPLE: Validate currency configuration - FAIL FAST if missing
        if (string.IsNullOrEmpty(storeConfig.Currency))
        {
            throw new InvalidOperationException(
                "DESIGN DEFICIENCY: StoreConfig must provide Currency configuration. " +
                "Training cannot decide currency defaults. " +
                "Ensure StoreConfig.Currency is set through proper configuration service.");
        }

        if (string.IsNullOrEmpty(storeConfig.CultureCode))
        {
            throw new InvalidOperationException(
                "DESIGN DEFICIENCY: StoreConfig must provide CultureCode configuration. " +
                "Training cannot decide culture defaults. " +
                "Ensure StoreConfig.CultureCode is set through proper configuration service.");
        }

        // Initialize configuration system (same as PosKernel.AI)
        var config = PosKernelConfiguration.Initialize();

        // ARCHITECTURAL FIX: Training uses TRAINING_AI_* configuration, not STORE_AI_*
        // This allows training to use Ollama (unlimited) while store uses OpenAI (quality)
        var provider = config.GetValue<string>("TRAINING_AI_PROVIDER") ??
            throw new InvalidOperationException(
                "DESIGN DEFICIENCY: Training requires Training AI provider configuration. " +
                "Set TRAINING_AI_PROVIDER in ~/.poskernel/.env (e.g., TRAINING_AI_PROVIDER=Ollama). " +
                $"Config directory: {PosKernelConfiguration.ConfigDirectory}");

        var model = config.GetValue<string>("TRAINING_AI_MODEL") ??
            throw new InvalidOperationException(
                "DESIGN DEFICIENCY: Training requires Training AI model configuration. " +
                "Set TRAINING_AI_MODEL in ~/.poskernel/.env (e.g., TRAINING_AI_MODEL=llama3.1:8b). " +
                $"Config directory: {PosKernelConfiguration.ConfigDirectory}");

        var baseUrl = config.GetValue<string>("TRAINING_AI_BASE_URL") ??
            throw new InvalidOperationException(
                "DESIGN DEFICIENCY: Training requires Training AI base URL configuration. " +
                "Set TRAINING_AI_BASE_URL in ~/.poskernel/.env (e.g., TRAINING_AI_BASE_URL=http://localhost:11434). " +
                $"Config directory: {PosKernelConfiguration.ConfigDirectory}");

        // API key handling - different for different providers
        string? apiKey = null;
        if (provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
        {
            apiKey = config.GetValue<string>("OPENAI_API_KEY");
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException(
                    "DESIGN DEFICIENCY: Training requires OpenAI API key when TRAINING_AI_PROVIDER=OpenAI. " +
                    "Set OPENAI_API_KEY in ~/.poskernel/.env or use local provider (TRAINING_AI_PROVIDER=Ollama). " +
                    $"Config directory: {PosKernelConfiguration.ConfigDirectory}");
            }
        }
        else if (provider.Equals("Ollama", StringComparison.OrdinalIgnoreCase))
        {
            apiKey = "local"; // Ollama doesn't need real API key
        }

        var services = new ServiceCollection();

        // Add logging (same as PosKernel.AI)
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            if (customLoggerProvider != null)
            {
                builder.AddProvider(customLoggerProvider);
            }
            else
            {
                builder.AddConsole();
            }
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Add configuration services (same as PosKernel.AI)
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection()
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<ICurrencyFormattingService, CurrencyFormattingService>();
        services.AddSingleton<IStoreConfigurationService>(provider => new ConfiguredStoreConfigurationService(storeConfig));

        // ARCHITECTURAL FIX: Use shared LLM factory with training AI configuration
        // Training uses TRAINING_AI_* configuration (can be different from STORE_AI_*)
        services.AddSingleton<ILargeLanguageModel>(serviceProvider =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<ILargeLanguageModel>>();
            return LLMProviderFactory.CreateLLM(provider, model, baseUrl, apiKey, logger);
        });

        // Create MCP configuration for training AI (may be different from store AI)
        var mcpConfig = new McpConfiguration
        {
            BaseUrl = baseUrl,
            CompletionEndpoint = provider.Equals("Ollama", StringComparison.OrdinalIgnoreCase) ? "api/generate" : "chat/completions",
            ApiKey = apiKey,
            TimeoutSeconds = 30,
            DefaultParameters = new Dictionary<string, object>
            {
                ["model"] = model,
                ["temperature"] = 0.3,
                ["max_tokens"] = 1000
            }
        };

        services.AddSingleton(mcpConfig);
        services.AddSingleton<McpClient>();

        // Add store configurations (same pattern as PosKernel.AI) - NO DEFAULTS
        services.AddSingleton(storeConfig);
        services.AddSingleton(personalityConfig);

        // Real kernel integration (EXACT same pattern as PosKernel.AI) - FAIL FAST if not available
        try
        {
            // Test RestaurantExtensionClient connection (same validation as PosKernel.AI)
            using var tempServiceProvider = services.BuildServiceProvider();
            var tempLogger = tempServiceProvider.GetRequiredService<ILogger<RestaurantExtensionClient>>();
            var restaurantClient = new RestaurantExtensionClient(tempLogger);

            // Test connection (same test as PosKernel.AI) - FAIL FAST if not available
            var testProducts = await restaurantClient.SearchProductsAsync("test", 1);

            // Register real services (EXACT same registrations as PosKernel.AI)
            services.AddSingleton<RestaurantExtensionClient>(_ => restaurantClient);
            services.AddSingleton<KernelPosToolsProvider>(provider =>
                new KernelPosToolsProvider(
                    restaurantClient,
                    provider.GetRequiredService<ILogger<KernelPosToolsProvider>>(),
                    storeConfig,
                    PosKernel.Client.PosKernelClientFactory.KernelType.HttpService,
                    configuration,
                    provider.GetRequiredService<ICurrencyFormattingService>()));

            // Register ConfigurableContextBuilder for training AI (EXACT same as PosKernel.AI)
            services.AddSingleton<ConfigurableContextBuilder>(provider =>
                new ConfigurableContextBuilder(
                    personalityConfig.Type,
                    provider.GetRequiredService<ILogger<ConfigurableContextBuilder>>()));

            // Register PosAiAgent for real kernel mode (EXACT same as PosKernel.AI)
            services.AddSingleton<PosAiAgent>(provider =>
            {
                var storeConfig = provider.GetRequiredService<StoreConfig>();
                return new PosAiAgent(
                    provider.GetRequiredService<McpClient>(),
                    provider.GetRequiredService<ILogger<PosAiAgent>>(),
                    storeConfig,
                    new Receipt(),
                    new Transaction(storeConfig.Currency),
                    provider.GetRequiredService<ConfigurableContextBuilder>(),
                    provider.GetRequiredService<KernelPosToolsProvider>());
            });
        }
        catch (Exception ex)
        {
            // ARCHITECTURAL PRINCIPLE: FAIL FAST (same as PosKernel.AI) - no silent fallbacks
            throw new InvalidOperationException(
                "DESIGN DEFICIENCY: Training requires real kernel services but they are not available. " +
                "Training cannot fall back to mock systems - must use EXACT same infrastructure as production AI. " +
                $"Error: {ex.Message}\n\n" +
                "To use training with production AI:\n" +
                "1. Start the Rust kernel service: cd pos-kernel-rs && cargo run --bin service\n" +
                "2. Ensure Restaurant Extension Service is running and accessible\n" +
                "3. Verify SQLite database exists: data/catalog/restaurant_catalog.db\n\n" +
                "Training requires the EXACT same infrastructure as the AI demo.");
        }

        var serviceProvider = services.BuildServiceProvider();

        // Validate PosAiAgent creation (same validation as PosKernel.AI) - FAIL FAST if not working
        try
        {
            var aiAgent = serviceProvider.GetRequiredService<PosAiAgent>();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "DESIGN DEFICIENCY: Failed to create production PosAiAgent for training. " +
                "Training requires functional production AI infrastructure. " +
                $"Error: {ex.Message}");
        }

        return serviceProvider;
    }
}

/// <summary>
/// Store configuration service that uses provided configuration instead of defaults
/// ARCHITECTURAL PRINCIPLE: No defaults - use configuration provided by caller
/// </summary>
internal class ConfiguredStoreConfigurationService : IStoreConfigurationService
{
    private readonly StoreConfig _storeConfig;

    public ConfiguredStoreConfigurationService(StoreConfig storeConfig)
    {
        _storeConfig = storeConfig ?? throw new ArgumentNullException(nameof(storeConfig),
            "DESIGN DEFICIENCY: ConfiguredStoreConfigurationService requires StoreConfig. " +
            "Cannot create store configuration service without store configuration. " +
            "Provide proper StoreConfig instance.");
    }

    public StoreConfiguration GetStoreConfiguration(string storeId)
    {
        // ARCHITECTURAL PRINCIPLE: Use provided configuration - no hardcoded defaults
        return new StoreConfiguration
        {
            StoreId = storeId,
            Currency = _storeConfig.Currency, // From provided configuration
            Locale = _storeConfig.CultureCode  // From provided configuration
        };
    }
}
