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
    /// Implementation of modification service for restaurant extension.
    /// Supports kopitiam modifications (free) and coffee shop modifications (charged).
    /// </summary>
    public class RestaurantModificationService : IModificationService
    {
        private readonly string _databasePath;
        private readonly ILogger<RestaurantModificationService> _logger;

        /// <summary>
        /// Initializes a new instance of the RestaurantModificationService.
        /// </summary>
        public RestaurantModificationService(
            ILogger<RestaurantModificationService> logger, 
            string? databasePath = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _databasePath = databasePath ?? Path.Combine("data", "catalog", "kopitiam_catalog.db");
        }

        /// <summary>
        /// Gets available modification groups for a specific product or category.
        /// </summary>
        public async Task<IReadOnlyList<IModificationGroup>> GetAvailableModificationGroupsAsync(
            string productId, 
            string? categoryId = null, 
            CancellationToken cancellationToken = default)
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync(cancellationToken);

            using var command = connection.CreateCommand();
            
            if (!string.IsNullOrEmpty(categoryId))
            {
                // Get by category
                command.CommandText = @"
                    SELECT mg.id, mg.name, mg.selection_type, mg.min_selections, mg.max_selections, mg.is_required, mg.localization_key
                    FROM modification_groups mg
                    INNER JOIN product_modification_groups pmg ON mg.id = pmg.modification_group_id
                    WHERE pmg.category_id = @categoryId AND pmg.is_active = 1
                    ORDER BY mg.id";
                command.Parameters.AddWithValue("@categoryId", categoryId);
            }
            else
            {
                // Get by product - first try direct product match, then fall back to category
                command.CommandText = @"
                    SELECT DISTINCT mg.id, mg.name, mg.selection_type, mg.min_selections, mg.max_selections, mg.is_required, mg.localization_key
                    FROM modification_groups mg
                    INNER JOIN product_modification_groups pmg ON mg.id = pmg.modification_group_id
                    LEFT JOIN products p ON p.sku = pmg.product_id OR p.category_id = pmg.category_id
                    WHERE (pmg.product_id = @productId OR (p.sku = @productId AND pmg.category_id = p.category_id))
                      AND pmg.is_active = 1
                    ORDER BY mg.id";
                command.Parameters.AddWithValue("@productId", productId);
            }

            var groups = new List<IModificationGroup>();
            using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                var group = new ModificationGroup
                {
                    Id = reader.GetString(0),
                    Name = reader.GetString(1),
                    SelectionType = Enum.Parse<ModificationSelectionType>(reader.GetString(2), ignoreCase: true),
                    MinSelections = reader.GetInt32(3),
                    MaxSelections = reader.GetInt32(4),
                    IsRequired = reader.GetBoolean(5),
                    LocalizationKey = reader.IsDBNull(6) ? null : reader.GetString(6)
                };
                groups.Add(group);
            }

            return groups;
        }

        /// <summary>
        /// Gets all modifications within a specific group.
        /// </summary>
        public async Task<IReadOnlyList<IModification>> GetModificationsInGroupAsync(
            string groupId, 
            CancellationToken cancellationToken = default)
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync(cancellationToken);

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT m.id, m.name, m.category, m.price_adjustment, m.tax_treatment, m.localization_key, m.sort_order, m.is_active
                FROM modifications m
                INNER JOIN modification_group_items mgi ON m.id = mgi.modification_id
                WHERE mgi.group_id = @groupId AND m.is_active = 1
                ORDER BY m.sort_order, m.name";
            command.Parameters.AddWithValue("@groupId", groupId);

            var modifications = new List<IModification>();
            using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                var modification = new Modification
                {
                    Id = reader.GetString(0),
                    Name = reader.GetString(1),
                    Category = reader.GetString(2),
                    PriceAdjustment = reader.GetDecimal(3),
                    TaxTreatment = Enum.Parse<TaxTreatment>(reader.GetString(4), ignoreCase: true),
                    LocalizationKey = reader.IsDBNull(5) ? null : reader.GetString(5),
                    SortOrder = reader.GetInt32(6),
                    IsActive = reader.GetBoolean(7)
                };
                modifications.Add(modification);
            }

            return modifications;
        }

        /// <summary>
        /// Validates a set of modification selections for a product.
        /// </summary>
        public async Task<ModificationValidationResult> ValidateModificationsAsync(
            string productId, 
            IReadOnlyList<ModificationSelection> selections, 
            CancellationToken cancellationToken = default)
        {
            var result = new ModificationValidationResult { IsValid = true };

            try
            {
                // Get available modification groups for the product
                var availableGroups = await GetAvailableModificationGroupsAsync(productId, cancellationToken: cancellationToken);
                var groupDict = availableGroups.ToDictionary(g => g.Id);

                // Group selections by modification group
                var selectionsByGroup = new Dictionary<string, List<ModificationSelection>>();
                
                foreach (var selection in selections)
                {
                    // Find which group this modification belongs to
                    var modification = await GetModificationAsync(selection.ModificationId, cancellationToken);
                    if (modification == null)
                    {
                        result.IsValid = false;
                        result.ErrorMessages.Add($"Unknown modification: {selection.ModificationId}");
                        continue;
                    }

                    var groupId = await GetModificationGroupIdAsync(selection.ModificationId, cancellationToken);
                    if (groupId == null)
                    {
                        result.IsValid = false;
                        result.ErrorMessages.Add($"Modification not associated with any group: {selection.ModificationId}");
                        continue;
                    }

                    if (!selectionsByGroup.ContainsKey(groupId))
                    {
                        selectionsByGroup[groupId] = new List<ModificationSelection>();
                    }
                    
                    selectionsByGroup[groupId].Add(selection);

                    // Add to total price adjustment
                    result.TotalPriceAdjustment += modification.PriceAdjustment * selection.Quantity;
                    
                    // Add preparation note
                    result.PreparationNotes.Add($"{modification.Name} x{selection.Quantity}");
                }

                // Validate each group's selection rules
                foreach (var group in availableGroups)
                {
                    var groupSelections = selectionsByGroup.GetValueOrDefault(group.Id, new List<ModificationSelection>());
                    var selectionCount = groupSelections.Sum(s => s.Quantity);

                    // Check minimum selections
                    if (selectionCount < group.MinSelections)
                    {
                        result.IsValid = false;
                        result.ErrorMessages.Add($"Group '{group.Name}' requires at least {group.MinSelections} selections, but {selectionCount} provided");
                    }

                    // Check maximum selections
                    if (selectionCount > group.MaxSelections)
                    {
                        result.IsValid = false;
                        result.ErrorMessages.Add($"Group '{group.Name}' allows at most {group.MaxSelections} selections, but {selectionCount} provided");
                    }

                    // Check required groups
                    if (group.IsRequired && selectionCount == 0)
                    {
                        result.IsValid = false;
                        result.ErrorMessages.Add($"Group '{group.Name}' is required but no selections were made");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating modifications for product {ProductId}", productId);
                result.IsValid = false;
                result.ErrorMessages.Add($"Validation error: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Calculates the total price adjustment for a set of modifications.
        /// </summary>
        public async Task<decimal> CalculateModificationTotalAsync(
            IReadOnlyList<ModificationSelection> selections, 
            CancellationToken cancellationToken = default)
        {
            decimal total = 0;

            foreach (var selection in selections)
            {
                var modification = await GetModificationAsync(selection.ModificationId, cancellationToken);
                if (modification != null)
                {
                    total += modification.PriceAdjustment * selection.Quantity;
                }
            }

            return total;
        }

        /// <summary>
        /// Gets localized modification name for display.
        /// </summary>
        public async Task<string> GetLocalizedModificationNameAsync(
            string modificationId, 
            string localeCode, 
            CancellationToken cancellationToken = default)
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync(cancellationToken);

            // First get the modification and its localization key
            using var modCommand = connection.CreateCommand();
            modCommand.CommandText = "SELECT name, localization_key FROM modifications WHERE id = @modId";
            modCommand.Parameters.AddWithValue("@modId", modificationId);

            using var modReader = await modCommand.ExecuteReaderAsync(cancellationToken);
            if (!await modReader.ReadAsync(cancellationToken))
            {
                return modificationId; // Fallback to ID if not found
            }

            var defaultName = modReader.GetString(0);
            var localizationKey = modReader.IsDBNull(1) ? null : modReader.GetString(1);

            if (string.IsNullOrEmpty(localizationKey))
            {
                return defaultName; // No localization key, return default
            }

            await modReader.CloseAsync();

            // Get localized text
            using var locCommand = connection.CreateCommand();
            locCommand.CommandText = @"
                SELECT text_value 
                FROM localizations 
                WHERE localization_key = @key AND locale_code = @locale";
            locCommand.Parameters.AddWithValue("@key", localizationKey);
            locCommand.Parameters.AddWithValue("@locale", localeCode);

            var localizedText = await locCommand.ExecuteScalarAsync(cancellationToken) as string;
            return localizedText ?? defaultName; // Fallback to default if no localization found
        }

        private async Task<IModification?> GetModificationAsync(string modificationId, CancellationToken cancellationToken)
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync(cancellationToken);

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT id, name, category, price_adjustment, tax_treatment, localization_key, sort_order, is_active
                FROM modifications 
                WHERE id = @modId";
            command.Parameters.AddWithValue("@modId", modificationId);

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return new Modification
                {
                    Id = reader.GetString(0),
                    Name = reader.GetString(1),
                    Category = reader.GetString(2),
                    PriceAdjustment = reader.GetDecimal(3),
                    TaxTreatment = Enum.Parse<TaxTreatment>(reader.GetString(4), ignoreCase: true),
                    LocalizationKey = reader.IsDBNull(5) ? null : reader.GetString(5),
                    SortOrder = reader.GetInt32(6),
                    IsActive = reader.GetBoolean(7)
                };
            }

            return null;
        }

        private async Task<string?> GetModificationGroupIdAsync(string modificationId, CancellationToken cancellationToken)
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync(cancellationToken);

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT group_id FROM modification_group_items WHERE modification_id = @modId LIMIT 1";
            command.Parameters.AddWithValue("@modId", modificationId);

            return await command.ExecuteScalarAsync(cancellationToken) as string;
        }
    }

    /// <summary>
    /// Implementation of a modification.
    /// </summary>
    public class Modification : IModification
    {
        /// <summary>
        /// Gets or sets the modification ID.
        /// </summary>
        public string Id { get; set; } = "";

        /// <summary>
        /// Gets or sets the modification name.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Gets or sets the modification category.
        /// </summary>
        public string Category { get; set; } = "";

        /// <summary>
        /// Gets or sets the price adjustment.
        /// </summary>
        public decimal PriceAdjustment { get; set; }

        /// <summary>
        /// Gets or sets the tax treatment.
        /// </summary>
        public TaxTreatment TaxTreatment { get; set; }

        /// <summary>
        /// Gets or sets the localization key.
        /// </summary>
        public string? LocalizationKey { get; set; }

        /// <summary>
        /// Gets or sets the sort order.
        /// </summary>
        public int SortOrder { get; set; }

        /// <summary>
        /// Gets or sets whether the modification is active.
        /// </summary>
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// Implementation of a modification group.
    /// </summary>
    public class ModificationGroup : IModificationGroup
    {
        /// <summary>
        /// Gets or sets the group ID.
        /// </summary>
        public string Id { get; set; } = "";

        /// <summary>
        /// Gets or sets the group name.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Gets or sets the selection type.
        /// </summary>
        public ModificationSelectionType SelectionType { get; set; }

        /// <summary>
        /// Gets or sets the minimum selections.
        /// </summary>
        public int MinSelections { get; set; }

        /// <summary>
        /// Gets or sets the maximum selections.
        /// </summary>
        public int MaxSelections { get; set; }

        /// <summary>
        /// Gets or sets whether the group is required.
        /// </summary>
        public bool IsRequired { get; set; }

        /// <summary>
        /// Gets or sets the localization key.
        /// </summary>
        public string? LocalizationKey { get; set; }
    }
}
