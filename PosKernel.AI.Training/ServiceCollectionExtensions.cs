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
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using PosKernel.AI.Training.Configuration;
using PosKernel.AI.Training.Core;
using PosKernel.AI.Training.Storage;
using PosKernel.AI.Services;
using PosKernel.AI.Models;
using PosKernel.Abstractions.Services;
using PosKernel.Configuration.Services;

namespace PosKernel.AI.Training;

/// <summary>
/// Extension methods for registering AI training services with dependency injection
/// ARCHITECTURAL PRINCIPLE: No hardcoded defaults, proper service configuration
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds AI training services to the dependency injection container
    /// ARCHITECTURAL PRINCIPLE: All configuration comes from services, not hardcoded defaults
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="dataDirectory">Directory for training data storage (optional, uses default if null)</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddAITraining(this IServiceCollection services, string? dataDirectory = null)
    {
        // ARCHITECTURAL PRINCIPLE: Fail fast if services already registered to prevent conflicts
        if (services.Any(s => s.ServiceType == typeof(ITrainingConfigurationService)))
        {
            throw new InvalidOperationException(
                "DESIGN DEFICIENCY: AI Training services already registered. " +
                "AddAITraining() should only be called once per service container. " +
                "Check for duplicate registration calls.");
        }

        // Determine data directory with cross-platform default
        var trainingDataDir = dataDirectory ?? GetDefaultDataDirectory();

        // Register core services
        services.TryAddSingleton<ITrainingDataStore>(serviceProvider =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<JsonFileTrainingDataStore>>();
            return new JsonFileTrainingDataStore(trainingDataDir, logger);
        });

        services.TryAddScoped<ITrainingConfigurationService, TrainingConfigurationService>();

        // Register configuration services with proper currency/culture (no defaults)
        services.TryAddSingleton<IStoreConfigurationService>(_ => 
            new TrainingStoreConfigurationService());

        // Register prompt management services  
        services.TryAddScoped<IPromptManagementService, PromptManagementService>();

        // Register training session - PRODUCTION implementation using EXACT same services as PosKernel.AI
        services.TryAddTransient<ITrainingSession>(provider =>
        {
            // ARCHITECTURAL PRINCIPLE: Get store configuration from service - FAIL FAST if not available
            var storeConfigurationService = provider.GetService<IStoreConfigurationService>();
            if (storeConfigurationService == null)
            {
                throw new InvalidOperationException(
                    "DESIGN DEFICIENCY: Training requires IStoreConfigurationService to get store configuration. " +
                    "Training cannot decide store configuration defaults. " +
                    "Register IStoreConfigurationService in DI container with proper store configuration.");
            }

            // ARCHITECTURAL PRINCIPLE: Get configuration from service - no hardcoded values
            var baseStoreConfig = storeConfigurationService.GetStoreConfiguration("training-store");
            
            // ARCHITECTURAL PRINCIPLE: Validate currency from service - FAIL FAST if missing
            if (string.IsNullOrEmpty(baseStoreConfig.Currency))
            {
                throw new InvalidOperationException(
                    "DESIGN DEFICIENCY: Store configuration service must provide Currency. " +
                    "Training cannot decide currency defaults. " +
                    "Ensure IStoreConfigurationService.GetStoreConfiguration returns valid Currency.");
            }

            // ARCHITECTURAL PRINCIPLE: Build store config from service data - no client defaults
            var storeConfig = new StoreConfig
            {
                StoreName = "Training Store", // Only non-business-logic value that's safe to default
                StoreType = StoreType.Kopitiam, // TODO: This should come from configuration service
                PersonalityType = PersonalityType.SingaporeanKopitiamUncle, // TODO: This should come from configuration service  
                Currency = baseStoreConfig.Currency, // From configuration service
                CultureCode = baseStoreConfig.Locale // From configuration service
            };

            var personalityConfig = new AiPersonalityConfig
            {
                Type = storeConfig.PersonalityType, // From store configuration
                StaffTitle = "Uncle", // TODO: This should be derived from PersonalityType by service
                VenueTitle = "Kopitiam", // TODO: This should be derived from StoreType by service
                SupportedLanguages = new List<string> { "en", "zh", "ms", "ta" }, // TODO: This should come from culture service
                CultureCode = storeConfig.CultureCode, // From store configuration  
                Currency = storeConfig.Currency // From store configuration
            };

            // ARCHITECTURAL PRINCIPLE: Create production services with validated configuration
            var productionServices = ProductionAIServiceFactory.CreateProductionServicesAsync(
                storeConfig,
                personalityConfig).GetAwaiter().GetResult();

            // Create training session with production services
            return new ProductionAITrainingSession(
                provider.GetRequiredService<ILogger<ProductionAITrainingSession>>(),
                provider.GetRequiredService<IPromptManagementService>(),
                productionServices,
                storeConfig.PersonalityType);
        });

        return services;
    }

    /// <summary>
    /// Gets the default cross-platform data directory - matches PosKernel configuration directory structure
    /// </summary>
    private static string GetDefaultDataDirectory()
    {
        // ARCHITECTURAL PRINCIPLE: Use same directory structure as PosKernelConfiguration
        // Training data should be in ~/.poskernel/training/ alongside ~/.poskernel/.env
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        
        if (string.IsNullOrEmpty(homeDir))
        {
            // Fallback for systems without UserProfile folder
            homeDir = Path.GetTempPath();
        }

        return Path.Combine(homeDir, ".poskernel", "training");
    }

    /// <summary>
    /// Validates that required AI training services are registered
    /// ARCHITECTURAL PRINCIPLE: FAIL FAST if required services missing
    /// </summary>
    /// <param name="services">The service provider</param>
    /// <exception cref="InvalidOperationException">Thrown when required services are missing</exception>
    public static void ValidateAITrainingServices(this IServiceProvider services)
    {
        var missingServices = new List<string>();

        if (services.GetService<ITrainingDataStore>() == null)
        {
            missingServices.Add(nameof(ITrainingDataStore));
        }

        if (services.GetService<ITrainingConfigurationService>() == null)
        {
            missingServices.Add(nameof(ITrainingConfigurationService));
        }

        if (services.GetService<ITrainingSession>() == null)
        {
            missingServices.Add(nameof(ITrainingSession));
        }

        if (services.GetService<IPromptManagementService>() == null)
        {
            missingServices.Add(nameof(IPromptManagementService));
        }

        // ARCHITECTURAL PRINCIPLE: Fail fast if required services missing
        if (missingServices.Any())
        {
            throw new InvalidOperationException(
                $"DESIGN DEFICIENCY: Required AI Training services not registered: {string.Join(", ", missingServices)}. " +
                "Call services.AddAITraining() in your service registration code before building the service provider.");
        }
    }
}
