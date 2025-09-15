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
using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace PosKernel.Extensions.Restaurant 
{
    /// <summary>
    /// Main entry point for the restaurant domain extension.
    /// This runs as a pure service that provides product catalog operations
    /// via named pipe IPC for any client (AI demo, kernel service, etc.).
    /// </summary>
    class Program 
    {
        static async Task<int> Main(string[] args) 
        {
            try 
            {
                Console.WriteLine("POS Kernel Restaurant Extension v0.4.0");
                Console.WriteLine("Simplified domain service");

                // Create a simple console logger
                using var loggerFactory = LoggerFactory.Create(builder => {
                    builder.AddConsole().SetMinimumLevel(LogLevel.Information);
                });
                var logger = loggerFactory.CreateLogger<Program>();

                // Create basic configuration
                var config = new RestaurantConfig {
                    DatabasePath = Path.Combine("data", "catalog", "restaurant_catalog.db")
                };

                // Initialize database
                await InitializeDatabaseAsync(config.DatabasePath, logger);

                // Simple console mode for testing
                logger.LogInformation("Restaurant extension initialized. Type 'exit' to quit.");
                
                string? input;
                do {
                    Console.Write("Enter command (or 'exit'): ");
                    input = Console.ReadLine();
                    
                    if (input == "test") {
                        // Test the extension
                        var context = new ValidationContext {
                            StoreId = "STORE_COFFEE_001",
                            TerminalId = "TERMINAL_01",
                            OperatorId = "TEST"
                        };
                        
                        var result = ValidateProduct("COFFEE_SMALL", context);
                        Console.WriteLine($"Product validation result: {result.IsValid}");
                        if (result.ProductInfo != null) {
                            Console.WriteLine($"Product: {result.ProductInfo.Name} - ${result.EffectivePriceCents / 100.0:F2}");
                        }
                    }
                } while (input != "exit");

                logger.LogInformation("Restaurant extension service stopped");
                return 0;
            }
            catch (Exception ex) {
                Console.Error.WriteLine($"Fatal error: {ex}");
                return 1;
            }
        }

        private static async Task InitializeDatabaseAsync(string databasePath, ILogger logger)
        {
            // Ensure database directory exists
            var dbDirectory = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrEmpty(dbDirectory) && !Directory.Exists(dbDirectory))
            {
                Directory.CreateDirectory(dbDirectory);
                logger.LogInformation("Created database directory: {Directory}", dbDirectory);
            }

            // Check if database exists and is initialized
            var dbExists = File.Exists(databasePath);
            
            using var connection = new SqliteConnection($"Data Source={databasePath}");
            connection.Open();

            if (!dbExists)
            {
                logger.LogInformation("Initializing new restaurant catalog database: {DatabasePath}", databasePath);
                
                // Read and execute schema script
                var schemaPath = Path.Combine("data", "catalog", "restaurant_catalog_schema.sql");
                if (File.Exists(schemaPath))
                {
                    var schema = await File.ReadAllTextAsync(schemaPath);
                    using var command = connection.CreateCommand();
                    command.CommandText = schema;
                    command.ExecuteNonQuery();
                    logger.LogInformation("Database schema created successfully");
                }
                else
                {
                    logger.LogWarning("Schema file not found: {SchemaPath}", schemaPath);
                }

                // Read and execute data script
                var dataPath = Path.Combine("data", "catalog", "restaurant_catalog_data.sql");
                if (File.Exists(dataPath))
                {
                    var data = await File.ReadAllTextAsync(dataPath);
                    using var command = connection.CreateCommand();
                    command.CommandText = data;
                    command.ExecuteNonQuery();
                    logger.LogInformation("Sample data loaded successfully");
                }
                else
                {
                    logger.LogWarning("Data file not found: {DataPath}", dataPath);
                }
            }
            else
            {
                // Verify database integrity
                using var command = connection.CreateCommand();
                command.CommandText = "PRAGMA integrity_check";
                var result = command.ExecuteScalar()?.ToString();
                
                if (result != "ok")
                {
                    throw new InvalidOperationException($"Database integrity check failed: {result}");
                }
                
                logger.LogInformation("Database integrity verified: {DatabasePath}", databasePath);
            }
        }

        public static ProductValidationResult ValidateProduct(string productId, ValidationContext context) 
        {
            // Validate product implementation
            // For demo, let's assume any product ID containing "COFFEE" is valid
            if (productId.Contains("COFFEE")) 
            {
                return new ProductValidationResult 
                {
                    IsValid = true,
                    ProductInfo = new ProductInfo 
                    {
                        Name = "Sample Coffee",
                        Description = "A delicious cup of coffee"
                    },
                    EffectivePriceCents = 299
                };
            }

            return new ProductValidationResult 
            {
                IsValid = false,
                ErrorMessage = "Product not found"
            };
        }
    }
}
