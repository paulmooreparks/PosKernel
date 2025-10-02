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
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using System.Text.Json;

namespace PosKernel.Extensions.Restaurant
{
    /// <summary>
    /// Main entry point for the retail domain extension service.
    /// ARCHITECTURAL PRINCIPLE: Generic retail extension that serves any retail store type.
    /// This runs as a hosted service that provides product catalog operations
    /// via named pipe IPC for any client (AI demo, kernel service, etc.).
    /// </summary>
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            try
            {
                Console.WriteLine("🏪 POS Kernel Retail Extension v0.5.0");
                Console.WriteLine("Generic retail domain service with HTTP API and named pipe IPC");

                // ARCHITECTURAL PRINCIPLE: CLI arguments take precedence over configuration files
                var storeTypeOverride = GetCommandLineArgument(args, "--store-type");
                var storeNameOverride = GetCommandLineArgument(args, "--store-name");
                var currencyOverride = GetCommandLineArgument(args, "--currency");

                // Build ASP.NET Core web application
                var builder = WebApplication.CreateBuilder(args);

                // Configure configuration
                builder.Configuration.Sources.Clear();

                // Add configuration from the project directory
                var projectDir = "PosKernel.Extensions.Restaurant";
                var configFile = Path.Combine(projectDir, "appsettings.json");

                Console.WriteLine($"📁 Loading configuration from: {configFile}");

                if (File.Exists(configFile))
                {
                    builder.Configuration.AddJsonFile(configFile, optional: false, reloadOnChange: true);
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

                builder.Configuration.AddEnvironmentVariables();

                // Configure services
                builder.Services.AddSingleton<RetailExtensionService>(serviceProvider =>
                {
                    var logger = serviceProvider.GetRequiredService<ILogger<RetailExtensionService>>();
                    var configuration = serviceProvider.GetRequiredService<IConfiguration>();

                    // ARCHITECTURAL PRINCIPLE: CLI arguments override configuration files
                    var storeType = storeTypeOverride ??
                                   configuration["Retail:StoreType"] ??
                                   "CoffeeShop";

                    Console.WriteLine($"🔧 Configured store type: {storeType}");

                    return new RetailExtensionService(logger, storeType);
                });

                builder.Services.AddHostedService<RetailExtensionHost>();

                // Configure logging
                builder.Logging.ClearProviders();
                builder.Logging.AddConsole();
                builder.Logging.SetMinimumLevel(LogLevel.Information);

                // Configure JSON options
                builder.Services.ConfigureHttpJsonOptions(options =>
                {
                    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                    options.SerializerOptions.WriteIndented = true;
                });

                // Set URLs
                builder.WebHost.UseUrls("http://localhost:8081");

                var app = builder.Build();

                // Get the retail extension service
                var retailService = app.Services.GetRequiredService<RetailExtensionService>();

                // Configure HTTP API endpoints
                app.MapGet("/health", () => Results.Ok(new { Status = "OK", Service = "Restaurant Extension", Version = "0.5.0" }));

                app.MapGet("/info", () =>
                {
                    return Results.Ok(new
                    {
                        Service = "Restaurant Extension",
                        Version = "0.5.0",
                        StoreType = retailService.StoreType,
                        Endpoints = new[] { "/health", "/info", "/products/search", "/products/{id}" }
                    });
                });

                app.MapGet("/products/search", async (string? q, int? limit) =>
                {
                    try
                    {
                        var query = q ?? "";
                        var maxResults = Math.Min(limit ?? 10, 100); // Cap at 100 results

                        var products = await retailService.SearchProductsHttpAsync(query, maxResults);

                        return Results.Ok(new
                        {
                            Query = query,
                            Count = products.Count,
                            Products = products.Select(p => new
                            {
                                Id = p.Sku,
                                Name = p.Name,
                                Price = p.BasePriceCents / 100.0, // Convert back to dollars
                                Category = p.CategoryName,
                                Description = p.Description
                            })
                        });
                    }
                    catch (Exception ex)
                    {
                        return Results.Problem(ex.Message, statusCode: 500);
                    }
                });

                app.MapGet("/products/{id}", async (string id) =>
                {
                    try
                    {
                        var product = await retailService.GetProductByIdHttpAsync(id);

                        if (product == null)
                        {
                            return Results.NotFound(new { Error = $"Product with ID '{id}' not found" });
                        }

                        return Results.Ok(new
                        {
                            Id = product.Sku,
                            Name = product.Name,
                            Price = product.BasePriceCents / 100.0, // Convert back to dollars
                            Category = product.CategoryName,
                            Description = product.Description
                        });
                    }
                    catch (Exception ex)
                    {
                        return Results.Problem(ex.Message, statusCode: 500);
                    }
                });

                Console.WriteLine("🌐 HTTP API available at: http://localhost:8081");
                Console.WriteLine("📡 Named pipe IPC available at: poskernel-restaurant-extension");
                Console.WriteLine();

                // Run the application
                await app.RunAsync();

                return 0;
            }
            catch (Exception ex) {
                Console.Error.WriteLine($"Fatal error: {ex}");
                return 1;
            }
        }

        /// <summary>
        /// Extract a command-line argument value from args array.
        /// ARCHITECTURAL PRINCIPLE: CLI arguments take precedence over configuration files.
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <param name="argumentName">Argument name (e.g., "--store-type")</param>
        /// <returns>Argument value or null if not found</returns>
        private static string? GetCommandLineArgument(string[] args, string argumentName)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i].Equals(argumentName, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }
            return null;
        }
    }

    /// <summary>
    /// Hosted service wrapper for the Retail Extension Service.
    /// </summary>
    public class RetailExtensionHost : BackgroundService
    {
        private readonly RetailExtensionService _service;
        private readonly ILogger<RetailExtensionHost> _logger;

        /// <summary>
        /// Initializes a new instance of the RetailExtensionHost.
        /// </summary>
        /// <param name="service">The retail extension service.</param>
        /// <param name="logger">The logger.</param>
        public RetailExtensionHost(RetailExtensionService service, ILogger<RetailExtensionHost> logger)
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
            _logger.LogInformation("Starting Retail Extension Service...");

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
                _logger.LogError(ex, "Error in Retail Extension Service");
                throw;
            }
            finally
            {
                _logger.LogInformation("Stopping Retail Extension Service...");
                await _service.StopAsync(CancellationToken.None);
            }
        }
    }
}
