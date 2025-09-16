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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace PosKernel.Extensions.Restaurant 
{
    /// <summary>
    /// Main entry point for the restaurant domain extension service.
    /// This runs as a hosted service that provides product catalog operations
    /// via named pipe IPC for any client (AI demo, kernel service, etc.).
    /// </summary>
    class Program 
    {
        static async Task<int> Main(string[] args) 
        {
            try 
            {
                Console.WriteLine("🏪 POS Kernel Restaurant Extension v0.5.0");
                Console.WriteLine("Domain service with named pipe IPC");

                // Build host with proper DI and logging
                var host = Host.CreateDefaultBuilder(args)
                    .ConfigureAppConfiguration((hostingContext, config) =>
                    {
                        // Clear existing sources and add our specific configuration
                        config.Sources.Clear();
                        
                        // Add configuration from the project directory  
                        var projectDir = "PosKernel.Extensions.Restaurant";
                        var configFile = Path.Combine(projectDir, "appsettings.json");
                        
                        Console.WriteLine($"📁 Loading configuration from: {configFile}");
                        
                        if (File.Exists(configFile))
                        {
                            config.AddJsonFile(configFile, optional: false, reloadOnChange: true);
                            Console.WriteLine($"✅ Found configuration file: {configFile}");
                        }
                        else
                        {
                            Console.WriteLine($"❌ Configuration file not found: {configFile}");
                            Console.WriteLine($"📁 Current directory: {Directory.GetCurrentDirectory()}");
                            Console.WriteLine($"📄 Files in current directory:");
                            foreach (var file in Directory.GetFiles(".", "*.json"))
                            {
                                Console.WriteLine($"   {file}");
                            }
                        }
                        
                        config.AddEnvironmentVariables();
                    })
                    .ConfigureServices((context, services) =>
                    {
                        // Register RestaurantExtensionService with configuration
                        services.AddSingleton<RestaurantExtensionService>(serviceProvider =>
                        {
                            var logger = serviceProvider.GetRequiredService<ILogger<RestaurantExtensionService>>();
                            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
                            
                            // Get database path from configuration - try different approaches
                            var databasePath = configuration["Restaurant:DatabasePath"];
                            if (string.IsNullOrEmpty(databasePath))
                            {
                                databasePath = configuration.GetValue<string>("Restaurant:DatabasePath");
                            }
                            
                            // Debug: Log what we're reading from configuration
                            Console.WriteLine($"🔧 Configuration DatabasePath: '{databasePath}'");
                            Console.WriteLine($"🔧 Full configuration dump:");
                            Console.WriteLine($"   Restaurant:DatabasePath = '{configuration["Restaurant:DatabasePath"]}'");
                            Console.WriteLine($"   Restaurant:StoreType = '{configuration["Restaurant:StoreType"]}'");
                            
                            if (string.IsNullOrEmpty(databasePath))
                            {
                                Console.WriteLine("⚠️  DatabasePath is null/empty, using default");
                                Console.WriteLine("📁 Current working directory: " + Directory.GetCurrentDirectory());
                                Console.WriteLine("📄 Looking for appsettings.json in current directory");
                            }
                            else
                            {
                                Console.WriteLine($"✅ Using configured database path: {databasePath}");
                            }
                            
                            return new RestaurantExtensionService(logger, databasePath: databasePath);
                        });
                        
                        services.AddHostedService<RestaurantExtensionHost>();
                    })
                    .ConfigureLogging(logging =>
                    {
                        logging.ClearProviders();
                        logging.AddConsole();
                        logging.SetMinimumLevel(LogLevel.Information);
                    })
                    .Build();

                // Run the service
                await host.RunAsync();

                return 0;
            }
            catch (Exception ex) {
                Console.Error.WriteLine($"Fatal error: {ex}");
                return 1;
            }
        }
    }

    /// <summary>
    /// Hosted service wrapper for the Restaurant Extension Service.
    /// </summary>
    public class RestaurantExtensionHost : BackgroundService
    {
        private readonly RestaurantExtensionService _service;
        private readonly ILogger<RestaurantExtensionHost> _logger;

        /// <summary>
        /// Initializes a new instance of the RestaurantExtensionHost.
        /// </summary>
        /// <param name="service">The restaurant extension service.</param>
        /// <param name="logger">The logger.</param>
        public RestaurantExtensionHost(RestaurantExtensionService service, ILogger<RestaurantExtensionHost> logger)
        {
            _service = service;
            _logger = logger;
        }

        /// <summary>
        /// Executes the background service.
        /// </summary>
        /// <param name="stoppingToken">Cancellation token.</param>
        /// <returns>A task representing the operation.</returns>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Starting Restaurant Extension Service...");
            
            try
            {
                await _service.StartAsync(stoppingToken);
                
                // Keep running until cancellation is requested
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Restaurant Extension Service");
                throw;
            }
            finally
            {
                _logger.LogInformation("Stopping Restaurant Extension Service...");
                await _service.StopAsync(CancellationToken.None);
            }
        }
    }
}
