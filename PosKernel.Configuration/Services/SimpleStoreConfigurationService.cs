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

using PosKernel.Configuration.Services;

namespace PosKernel.Configuration.Services
{
    /// <summary>
    /// Simple implementation of IStoreConfigurationService for demo and testing purposes.
    /// In a real system, this would load store configuration from database or configuration files.
    /// ARCHITECTURAL PRINCIPLE: Demo service - minimal implementation for development/testing
    /// </summary>
    public class SimpleStoreConfigurationService : IStoreConfigurationService
    {
        /// <summary>
        /// Gets the store configuration for the specified store ID.
        /// Returns a default configuration with USD currency and en-US locale.
        /// </summary>
        /// <param name="storeId">The store identifier</param>
        /// <returns>A StoreConfiguration with default values</returns>
        public StoreConfiguration GetStoreConfiguration(string storeId)
        {
            // For now, return a generic store configuration
            // In a real system, this would be looked up from a database or configuration
            return new StoreConfiguration
            {
                StoreId = storeId,
                Currency = "USD", // Default - will be overridden by StoreConfig
                Locale = "en-US"  // Default - will be overridden by StoreConfig
            };
        }
    }
}
