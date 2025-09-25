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

namespace PosKernel.AI.Services
{
    /// <summary>
    /// Configuration for different store types and their associated personalities and catalogs.
    /// </summary>
    public class StoreConfig
    {
        /// <summary>
        /// Gets or sets the unique store identifier.
        /// </summary>
        public string StoreId { get; set; } = "";

        /// <summary>
        /// Gets or sets the store name.
        /// </summary>
        public string StoreName { get; set; } = "";

        /// <summary>
        /// Gets or sets the store type.
        /// </summary>
        public StoreType StoreType { get; set; }

        /// <summary>
        /// Gets or sets the AI personality type for this store.
        /// </summary>
        public PersonalityType PersonalityType { get; set; }

        /// <summary>
        /// Gets or sets the primary currency code (ISO 4217).
        /// </summary>
        public string Currency { get; set; } = "";

        /// <summary>
        /// Gets or sets the culture code for localization.
        /// </summary>
        public string CultureCode { get; set; } = "";

        /// <summary>
        /// Gets or sets the catalog provider type.
        /// </summary>
        public CatalogProviderType CatalogProvider { get; set; }

        /// <summary>
        /// ARCHITECTURAL ENHANCEMENT: Store-specific payment methods configuration.
        /// PRINCIPLE: Each store defines what payment methods it accepts - no hardcoded assumptions.
        /// </summary>
        public PaymentMethodsConfiguration PaymentMethods { get; set; } = new();

        /// <summary>
        /// Gets or sets additional store configuration.
        /// </summary>
        public Dictionary<string, object> AdditionalConfig { get; set; } = new();
    }

    /// <summary>
    /// Configuration for payment methods accepted by a store.
    /// ARCHITECTURAL PRINCIPLE: Payment methods are store-specific configuration, not universal constants.
    /// </summary>
    public class PaymentMethodsConfiguration
    {
        /// <summary>
        /// Gets or sets the list of accepted payment methods for this store.
        /// ARCHITECTURAL PRINCIPLE: Each store explicitly defines what it accepts.
        /// </summary>
        public List<PaymentMethodConfig> AcceptedMethods { get; set; } = new();

        /// <summary>
        /// Gets or sets the default payment method (optional).
        /// Used for suggestions, not assumptions.
        /// </summary>
        public string? DefaultMethod { get; set; }

        /// <summary>
        /// Gets or sets whether cash is accepted (most stores accept cash, but some may not).
        /// </summary>
        public bool AcceptsCash { get; set; } = true;

        /// <summary>
        /// Gets or sets whether digital payments are accepted.
        /// </summary>
        public bool AcceptsDigitalPayments { get; set; } = true;

        /// <summary>
        /// Gets or sets store-specific payment instructions or requirements.
        /// </summary>
        public string? PaymentInstructions { get; set; }
    }

    /// <summary>
    /// Configuration for a specific payment method.
    /// ARCHITECTURAL PRINCIPLE: Payment methods are configurable, not hardcoded constants.
    /// </summary>
    public class PaymentMethodConfig
    {
        /// <summary>
        /// Gets or sets the payment method identifier (e.g., "cash", "visa", "apple_pay").
        /// </summary>
        public string MethodId { get; set; } = "";

        /// <summary>
        /// Gets or sets the display name for this payment method.
        /// </summary>
        public string DisplayName { get; set; } = "";

        /// <summary>
        /// Gets or sets the payment method type category.
        /// </summary>
        public PaymentMethodType Type { get; set; }

        /// <summary>
        /// Gets or sets whether this payment method is currently enabled.
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the minimum amount for this payment method (if any).
        /// </summary>
        public decimal? MinimumAmount { get; set; }

        /// <summary>
        /// Gets or sets the maximum amount for this payment method (if any).
        /// </summary>
        public decimal? MaximumAmount { get; set; }

        /// <summary>
        /// Gets or sets additional configuration for this payment method.
        /// </summary>
        public Dictionary<string, object> AdditionalConfig { get; set; } = new();
    }

    /// <summary>
    /// Categories of payment methods for organizational purposes.
    /// ARCHITECTURAL PRINCIPLE: Categories are configurable organizational tools, not business logic.
    /// </summary>
    public enum PaymentMethodType
    {
        Cash,
        CreditCard,
        DebitCard,
        DigitalWallet,
        BankTransfer,
        Cryptocurrency,
        StoreCredit,
        GiftCard,
        Other
    }

    /// <summary>
    /// Available store types with different product catalogs and business models.
    /// </summary>
    public enum StoreType
    {
        /// <summary>
        /// Singaporean kopitiam - traditional drinks and local food.
        /// </summary>
        Kopitiam,
        
