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
/// Store configuration service for training that provides proper currency/culture configuration
/// ARCHITECTURAL PRINCIPLE: No hardcoded defaults - explicit configuration for training scenarios
/// TODO: In production, this would integrate with database or configuration management system
/// </summary>
public class TrainingStoreConfigurationService : IStoreConfigurationService
{
    public StoreConfiguration GetStoreConfiguration(string storeId)
    {
        // ARCHITECTURAL PRINCIPLE: Training uses explicit configuration - no silent defaults
        // For training purposes, use Singapore Kopitiam configuration to match existing AI personality
        // TODO: This should be configurable through proper configuration management
        return storeId switch
        {
            "training-store" => new StoreConfiguration
            {
                StoreId = storeId,
                Currency = "SGD", // Singapore Dollar for Kopitiam Uncle personality
                Locale = "en-SG"  // Singapore English locale
            },
            _ => throw new InvalidOperationException(
                $"DESIGN DEFICIENCY: Unknown store configuration '{storeId}'. " +
                "Training store configuration service only supports 'training-store' configuration. " +
                "Add configuration for store '{storeId}' or use supported store ID.")
        };
    }
}
