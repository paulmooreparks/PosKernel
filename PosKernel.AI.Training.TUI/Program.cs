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
using PosKernel.AI.Training.TUI.Views;
using Terminal.Gui;
using TGAttribute = Terminal.Gui.Attribute;

// ARCHITECTURAL PRINCIPLE: Application is pure UI orchestration - no training logic

var builder = Host.CreateApplicationBuilder(args);

// Add logging without console provider to avoid interference with Terminal.Gui
builder.Services.AddLogging(logging =>
{
    logging.ClearProviders(); // Remove console logging that interferes with TUI
});

// Add AI training services
builder.Services.AddAITraining();

// Add TUI-specific services
builder.Services.AddTransient<TrainingConfigurationDialog>();
builder.Services.AddTransient<TrainingSessionDialog>();
builder.Services.AddSingleton<TrainingConfigurationTUI>();

var host = builder.Build();

// Validate services are properly registered
host.Services.ValidateAITrainingServices();

var logger = host.Services.GetRequiredService<ILogger<Program>>();

try
{
    logger.LogDebug("=== POS Kernel AI Training Configuration Tool ===");

    var tui = host.Services.GetRequiredService<TrainingConfigurationTUI>();
    await tui.RunAsync();
    
    return 0;
}
catch (Exception ex)
{
    // Ensure Terminal.Gui is shut down properly before logging
    try 
    { 
        Application.Shutdown(); 
    } 
    catch 
    { 
        // Ignore shutdown errors
    }

    // Use original console streams for final error output (redirectors should be disposed by now)
    Console.WriteLine($"Fatal error: {ex.Message}");
    
    if (args.Contains("--debug"))
    {
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
    }
    
    Console.WriteLine("Press any key to exit...");
    Console.ReadKey();
    
    return 1;
}