        /// <summary>
        /// American coffee shop - coffee drinks, pastries, sandwiches.
        /// </summary>
        CoffeeShop,
        
        /// <summary>
        /// French bakery - bread, pastries, coffee, French specialties.
        /// </summary>
        Boulangerie,
        
        /// <summary>
        /// Japanese convenience store - diverse items, efficient service.
        /// </summary>
        ConvenienceStore,
        
        /// <summary>
        /// Indian chai stall - tea varieties, snacks, street food.
        /// </summary>
        ChaiStall,
        
        /// <summary>
        /// Generic retail store - flexible catalog, neutral personality.
        /// </summary>
        GenericStore
    }

    /// <summary>
    /// Available catalog provider types for different store types.
    /// </summary>
    public enum CatalogProviderType
    {
        /// <summary>
        /// Mock catalog for demonstrations and testing.
        /// </summary>
        Mock,
        
        /// <summary>
        /// Restaurant extension with full database catalog.
        /// </summary>
        RestaurantExtension,
        
        /// <summary>
        /// Simple in-memory catalog for basic demos.
        /// </summary>
        InMemory,
        
        /// <summary>
        /// Real external catalog service integration.
        /// </summary>
        External
    }

    /// <summary>
    /// Factory for creating store configurations for different business types.
    /// </summary>
    public static class StoreConfigFactory
    {
        /// <summary>
        /// Creates a store configuration for the specified store type.
        /// </summary>
        /// <param name="storeType">The type of store to configure.</param>
        /// <param name="useRealKernel">Whether to use real kernel integration.</param>
        /// <returns>A configured store instance.</returns>
        public static StoreConfig CreateStore(StoreType storeType, bool useRealKernel = false)
        {
            return storeType switch
            {
                StoreType.Kopitiam => CreateKopitiam(useRealKernel),
                StoreType.CoffeeShop => CreateCoffeeShop(useRealKernel),
                StoreType.Boulangerie => CreateBoulangerie(useRealKernel),
                StoreType.ConvenienceStore => CreateConvenienceStore(useRealKernel),
                StoreType.ChaiStall => CreateChaiStall(useRealKernel),
                StoreType.GenericStore => CreateGenericStore(useRealKernel),
                _ => throw new ArgumentOutOfRangeException(nameof(storeType), storeType, "Unknown store type")
            };
        }

        /// <summary>
        /// Gets all available store types for demonstration purposes.
        /// </summary>
        /// <returns>List of available store configurations.</returns>
        public static List<StoreConfig> GetAvailableStores(bool useRealKernel = false)
        {
            return Enum.GetValues<StoreType>()
                       .Select(storeType => CreateStore(storeType, useRealKernel))
                       .ToList();
        }

        private static StoreConfig CreateCoffeeShop(bool useRealKernel)
        {
            return new StoreConfig
            {
                StoreId = "coffee-shop-01",
                StoreType = StoreType.CoffeeShop,
                PersonalityType = PersonalityType.AmericanBarista,
                CatalogProvider = useRealKernel ? CatalogProviderType.RestaurantExtension : CatalogProviderType.InMemory,
                StoreName = "Brew & Bean Coffee",
                Currency = "USD",
                CultureCode = "en-US",
                
                // ARCHITECTURAL FIX: Proper payment methods configuration
                PaymentMethods = new PaymentMethodsConfiguration
                {
                    AcceptsCash = true,
                    AcceptsDigitalPayments = true,
                    DefaultMethod = "card",
                    PaymentInstructions = "All major cards and digital wallets accepted",
                    AcceptedMethods = new List<PaymentMethodConfig>
                    {
                        new() { 
                            MethodId = "cash", 
                            DisplayName = "Cash", 
                            Type = PaymentMethodType.Cash, 
                            IsEnabled = true 
                        },
                        new() { 
                            MethodId = "visa", 
                            DisplayName = "Visa", 
                            Type = PaymentMethodType.CreditCard, 
                            IsEnabled = true
                        },
                        new() { 
                            MethodId = "mastercard", 
                            DisplayName = "MasterCard", 
                            Type = PaymentMethodType.CreditCard, 
                            IsEnabled = true
                        },
                        new() { 
                            MethodId = "apple_pay", 
                            DisplayName = "Apple Pay", 
                            Type = PaymentMethodType.DigitalWallet, 
                            IsEnabled = true
                        },
                        new() { 
                            MethodId = "google_pay", 
                            DisplayName = "Google Pay", 
                            Type = PaymentMethodType.DigitalWallet, 
                            IsEnabled = true
                        }
                    }
                },
                
                AdditionalConfig = new Dictionary<string, object>
                {
                    ["specialty"] = "Third-wave coffee",
                    ["atmosphere"] = "Cozy and modern",
                    ["target_service_time"] = "3-5 minutes"
                }
            };
        }

