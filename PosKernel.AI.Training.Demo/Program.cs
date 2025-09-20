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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PosKernel.AI.Training;
using PosKernel.AI.Training.Configuration;

// Create host builder with AI training services
var builder = Host.CreateApplicationBuilder(args);

// Add logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Debug);
});

// Add AI training services
builder.Services.AddAITraining();

var host = builder.Build();

// Get services and demonstrate functionality
var configService = host.Services.GetRequiredService<ITrainingConfigurationService>();
var logger = host.Services.GetRequiredService<ILogger<Program>>();

try
{
    logger.LogInformation("=== POS Kernel AI Training Library Demo ===");
    
    // Try to load existing configuration
    TrainingConfiguration? config = null;
    try
    {
        config = await configService.LoadConfigurationAsync();
        logger.LogInformation("Loaded existing configuration with {ScenarioCount} scenarios", config.ScenarioCount);
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("DESIGN DEFICIENCY"))
    {
        logger.LogInformation("No existing configuration found, creating default");
        
        // Create and save default configuration
        config = configService.CreateDefaultConfiguration();
        await configService.SaveConfigurationAsync(config);
        logger.LogInformation("Created and saved default configuration");
    }

    // Validate configuration
    var validation = await configService.ValidateConfigurationAsync(config);
    logger.LogInformation("Configuration validation: IsValid={IsValid}, Errors={ErrorCount}, Warnings={WarningCount}",
        validation.IsValid, validation.Errors.Count, validation.Warnings.Count);

    foreach (var error in validation.Errors)
    {
        logger.LogError("Validation error: {Error}", error);
    }

    foreach (var warning in validation.Warnings)
    {
        logger.LogWarning("Validation warning: {Warning}", warning);
    }

    // Display configuration summary
    logger.LogInformation("Configuration Summary:");
    logger.LogInformation("  Scenario Count: {ScenarioCount}", config.ScenarioCount);
    logger.LogInformation("  Max Generations: {MaxGenerations}", config.MaxGenerations);
    logger.LogInformation("  Improvement Threshold: {Threshold:P2}", config.ImprovementThreshold);
    logger.LogInformation("  Focus Areas:");
    logger.LogInformation("    Tool Selection: {Focus:P1}", config.Focus.ToolSelectionAccuracy);
    logger.LogInformation("    Personality: {Focus:P1}", config.Focus.PersonalityAuthenticity);
    logger.LogInformation("    Payment Flow: {Focus:P1}", config.Focus.PaymentFlowCompletion);

    // Test data store directly
    var dataStore = host.Services.GetRequiredService<PosKernel.AI.Training.Storage.ITrainingDataStore>();
    
    logger.LogInformation("=== Testing Data Store Operations ===");
    
    // Test basic operations
    var testKey = "test-data";
    var testData = new { Message = "Hello from POS Kernel AI Training!", Timestamp = DateTime.UtcNow };
    
    await dataStore.SaveAsync(testKey, testData);
    logger.LogInformation("Saved test data");
    
    var loadedData = await dataStore.LoadAsync<object>(testKey);
    logger.LogInformation("Loaded test data: {Data}", loadedData);
    
    var exists = await dataStore.ExistsAsync(testKey);
    logger.LogInformation("Test data exists: {Exists}", exists);
    
    var keys = await dataStore.ListKeysAsync();
    logger.LogInformation("All keys: {Keys}", string.Join(", ", keys));
    
    await dataStore.DeleteAsync(testKey);
    logger.LogInformation("Deleted test data");
    
    logger.LogInformation("=== Demo Complete ===");
}
catch (Exception ex)
{
    logger.LogError(ex, "Demo failed with error: {Error}", ex.Message);
    return 1;
}

return 0;
