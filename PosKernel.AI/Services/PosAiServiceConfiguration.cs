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

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PosKernel.Abstractions;
using PosKernel.Abstractions.Services;
using PosKernel.AI.Common.Abstractions;
using PosKernel.AI.Common.Factories;
using PosKernel.AI.Core;
using PosKernel.AI.Interfaces;
using PosKernel.AI.Models;
using PosKernel.AI.Services;
using PosKernel.AI.Tools;
using PosKernel.Configuration;
using PosKernel.Configuration.Services;
using PosKernel.Extensions.Restaurant.Client;

namespace PosKernel.AI.Services
{
    /// <summary>
    /// Shared service configuration for POS AI applications.
    /// Extracted from PosKernel.AI.Demo to enable reuse across different hosting models
    /// (TUI, headless service, CLI, etc.)
    ///
    /// ARCHITECTURAL PRINCIPLE: Service composition logic should be reusable
    /// across different user interfaces and hosting models.
    /// </summary>
    public static class PosAiServiceConfiguration
    {
        /// <summary>
        /// Configure the complete POS AI service stack in a DI container.
        /// This includes AI models, store configuration, restaurant extension client,
        /// and all the necessary services for running a POS AI cashier.
        /// </summary>
        /// <param name="services">Service collection to configure</param>
        /// <param name="storeConfig">Store configuration including payment methods</param>
        /// <param name="personality">AI personality configuration</param>
        /// <param name="showDebug">Whether to enable debug logging</param>
        /// <returns>Configured service collection</returns>
        public static async Task<IServiceCollection> ConfigurePosAiServicesAsync(
            this IServiceCollection services,
            StoreConfig storeConfig,
            AiPersonalityConfig personality,
            bool showDebug = false)
        {
            // Initialize configuration system
            var config = PosKernelConfiguration.Initialize();
            var storeAiConfig = new AiConfiguration(config);

            // Validate AI configuration (fail fast)
            var provider = storeAiConfig.Provider;
            var model = storeAiConfig.Model;
            var baseUrl = storeAiConfig.BaseUrl;
            var apiKey = storeAiConfig.ApiKey;

            if (string.IsNullOrWhiteSpace(provider))
            {
                throw new InvalidOperationException(
                    "DESIGN DEFICIENCY: Store AI provider not configured! " +
                    "Set STORE_AI_PROVIDER in ~/.poskernel/.env (e.g., STORE_AI_PROVIDER=Ollama)");
            }

            if (provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException(
                    "DESIGN DEFICIENCY: OpenAI API key required when STORE_AI_PROVIDER=OpenAI. " +
                    "Set OPENAI_API_KEY in ~/.poskernel/.env");
            }

            // Configure logging
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddConsole();
                builder.SetMinimumLevel(showDebug ? LogLevel.Debug : LogLevel.Information);
            });

            // Add configuration services
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection()
                .Build();

            services.AddSingleton<IConfiguration>(configuration);
            services.AddSingleton<IStoreConfigurationService, SimpleStoreConfigurationService>();
            services.AddSingleton<ICurrencyFormattingService, CurrencyFormattingService>();

            // Configure AI model
            services.AddSingleton<ILargeLanguageModel>(serviceProvider =>
            {
                var logger = serviceProvider.GetRequiredService<ILogger<ILargeLanguageModel>>();
                return LLMProviderFactory.CreateLLM(provider, model, baseUrl, apiKey, logger);
            });

            // Configure MCP client
            var mcpConfig = storeAiConfig.ToMcpConfiguration();
            services.AddSingleton(mcpConfig);
            services.AddSingleton<McpClient>();

            // Configure payment methods if not already set
            if (storeConfig.PaymentMethods == null)
            {
                storeConfig.PaymentMethods = new PaymentMethodsConfiguration
                {
                    AcceptsCash = true,
                    AcceptsDigitalPayments = true,
                    DefaultMethod = "cash",
                    PaymentInstructions = GetPaymentInstructionsForStore(storeConfig),
                    AcceptedMethods = GetAcceptedPaymentMethodsForStore(storeConfig)
                };
            }

            // Test and configure HTTP restaurant extension client
            var tempServiceProvider = services.BuildServiceProvider();
            var tempLogger = tempServiceProvider.GetRequiredService<ILogger<HttpRestaurantExtensionClient>>();
            var httpRestaurantClient = new HttpRestaurantExtensionClient(tempLogger);

