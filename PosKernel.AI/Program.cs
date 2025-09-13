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
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PosKernel.AI.Core;
using PosKernel.AI.Services;
using PosKernel.AI.Examples;
using PosKernel.AI.Implementation;

namespace PosKernel.AI
{
    /// <summary>
    /// AI integration startup and demonstration program.
    /// Shows Phase 1 AI capabilities including MCP integration, natural language processing,
    /// and intelligent POS assistance while maintaining kernel isolation.
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("🧠 POS Kernel AI Integration - Phase 1 Demo");
            Console.WriteLine("============================================");
            Console.WriteLine();

            try
            {
                // Set up dependency injection for AI services
                var services = ConfigureServices();
                var serviceProvider = services.BuildServiceProvider();

                // Initialize AI services
                using var mcpClient = serviceProvider.GetRequiredService<McpClient>();
                var aiService = serviceProvider.GetRequiredService<PosAiService>();
                var demo = new AiPosDemo(aiService, mcpClient);

                Console.WriteLine("🚀 AI Services initialized successfully");
                Console.WriteLine("💡 Demonstrating AI capabilities while keeping kernel pure");
                Console.WriteLine();

                // Run AI demonstrations
                await demo.DemonstrateNaturalLanguageTransactionAsync();
                await demo.DemonstrateFraudDetectionAsync();
                await demo.DemonstrateBusinessIntelligenceAsync();
                await demo.DemonstrateIntelligentRecommendationsAsync();

                Console.WriteLine("✅ AI Integration Phase 1 Demo Complete!");
                Console.WriteLine();
                Console.WriteLine("🎯 Next Steps:");
                Console.WriteLine("  - Phase 2: Multi-modal AI (Voice, Vision)");
                Console.WriteLine("  - Phase 3: AI Extensions Marketplace");
                Console.WriteLine("  - Integration with regional CAL providers");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ AI Demo failed: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"   Inner exception: {ex.InnerException.Message}");
                }
                Environment.Exit(1);
            }

            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        /// <summary>
        /// Configures dependency injection for AI services.
        /// </summary>
        private static IServiceCollection ConfigureServices()
        {
            var services = new ServiceCollection();

            // Add logging
            services.AddLogging(builder => builder.AddConsole());

            // Configure MCP client
            services.AddSingleton<McpConfiguration>(provider =>
            {
                return new McpConfiguration
                {
                    BaseUrl = Environment.GetEnvironmentVariable("MCP_BASE_URL") ?? "https://api.openai.com/v1",
                    ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY"), // Optional for demo
                    TimeoutSeconds = 30,
                    DefaultParameters = new()
                    {
                        ["model"] = "gpt-4",
                        ["temperature"] = 0.3,
                        ["max_tokens"] = 1000
                    }
                };
            });

            // Add security services
            services.AddSingleton<IPromptSanitizer, BasicPromptSanitizer>();
            services.AddSingleton<IResponseValidator, BasicResponseValidator>();
            services.AddSingleton<IAuditLogger, ConsoleAuditLogger>();
            services.AddSingleton<IAiSecurityService, AiSecurityService>();

            // Add context providers
            services.AddSingleton<ITransactionContextProvider, BasicTransactionContextProvider>();
            services.AddSingleton<IAiPromptEngine, BasicAiPromptEngine>();

            // Add core services
            services.AddSingleton<McpClient>();
            services.AddSingleton<PosAiService>();

            return services;
        }
    }
}
