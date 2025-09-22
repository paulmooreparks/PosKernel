/*
Quick test to verify environment variable loading is working in training system
*/
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PosKernel.AI.Training;
using PosKernel.AI.Training.Services;
using PosKernel.Configuration;

Console.WriteLine("=== POS Kernel Training Configuration Test ===");

try
{
    // Initialize PosKernel configuration (this should load the .env file)
    var config = PosKernelConfiguration.Initialize();
    Console.WriteLine("✅ PosKernelConfiguration initialized successfully");
    
    // Check environment variables
    var storeProvider = Environment.GetEnvironmentVariable("STORE_AI_PROVIDER");
    var storeModel = Environment.GetEnvironmentVariable("STORE_AI_MODEL");
    var trainingProvider = Environment.GetEnvironmentVariable("TRAINING_AI_PROVIDER");
    var trainingModel = Environment.GetEnvironmentVariable("TRAINING_AI_MODEL");
    
    Console.WriteLine($"STORE_AI_PROVIDER: {storeProvider ?? "NOT SET"}");
    Console.WriteLine($"STORE_AI_MODEL: {storeModel ?? "NOT SET"}");
    Console.WriteLine($"TRAINING_AI_PROVIDER: {trainingProvider ?? "NOT SET"}");  
    Console.WriteLine($"TRAINING_AI_MODEL: {trainingModel ?? "NOT SET"}");
    
    // Test service registration
    var services = new ServiceCollection();
    services.AddLogging(builder => builder.AddConsole());
    services.AddAITraining();
    
    using var serviceProvider = services.BuildServiceProvider();
    serviceProvider.ValidateAITrainingServices();
    
    Console.WriteLine("✅ AI Training services registered successfully");
    
    // Test prompt service
    var promptService = serviceProvider.GetRequiredService<IPromptManagementService>();
    Console.WriteLine("✅ IPromptManagementService resolved successfully");
    
    // Test prompt loading
    var context = new PosKernel.AI.Services.PromptContext
    {
        TimeOfDay = "afternoon",
        CurrentTime = DateTime.Now.ToString("HH:mm"),
        Currency = "SGD"
    };
    
    var prompt = await promptService.GetCurrentPromptAsync(
        PosKernel.Abstractions.PersonalityType.SingaporeanKopitiamUncle, 
        "ordering", 
        context);
        
    Console.WriteLine("✅ Prompt loaded successfully");
    Console.WriteLine($"Prompt length: {prompt.Length} characters");
    Console.WriteLine($"Prompt preview: {prompt.Substring(0, Math.Min(200, prompt.Length))}...");
    
    Console.WriteLine("\n🎉 ALL TESTS PASSED - Training system configuration is working!");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ ERROR: {ex.Message}");
    if (ex.InnerException != null)
    {
        Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
    }
    return 1;
}

return 0;
