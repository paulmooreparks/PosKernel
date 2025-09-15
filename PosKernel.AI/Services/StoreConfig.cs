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
        /// Gets or sets the store type.
        /// </summary>
        public StoreType StoreType { get; set; }

        /// <summary>
        /// Gets or sets the personality type for this store.
        /// </summary>
        public PersonalityType PersonalityType { get; set; }

        /// <summary>
        /// Gets or sets the catalog provider type.
        /// </summary>
        public CatalogProviderType CatalogProvider { get; set; }

        /// <summary>
        /// Gets or sets the store name.
        /// </summary>
        public string StoreName { get; set; } = "";

        /// <summary>
        /// Gets or sets the store description.
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// Gets or sets the primary currency for this store.
        /// </summary>
        public string Currency { get; set; } = "USD";

        /// <summary>
        /// Gets or sets the store location/culture code.
        /// </summary>
        public string CultureCode { get; set; } = "en-US";

        /// <summary>
        /// Gets or sets whether to use the real kernel integration.
        /// </summary>
        public bool UseRealKernel { get; set; } = false;

        /// <summary>
        /// Gets or sets additional store-specific configuration.
        /// </summary>
        public Dictionary<string, object> AdditionalConfig { get; set; } = new();
    }

    /// <summary>
    /// Available store types with different product catalogs and business models.
    /// </summary>
    public enum StoreType
    {
        /// <summary>
        /// American coffee shop - coffee drinks, pastries, sandwiches.
        /// </summary>
        CoffeeShop,
        
        /// <summary>
        /// Singaporean kopitiam - traditional drinks and local food.
        /// </summary>
        Kopitiam,
        
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
                StoreType.CoffeeShop => CreateCoffeeShop(useRealKernel),
                StoreType.Kopitiam => CreateKopitiam(useRealKernel),
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
                StoreType = StoreType.CoffeeShop,
                PersonalityType = PersonalityType.AmericanBarista,
                CatalogProvider = useRealKernel ? CatalogProviderType.RestaurantExtension : CatalogProviderType.InMemory,
                StoreName = "Brew & Bean Coffee",
                Description = "Artisan coffee shop with fresh roasted beans and homemade pastries",
                Currency = "USD",
                CultureCode = "en-US",
                UseRealKernel = useRealKernel,
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
                StoreType = StoreType.Kopitiam,
                PersonalityType = PersonalityType.SingaporeanKopitiamUncle,
                CatalogProvider = useRealKernel ? CatalogProviderType.RestaurantExtension : CatalogProviderType.Mock,
                StoreName = "Uncle's Traditional Kopitiam",
                Description = "Authentic Singaporean kopitiam with traditional drinks and local favorites",
                Currency = "SGD",
                CultureCode = "en-SG",
                UseRealKernel = useRealKernel,
                AdditionalConfig = new Dictionary<string, object>
                {
                    ["specialty"] = "Traditional kopitiam culture",
                    ["atmosphere"] = "Busy and authentic",
                    ["target_service_time"] = "2-4 minutes",
                    ["languages"] = new[] { "English", "Mandarin", "Cantonese", "Hokkien", "Malay" }
                }
            };
        }

        private static StoreConfig CreateBoulangerie(bool useRealKernel)
        {
            return new StoreConfig
            {
                StoreType = StoreType.Boulangerie,
                PersonalityType = PersonalityType.FrenchBoulanger,
                CatalogProvider = useRealKernel ? CatalogProviderType.RestaurantExtension : CatalogProviderType.InMemory,
                StoreName = "La Belle Boulangerie",
                Description = "Traditional French bakery with artisanal breads and pastries",
                Currency = "EUR",
                CultureCode = "fr-FR",
                UseRealKernel = useRealKernel,
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
                StoreType = StoreType.ConvenienceStore,
                PersonalityType = PersonalityType.JapaneseConbiniClerk,
                CatalogProvider = useRealKernel ? CatalogProviderType.RestaurantExtension : CatalogProviderType.InMemory,
                StoreName = "Daily Mart",
                Description = "24/7 convenience store with fresh food and essentials",
                Currency = "JPY",
                CultureCode = "ja-JP",
                UseRealKernel = useRealKernel,
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
                StoreType = StoreType.ChaiStall,
                PersonalityType = PersonalityType.IndianChaiWala,
                CatalogProvider = useRealKernel ? CatalogProviderType.RestaurantExtension : CatalogProviderType.InMemory,
                StoreName = "Raj's Chai Corner",
                Description = "Authentic Indian chai stall with fresh tea and local snacks",
                Currency = "INR",
                CultureCode = "hi-IN",
                UseRealKernel = useRealKernel,
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
                StoreType = StoreType.GenericStore,
                PersonalityType = PersonalityType.GenericCashier,
                CatalogProvider = useRealKernel ? CatalogProviderType.RestaurantExtension : CatalogProviderType.InMemory,
                StoreName = "Quick Stop Market",
                Description = "General retail store with diverse products and professional service",
                Currency = "USD",
                CultureCode = "en-US",
                UseRealKernel = useRealKernel,
                AdditionalConfig = new Dictionary<string, object>
                {
                    ["specialty"] = "General retail",
                    ["atmosphere"] = "Professional and efficient",
                    ["target_service_time"] = "2-4 minutes"
                }
            };
        }
    }
}