        private static StoreConfig CreateKopitiam(bool useRealKernel)
        {
            return new StoreConfig
            {
                StoreId = "kopitiam-01",
                StoreType = StoreType.Kopitiam,
                PersonalityType = PersonalityType.SingaporeanKopitiamUncle,
                CatalogProvider = useRealKernel ? CatalogProviderType.RestaurantExtension : CatalogProviderType.Mock,
                StoreName = "Uncle's Traditional Kopitiam",
                Currency = "SGD",
                CultureCode = "en-SG",
                
                // ARCHITECTURAL FIX: Proper payment methods configuration
                PaymentMethods = new PaymentMethodsConfiguration
                {
                    AcceptsCash = true,
                    AcceptsDigitalPayments = true,
                    DefaultMethod = "cash",
                    PaymentInstructions = "Cash preferred, digital payments accepted",
                    AcceptedMethods = new List<PaymentMethodConfig>
                    {
                        new() { 
                            MethodId = "cash", 
                            DisplayName = "Cash", 
                            Type = PaymentMethodType.Cash, 
                            IsEnabled = true 
                        },
                        new() { 
                            MethodId = "paynow", 
                            DisplayName = "PayNow", 
                            Type = PaymentMethodType.BankTransfer, 
                            IsEnabled = true,
                            MinimumAmount = 1.00m
                        },
                        new() { 
                            MethodId = "nets", 
                            DisplayName = "NETS", 
                            Type = PaymentMethodType.DebitCard, 
                            IsEnabled = true,
                            MinimumAmount = 5.00m
                        },
                        new() { 
                            MethodId = "grabpay", 
                            DisplayName = "GrabPay", 
                            Type = PaymentMethodType.DigitalWallet, 
                            IsEnabled = true,
                            MinimumAmount = 2.00m
                        }
                    }
                },
                
                AdditionalConfig = new Dictionary<string, object>
                {
                    ["specialty"] = "Traditional kopitiam culture",
                    ["atmosphere"] = "Busy and authentic",
                    ["target_service_time"] = "2-4 minutes",
                    ["languages"] = new[] { "English", "Mandarin", "Cantonese", "Hokkien", "Malay", "Tamil" },
                    ["terminal_id"] = "AI_TERMINAL",
                    ["operator_id"] = "AI_ASSISTANT"
                }
            };
        }

        private static StoreConfig CreateBoulangerie(bool useRealKernel)
        {
            return new StoreConfig
            {
                StoreId = "boulangerie-01",
                StoreType = StoreType.Boulangerie,
                PersonalityType = PersonalityType.FrenchBoulanger,
                CatalogProvider = useRealKernel ? CatalogProviderType.RestaurantExtension : CatalogProviderType.InMemory,
                StoreName = "La Belle Boulangerie",
                Currency = "EUR",
                CultureCode = "fr-FR",
                
                // ARCHITECTURAL FIX: Proper payment methods configuration
                PaymentMethods = new PaymentMethodsConfiguration
                {
                    AcceptsCash = true,
                    AcceptsDigitalPayments = true,
                    DefaultMethod = "cash",
                    PaymentInstructions = "Espèces préférées, cartes acceptées",
                    AcceptedMethods = new List<PaymentMethodConfig>
                    {
                        new() { 
                            MethodId = "cash", 
                            DisplayName = "Espèces", 
                            Type = PaymentMethodType.Cash, 
                            IsEnabled = true 
                        },
                        new() { 
                            MethodId = "carte_bancaire", 
                            DisplayName = "Carte Bancaire", 
                            Type = PaymentMethodType.DebitCard, 
                            IsEnabled = true,
                            MinimumAmount = 5.00m
                        },
                        new() { 
                            MethodId = "contactless", 
                            DisplayName = "Sans Contact", 
                            Type = PaymentMethodType.DigitalWallet, 
                            IsEnabled = true
                        }
                    }
                },
                
                AdditionalConfig = new Dictionary<string, object>
                {
                    ["specialty"] = "Artisanal French baking",
                    ["atmosphere"] = "Traditional and elegant",
                    ["target_service_time"] = "4-6 minutes",
                    ["daily_specials"] = true
                }
            };
        }

