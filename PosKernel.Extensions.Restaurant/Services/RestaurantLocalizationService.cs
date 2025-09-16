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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using PosKernel.Abstractions.Services;

namespace PosKernel.Extensions.Restaurant.Services
{
    /// <summary>
    /// Implementation of localization service for restaurant extension.
    /// Supports Singapore kopitiam 4-language implementation (English, Chinese, Malay, Tamil).
    /// </summary>
    public class RestaurantLocalizationService : ILocalizationService
    {
        private readonly string _databasePath;
        private readonly ILogger<RestaurantLocalizationService> _logger;
        private readonly string _defaultLocale;

        /// <summary>
        /// Initializes a new instance of the RestaurantLocalizationService.
        /// </summary>
        public RestaurantLocalizationService(
            ILogger<RestaurantLocalizationService> logger, 
            string? databasePath = null,
            string defaultLocale = "en")
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _databasePath = databasePath ?? Path.Combine("data", "catalog", "kopitiam_catalog.db");
            _defaultLocale = defaultLocale;
        }

        /// <summary>
        /// Gets localized text for a given key and locale.
        /// </summary>
        public async Task<string> GetLocalizedTextAsync(
            string localizationKey, 
            string localeCode, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var connection = new SqliteConnection($"Data Source={_databasePath}");
                await connection.OpenAsync(cancellationToken);

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT text_value 
                    FROM localizations 
                    WHERE localization_key = @key AND locale_code = @locale";
                command.Parameters.AddWithValue("@key", localizationKey);
                command.Parameters.AddWithValue("@locale", localeCode);

                var result = await command.ExecuteScalarAsync(cancellationToken) as string;
                
                if (result != null)
                {
                    return result;
                }

                // Try default locale fallback
                if (localeCode != _defaultLocale)
                {
                    _logger.LogDebug("No localization found for {Key} in {Locale}, trying default locale {DefaultLocale}", 
                        localizationKey, localeCode, _defaultLocale);
                    
                    command.Parameters["@locale"].Value = _defaultLocale;
                    result = await command.ExecuteScalarAsync(cancellationToken) as string;
                    
                    if (result != null)
                    {
                        return result;
                    }
                }

                // Last resort: try to get any localization for this key
                command.CommandText = "SELECT text_value FROM localizations WHERE localization_key = @key LIMIT 1";
                result = await command.ExecuteScalarAsync(cancellationToken) as string;
                
                if (result != null)
                {
                    _logger.LogDebug("Using fallback localization for {Key}", localizationKey);
                    return result;
                }

                // No localization found
                _logger.LogWarning("No localization found for key {Key} in any locale", localizationKey);
                return localizationKey; // Return the key itself as fallback
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting localized text for {Key} in {Locale}", localizationKey, localeCode);
                return localizationKey; // Return key as fallback
            }
        }

        /// <summary>
        /// Gets all available localizations for a given key.
        /// </summary>
        public async Task<IReadOnlyDictionary<string, string>> GetAllLocalizationsAsync(
            string localizationKey, 
            CancellationToken cancellationToken = default)
        {
            var localizations = new Dictionary<string, string>();

            try
            {
                using var connection = new SqliteConnection($"Data Source={_databasePath}");
                await connection.OpenAsync(cancellationToken);

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT locale_code, text_value 
                    FROM localizations 
                    WHERE localization_key = @key
                    ORDER BY locale_code";
                command.Parameters.AddWithValue("@key", localizationKey);

                using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var localeCode = reader.GetString(0);
                    var textValue = reader.GetString(1);
                    localizations[localeCode] = textValue;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all localizations for key {Key}", localizationKey);
            }

            return localizations;
        }

        /// <summary>
        /// Gets supported locale codes for the current store.
        /// </summary>
        public async Task<IReadOnlyList<string>> GetSupportedLocalesAsync(CancellationToken cancellationToken = default)
        {
            var locales = new List<string>();

            try
            {
                using var connection = new SqliteConnection($"Data Source={_databasePath}");
                await connection.OpenAsync(cancellationToken);

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT DISTINCT locale_code 
                    FROM localizations 
                    ORDER BY locale_code";

                using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    locales.Add(reader.GetString(0));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting supported locales");
                // Return default locale as fallback
                locales.Add(_defaultLocale);
            }

            // Always ensure default locale is included
            if (!locales.Contains(_defaultLocale))
            {
                locales.Insert(0, _defaultLocale);
            }

            return locales;
        }

        /// <summary>
        /// Gets the default locale code for fallback.
        /// </summary>
        public string GetDefaultLocale()
        {
            return _defaultLocale;
        }

        /// <summary>
        /// Gets localized product name with fallback to original name.
        /// </summary>
        public async Task<string> GetLocalizedProductNameAsync(
            string productId, 
            string localeCode, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var connection = new SqliteConnection($"Data Source={_databasePath}");
                await connection.OpenAsync(cancellationToken);

                // Get product and its localization key
                using var productCommand = connection.CreateCommand();
                productCommand.CommandText = @"
                    SELECT name, name_localization_key 
                    FROM products 
                    WHERE sku = @productId";
                productCommand.Parameters.AddWithValue("@productId", productId);

                using var reader = await productCommand.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                {
                    return productId; // Product not found, return ID
                }

                var defaultName = reader.GetString(0);
                var localizationKey = reader.IsDBNull(1) ? null : reader.GetString(1);

                if (string.IsNullOrEmpty(localizationKey))
                {
                    return defaultName; // No localization key
                }

                await reader.CloseAsync();

                // Get localized text
                var localizedName = await GetLocalizedTextAsync(localizationKey, localeCode, cancellationToken);
                return localizedName != localizationKey ? localizedName : defaultName;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting localized product name for {ProductId} in {Locale}", productId, localeCode);
                return productId;
            }
        }

        /// <summary>
        /// Gets localized product description with fallback to original description.
        /// </summary>
        public async Task<string> GetLocalizedProductDescriptionAsync(
            string productId, 
            string localeCode, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var connection = new SqliteConnection($"Data Source={_databasePath}");
                await connection.OpenAsync(cancellationToken);

                // Get product and its localization key
                using var productCommand = connection.CreateCommand();
                productCommand.CommandText = @"
                    SELECT description, description_localization_key 
                    FROM products 
                    WHERE sku = @productId";
                productCommand.Parameters.AddWithValue("@productId", productId);

                using var reader = await productCommand.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                {
                    return ""; // Product not found
                }

                var defaultDescription = reader.GetString(0);
                var localizationKey = reader.IsDBNull(1) ? null : reader.GetString(1);

                if (string.IsNullOrEmpty(localizationKey))
                {
                    return defaultDescription; // No localization key
                }

                await reader.CloseAsync();

                // Get localized text
                var localizedDescription = await GetLocalizedTextAsync(localizationKey, localeCode, cancellationToken);
                return localizedDescription != localizationKey ? localizedDescription : defaultDescription;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting localized product description for {ProductId} in {Locale}", productId, localeCode);
                return "";
            }
        }
    }
}