            // Test the HTTP connection
            var connectionSuccess = await httpRestaurantClient.TestConnectionAsync();
            if (!connectionSuccess)
            {
                throw new InvalidOperationException(
                    "DESIGN DEFICIENCY: Failed to connect to Restaurant Extension via HTTP. " +
                    "Ensure Restaurant Extension service is running on http://localhost:8081.");
            }

            var testProducts = await httpRestaurantClient.SearchProductsAsync("kopi", 3);
            Console.WriteLine($"🔍 HTTP API Test: Found {testProducts.Count} products for 'kopi' search");

            // Add services to DI
            services.AddSingleton<HttpRestaurantExtensionClient>(_ => httpRestaurantClient);
            services.AddSingleton<KernelPosToolsProvider>(provider =>
                new KernelPosToolsProvider(
                    httpRestaurantClient,
                    provider.GetRequiredService<ILogger<KernelPosToolsProvider>>(),
                    storeConfig,
                    PosKernel.Client.PosKernelClientFactory.KernelType.HttpService,
                    configuration,
                    provider.GetRequiredService<ICurrencyFormattingService>()));

            // Configure context builder
            services.AddSingleton<ConfigurableContextBuilder>(provider =>
                new ConfigurableContextBuilder(
                    personality.Type,
                    provider.GetRequiredService<ILogger<ConfigurableContextBuilder>>()));

            // Configure POS AI agent
            services.AddSingleton<PosAiAgent>(provider =>
            {
                return new PosAiAgent(
                    provider.GetRequiredService<McpClient>(),
                    provider.GetRequiredService<ILogger<PosAiAgent>>(),
                    storeConfig,
                    new Receipt(),
                    new Transaction(storeConfig.Currency),
                    provider.GetRequiredService<ConfigurableContextBuilder>(),
                    provider.GetRequiredService<KernelPosToolsProvider>());
            });

            // Add store and personality configuration
            services.AddSingleton(storeConfig);
            services.AddSingleton(personality);

            tempServiceProvider.Dispose();
            return services;
        }

        private static string GetPaymentInstructionsForStore(StoreConfig storeConfig)
        {
            return storeConfig.StoreType switch
            {
                StoreType.Kopitiam => "Cash preferred, digital payments accepted for orders above S$5",
                StoreType.CoffeeShop => "All major payment methods accepted",
                StoreType.Boulangerie => "Cash and cards accepted, contactless preferred",
                StoreType.ConvenienceStore => "All payment methods accepted for quick service",
                StoreType.ChaiStall => "Cash preferred, digital payments for larger orders",
                _ => "Multiple payment options available"
            };
        }

        private static List<PaymentMethodConfig> GetAcceptedPaymentMethodsForStore(StoreConfig storeConfig)
        {
            var methods = new List<PaymentMethodConfig>();

            // All stores accept cash
            methods.Add(new PaymentMethodConfig
            {
                MethodId = "cash",
                DisplayName = "Cash",
                Type = PaymentMethodType.Cash,
                IsEnabled = true
            });

            // Add region-specific digital payment methods
            switch (storeConfig.StoreType)
            {
                case StoreType.Kopitiam:
                    methods.Add(new PaymentMethodConfig
                    {
                        MethodId = "paynow",
                        DisplayName = "PayNow",
                        Type = PaymentMethodType.DigitalWallet,
                        IsEnabled = true
                    });
                    methods.Add(new PaymentMethodConfig
                    {
                        MethodId = "grabpay",
                        DisplayName = "GrabPay",
                        Type = PaymentMethodType.DigitalWallet,
                        IsEnabled = true
                    });
                    break;

                case StoreType.CoffeeShop:
                case StoreType.Boulangerie:
                case StoreType.ConvenienceStore:
                    methods.Add(new PaymentMethodConfig
                    {
                        MethodId = "card",
                        DisplayName = "Credit/Debit Card",
                        Type = PaymentMethodType.CreditCard,
                        IsEnabled = true
                    });
                    methods.Add(new PaymentMethodConfig
                    {
                        MethodId = "contactless",
                        DisplayName = "Contactless Payment",
                        Type = PaymentMethodType.CreditCard, // Using CreditCard as closest match
                        IsEnabled = true
                    });
                    break;

                case StoreType.ChaiStall:
                    methods.Add(new PaymentMethodConfig
                    {
                        MethodId = "upi",
                        DisplayName = "UPI",
                        Type = PaymentMethodType.DigitalWallet,
                        IsEnabled = true
                    });
                    break;
            }

            return methods;
        }
    }
}