        private static StoreConfig CreateConvenienceStore(bool useRealKernel)
        {
            return new StoreConfig
            {
                StoreId = "convenience-01",
                StoreType = StoreType.ConvenienceStore,
                PersonalityType = PersonalityType.JapaneseConbiniClerk,
                CatalogProvider = useRealKernel ? CatalogProviderType.RestaurantExtension : CatalogProviderType.InMemory,
                StoreName = "Daily Mart",
                Currency = "JPY",
                CultureCode = "ja-JP",
                AdditionalConfig = new Dictionary<string, object>
                {
                    ["specialty"] = "Convenience and efficiency",
                    ["atmosphere"] = "Clean and organized",
                    ["target_service_time"] = "1-2 minutes",
                    ["operating_hours"] = "24/7"
                }
            };
        }

        private static StoreConfig CreateChaiStall(bool useRealKernel)
        {
            return new StoreConfig
            {
                StoreId = "chai-stall-01",
                StoreType = StoreType.ChaiStall,
                PersonalityType = PersonalityType.IndianChaiWala,
                CatalogProvider = useRealKernel ? CatalogProviderType.RestaurantExtension : CatalogProviderType.InMemory,
                StoreName = "Raj's Chai Corner",
                Currency = "INR",
                CultureCode = "hi-IN",
                AdditionalConfig = new Dictionary<string, object>
                {
                    ["specialty"] = "Fresh masala chai",
                    ["atmosphere"] = "Bustling and social",
                    ["target_service_time"] = "2-3 minutes",
                    ["languages"] = new[] { "Hindi", "English", "Punjabi" }
                }
            };
        }

        private static StoreConfig CreateGenericStore(bool useRealKernel)
        {
            return new StoreConfig
            {
                StoreId = "generic-01",
                StoreType = StoreType.GenericStore,
                PersonalityType = PersonalityType.GenericCashier,
                CatalogProvider = useRealKernel ? CatalogProviderType.RestaurantExtension : CatalogProviderType.InMemory,
                StoreName = "Quick Stop Market",
                Currency = "USD",
                CultureCode = "en-US",
                AdditionalConfig = new Dictionary<string, object>
                {
                    ["specialty"] = "General retail",
                    ["atmosphere"] = "Professional and efficient",
                    ["target_service_time"] = "2-4 minutes"
                }
            };
        }
        
        /// <summary>
        /// Creates a sample Singapore kopitiam store configuration with realistic payment methods.
        /// ARCHITECTURAL PRINCIPLE: Each store explicitly configures its accepted payment methods.
        /// </summary>
        private static StoreConfig CreateSingaporeKopitiamConfig(bool useRealKernel)
        {
            return new StoreConfig
            {
                StoreId = "kopitiam-01",
                StoreName = "Uncle Lim's Traditional Kopitiam",
                StoreType = StoreType.Kopitiam,
                PersonalityType = PersonalityType.SingaporeanKopitiamUncle,
                Currency = "SGD",
                CultureCode = "en-SG",
                CatalogProvider = CatalogProviderType.RestaurantExtension,
                
                // ARCHITECTURAL ENHANCEMENT: Store-specific payment methods configuration
                PaymentMethods = new PaymentMethodsConfiguration
                {
                    AcceptsCash = true,
                    AcceptsDigitalPayments = true,
                    DefaultMethod = "cash", // Most kopitiam customers pay cash
                    PaymentInstructions = "Cash preferred, digital payments accepted for orders above S$5",
                    AcceptedMethods = new List<PaymentMethodConfig>
                    {
                        new() { 
                            MethodId = "cash", 
                            DisplayName = "Cash", 
                            Type = PaymentMethodType.Cash, 
                            IsEnabled = true 
                        },
                        new() { 
                            MethodId = "nets", 
                            DisplayName = "NETS", 
                            Type = PaymentMethodType.DebitCard, 
                            IsEnabled = true,
                            MinimumAmount = 5.00m
                        },
                        new() { 
                            MethodId = "grabpay", 
                            DisplayName = "GrabPay", 
                            Type = PaymentMethodType.DigitalWallet, 
                            IsEnabled = true,
                            MinimumAmount = 2.00m
                        },
                        new() { 
                            MethodId = "paynow", 
                            DisplayName = "PayNow QR", 
                            Type = PaymentMethodType.BankTransfer, 
                            IsEnabled = true,
                            MinimumAmount = 1.00m
                        }
                    }
                },

                AdditionalConfig = new Dictionary<string, object>
                {
                    {"terminal_id", "KOPITIAM_01_T1"},
                    {"operator_id", "UNCLE_LIM"},
                    {"table_service", false},
                    {"language_support", new[] {"en", "zh", "ms", "ta"}}
                }
            };
        }
    }
}
