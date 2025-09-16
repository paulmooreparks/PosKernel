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

namespace PosKernel.Abstractions.Services
{
    /// <summary>
    /// Service for managing product modifications across all store types.
    /// Supports cultural contexts (kopitiam, coffee shops, grocery stores, etc.)
    /// </summary>
    public interface IModificationService
    {
        /// <summary>
        /// Gets available modification groups for a specific product or category.
        /// </summary>
        Task<IReadOnlyList<IModificationGroup>> GetAvailableModificationGroupsAsync(
            string productId,
            string? categoryId = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all modifications within a specific group.
        /// </summary>
        Task<IReadOnlyList<IModification>> GetModificationsInGroupAsync(
            string groupId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates a set of modification selections for a product.
        /// </summary>
        Task<ModificationValidationResult> ValidateModificationsAsync(
            string productId,
            IReadOnlyList<ModificationSelection> selections,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Calculates the total price adjustment for a set of modifications.
        /// </summary>
        Task<decimal> CalculateModificationTotalAsync(
            IReadOnlyList<ModificationSelection> selections,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets localized modification names for display.
        /// </summary>
        Task<string> GetLocalizedModificationNameAsync(
            string modificationId,
            string localeCode,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Service for managing multi-language localization.
    /// </summary>
    public interface ILocalizationService
    {
        /// <summary>
        /// Gets localized text for a given key and locale.
        /// </summary>
        Task<string> GetLocalizedTextAsync(
            string localizationKey,
            string localeCode,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all available localizations for a given key.
        /// </summary>
        Task<IReadOnlyDictionary<string, string>> GetAllLocalizationsAsync(
            string localizationKey,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets supported locale codes for the current store.
        /// </summary>
        Task<IReadOnlyList<string>> GetSupportedLocalesAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the default locale code for fallback.
        /// </summary>
        string GetDefaultLocale();
    }

    /// <summary>
    /// Represents a product modification (e.g., "no sugar", "extra shot").
    /// </summary>
    public interface IModification
    {
        /// <summary>
        /// Gets the unique modification identifier.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Gets the default modification name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the modification category (e.g., "sweetness", "strength").
        /// </summary>
        string Category { get; }

        /// <summary>
        /// Gets the price adjustment for this modification.
        /// </summary>
        decimal PriceAdjustment { get; }

        /// <summary>
        /// Gets the tax treatment for this modification.
        /// </summary>
        TaxTreatment TaxTreatment { get; }

        /// <summary>
        /// Gets the localization key for multi-language support.
        /// </summary>
        string? LocalizationKey { get; }

        /// <summary>
        /// Gets the sort order for display.
        /// </summary>
        int SortOrder { get; }

        /// <summary>
        /// Gets whether this modification is currently active.
        /// </summary>
        bool IsActive { get; }
    }

    /// <summary>
    /// Represents a group of related modifications with selection rules.
    /// </summary>
    public interface IModificationGroup
    {
        /// <summary>
        /// Gets the unique group identifier.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Gets the group name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the selection type (single, multiple, optional).
        /// </summary>
        ModificationSelectionType SelectionType { get; }

        /// <summary>
        /// Gets the minimum number of selections required.
        /// </summary>
        int MinSelections { get; }

        /// <summary>
        /// Gets the maximum number of selections allowed.
        /// </summary>
        int MaxSelections { get; }

        /// <summary>
        /// Gets whether at least one selection is required.
        /// </summary>
        bool IsRequired { get; }

        /// <summary>
        /// Gets the localization key for the group name.
        /// </summary>
        string? LocalizationKey { get; }
    }

    /// <summary>
    /// Represents a customer's selection of a modification with quantity.
    /// </summary>
    public class ModificationSelection
    {
        /// <summary>
        /// Gets or sets the modification ID.
        /// </summary>
        public string ModificationId { get; set; } = "";

        /// <summary>
        /// Gets or sets the quantity selected.
        /// </summary>
        public int Quantity { get; set; } = 1;

        /// <summary>
        /// Gets or sets additional selection context.
        /// </summary>
        public Dictionary<string, object> Context { get; set; } = new();
    }

    /// <summary>
    /// Result of modification validation.
    /// </summary>
    public class ModificationValidationResult
    {
        /// <summary>
        /// Gets or sets whether the selections are valid.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Gets or sets validation error messages.
        /// </summary>
        public List<string> ErrorMessages { get; set; } = new();

        /// <summary>
        /// Gets or sets the total price adjustment.
        /// </summary>
        public decimal TotalPriceAdjustment { get; set; }

        /// <summary>
        /// Gets or sets preparation notes for kitchen/barista.
        /// </summary>
        public List<string> PreparationNotes { get; set; } = new();
    }

    /// <summary>
    /// Defines how modifications can be selected.
    /// </summary>
    public enum ModificationSelectionType
    {
        /// <summary>
        /// Only one modification can be selected.
        /// </summary>
        Single,

        /// <summary>
        /// Multiple modifications can be selected.
        /// </summary>
        Multiple,

        /// <summary>
        /// Modifications are optional (including none).
        /// </summary>
        Optional
    }

    /// <summary>
    /// Defines tax treatment for modifications.
    /// </summary>
    public enum TaxTreatment
    {
        /// <summary>
        /// Use the same tax treatment as the base product.
        /// </summary>
        Inherit,

        /// <summary>
        /// This modification is tax-exempt.
        /// </summary>
        Exempt,

        /// <summary>
        /// Apply standard tax rate.
        /// </summary>
        Standard,

        /// <summary>
        /// Apply reduced tax rate.
        /// </summary>
        Reduced
    }
}
