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

using PosKernel.Configuration.Services;

namespace PosKernel.AI.Training.Core;

/// <summary>
/// Store configuration service for training that provides proper multi-store configuration
/// ARCHITECTURAL PRINCIPLE: No hardcoded defaults - explicit configuration for all store types
/// ARCHITECTURAL FIX: Support all store types, not just Singapore Kopitiam
/// </summary>
public class TrainingStoreConfigurationService : IStoreConfigurationService
{
    public StoreConfiguration GetStoreConfiguration(string storeId)
    {
        // ARCHITECTURAL PRINCIPLE: Training uses explicit configuration - no silent defaults
        // Return basic currency/locale configuration - business logic is in StoreConfig
        return storeId switch
        {
            "training-store" => new StoreConfiguration
            {
                StoreId = storeId,
                Currency = "SGD", // Singapore Dollar for default Kopitiam training
                Locale = "en-SG" // Singapore English locale
            },
            "training-store-us-coffee" => new StoreConfiguration
            {
                StoreId = storeId,
                Currency = "USD", // US Dollar for American coffee shop
                Locale = "en-US"
            },
            "training-store-fr-bakery" => new StoreConfiguration
            {
                StoreId = storeId,
                Currency = "EUR", // Euro for French bakery
                Locale = "fr-FR"
            },
            "training-store-jp-convenience" => new StoreConfiguration
            {
                StoreId = storeId,
                Currency = "JPY", // Japanese Yen for convenience store
                Locale = "ja-JP"
            },
            "training-store-in-chai" => new StoreConfiguration
            {
                StoreId = storeId,
                Currency = "INR", // Indian Rupee for chai stall
                Locale = "hi-IN"
            },
            _ => throw new InvalidOperationException(
                $"DESIGN DEFICIENCY: Unknown store configuration '{storeId}'. " +
                "Training store configuration service supports:\n" +
                "- 'training-store' (Singapore Kopitiam, SGD, en-SG)\n" +
                "- 'training-store-us-coffee' (American Coffee Shop, USD, en-US)\n" +
                "- 'training-store-fr-bakery' (French Bakery, EUR, fr-FR)\n" +
                "- 'training-store-jp-convenience' (Japanese Convenience Store, JPY, ja-JP)\n" +
                "- 'training-store-in-chai' (Indian Chai Stall, INR, hi-IN)\n" +
                "Add configuration for store '{storeId}' or use supported store ID.")
        };
    }
}
