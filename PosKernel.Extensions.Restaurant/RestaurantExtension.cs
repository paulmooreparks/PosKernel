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
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace PosKernel.Extensions.Restaurant
{
    /// <summary>
    /// Restaurant domain extension for POS Kernel.
    /// Provides product catalog services with allergen tracking, preparation time validation,
    /// and menu scheduling for restaurant operations.
    /// </summary>
    public class RestaurantExtension
    {
        private readonly ILogger<RestaurantExtension> _logger;
        private readonly SqliteConnection _database;
        private readonly RestaurantConfig _config;

        public RestaurantExtension(ILogger<RestaurantExtension> logger, RestaurantConfig config)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            
            // Initialize SQLite database connection
            _database = new SqliteConnection($"Data Source={_config.DatabasePath}");
            _database.Open();
            
            _logger.LogInformation("Restaurant extension initialized with database: {DatabasePath}", _config.DatabasePath);
        }

        /// <summary>
        /// Validates a product for sale, including allergen checks and menu availability.
        /// </summary>
        public ProductValidationResult ValidateProduct(string productId, ValidationContext context)
        {
            try
            {
                _logger.LogDebug("Validating product: {ProductId} for store: {StoreId}", productId, context.StoreId);

                using var command = _database.CreateCommand();
                command.CommandText = @"
                    SELECT p.sku, p.name, p.description, p.category_id, p.base_price_cents, 
                           p.is_active, p.requires_preparation, p.preparation_time_minutes, p.popularity_rank,
                           c.name as category_name, c.tax_category, c.requires_preparation as category_prep
                    FROM products p
                    JOIN categories c ON p.category_id = c.id
                    WHERE p.sku = @productId 
                       OR EXISTS (SELECT 1 FROM product_identifiers pi 
                                 WHERE pi.product_sku = p.sku 
                                   AND pi.identifier_value = @productId)";
                command.Parameters.AddWithValue("@productId", productId);

                using var reader = command.ExecuteReader();
                if (!reader.Read())
                {
                    return new ProductValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = $"Product not found: {productId}"
                    };
                }

                var product = new ProductInfo
                {
                    Sku = reader.GetString(0), // "sku"
                    Name = reader.GetString(1), // "name" 
                    Description = reader.GetString(2), // "description"
                    CategoryId = reader.GetString(3), // "category_id"
                    CategoryName = reader.GetString(9), // "category_name"
                    BasePriceCents = reader.GetInt64(4), // "base_price_cents"
                    IsActive = reader.GetBoolean(5), // "is_active"
                    RequiresPreparation = reader.GetBoolean(6), // "requires_preparation"
                    PreparationTimeMinutes = reader.GetInt32(7), // "preparation_time_minutes"
                    TaxCategory = reader.GetString(10), // "tax_category"
                    PopularityRank = reader.GetInt32(8) // "popularity_rank"
                };

                reader.Close();

                // Validate business rules
                var validationResult = new ProductValidationResult { IsValid = true };

                // Check if product is active
                if (!product.IsActive)
                {
                    validationResult.IsValid = false;
                    validationResult.ErrorMessage = "Product is not currently available";
                    return validationResult;
                }

                // Load allergen information
                LoadAllergenInfo(product);

                // Load specifications
                LoadProductSpecs(product);

                // Load customizations
                LoadCustomizations(product);

                // Load upsell suggestions
                LoadUpsellSuggestions(product);

                // Check menu scheduling if configured
                if (_config.MenuSchedulingEnabled)
                {
                    var menuValidation = ValidateMenuTiming(product, DateTime.Now);
                    if (!menuValidation.IsValid)
                    {
                        validationResult.IsValid = false;
                        validationResult.ErrorMessage = menuValidation.ErrorMessage;
                        return validationResult;
                    }
                }

                validationResult.ProductInfo = product;
                validationResult.EffectivePriceCents = CalculateEffectivePrice(product, context);

                _logger.LogDebug("Product validation successful: {ProductId} -> {Name}", productId, product.Name);
                return validationResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating product: {ProductId}", productId);
                return new ProductValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"Internal error validating product: {ex.Message}"
                };
            }
        }

        private void LoadAllergenInfo(ProductInfo product)
        {
            using var command = _database.CreateCommand();
            command.CommandText = @"
                SELECT a.id, a.name, a.description, pa.contamination_risk
                FROM product_allergens pa
                JOIN allergens a ON pa.allergen_id = a.id
                WHERE pa.product_sku = @sku AND a.is_active = 1";
            command.Parameters.AddWithValue("@sku", product.Sku);

            using var reader = command.ExecuteReader();
            var allergens = new List<AllergenInfo>();
            
            while (reader.Read())
            {
                allergens.Add(new AllergenInfo
                {
                    Id = reader.GetString(0), // "id"
                    Name = reader.GetString(1), // "name"
                    Description = reader.GetString(2), // "description"
                    ContaminationRisk = reader.GetString(3) // "contamination_risk"
                });
            }
            
            product.Allergens = allergens;
        }

        private void LoadProductSpecs(ProductInfo product)
        {
            using var command = _database.CreateCommand();
            command.CommandText = @"
                SELECT spec_key, spec_value, spec_type
                FROM product_specifications
                WHERE product_sku = @sku";
            command.Parameters.AddWithValue("@sku", product.Sku);

            using var reader = command.ExecuteReader();
            var specifications = new Dictionary<string, object>();
            
            while (reader.Read())
            {
                var key = reader.GetString(0); // "spec_key"
                var value = reader.GetString(1); // "spec_value"
                var type = reader.GetString(2); // "spec_type"

                specifications[key] = type switch
                {
                    "number" => double.TryParse(value, out var num) ? num : value,
                    "boolean" => bool.TryParse(value, out var flag) ? flag : value,
                    _ => value
                };
            }
            
            product.Specifications = specifications;
        }

        private void LoadCustomizations(ProductInfo product)
        {
            using var command = _database.CreateCommand();
            command.CommandText = @"
                SELECT customization_type, option_value, price_modifier_cents, is_default, display_order, is_available
                FROM product_customizations
                WHERE product_sku = @sku AND is_available = 1
                ORDER BY customization_type, display_order";
            command.Parameters.AddWithValue("@sku", product.Sku);

            using var reader = command.ExecuteReader();
            var customizations = new Dictionary<string, List<CustomizationOption>>();
            
            while (reader.Read())
            {
                var type = reader.GetString(0); // "customization_type"
                var option = new CustomizationOption
                {
                    Value = reader.GetString(1), // "option_value"
                    PriceModifierCents = reader.GetInt64(2), // "price_modifier_cents" 
                    IsDefault = reader.GetBoolean(3), // "is_default"
                    DisplayOrder = reader.GetInt32(4) // "display_order"
                };

                if (!customizations.ContainsKey(type))
                {
                    customizations[type] = new List<CustomizationOption>();
                }
                customizations[type].Add(option);
            }
            
            product.Customizations = customizations;
        }

        private void LoadUpsellSuggestions(ProductInfo product)
        {
            using var command = _database.CreateCommand();
            command.CommandText = @"
                SELECT u.suggested_sku, u.suggestion_type, u.priority, p.name, p.base_price_cents
                FROM product_upsells u
                JOIN products p ON u.suggested_sku = p.sku
                WHERE u.product_sku = @sku AND p.is_active = 1
                ORDER BY u.priority";
            command.Parameters.AddWithValue("@sku", product.Sku);

            using var reader = command.ExecuteReader();
            var upsells = new List<UpsellSuggestion>();
            
            while (reader.Read())
            {
                upsells.Add(new UpsellSuggestion
                {
                    Sku = reader.GetString(0), // "suggested_sku"
                    SuggestionType = reader.GetString(1), // "suggestion_type"
                    Priority = reader.GetInt32(2), // "priority"
                    Name = reader.GetString(3), // "name"
                    PriceCents = reader.GetInt64(4) // "base_price_cents"
                });
            }
            
            product.UpsellSuggestions = upsells;
        }

        private MenuValidationResult ValidateMenuTiming(ProductInfo product, DateTime currentTime)
        {
            // Check if this category has time restrictions
            using var command = _database.CreateCommand();
            command.CommandText = @"
                SELECT ps.spec_value as availability_hours
                FROM product_specifications ps
                WHERE ps.product_sku = @sku AND ps.spec_key = 'availability_hours'
                UNION
                SELECT ps.spec_value as availability_hours
                FROM product_specifications ps
                JOIN products p ON ps.product_sku = p.sku
                WHERE p.category_id = @categoryId AND ps.spec_key = 'category_availability_hours'";
            command.Parameters.AddWithValue("@sku", product.Sku);
            command.Parameters.AddWithValue("@categoryId", product.CategoryId);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                var hoursJson = reader.GetString(0); // "availability_hours"
                try
                {
                    var hours = JsonSerializer.Deserialize<AvailabilityHours>(hoursJson);
                    if (hours != null)
                    {
                        var currentHour = currentTime.Hour + (currentTime.Minute / 60.0);
                        if (currentHour < hours.StartHour || currentHour > hours.EndHour)
                        {
                            return new MenuValidationResult
                            {
                                IsValid = false,
                                ErrorMessage = $"Product not available at this time. Available: {hours.StartHour:F1} - {hours.EndHour:F1}"
                            };
                        }
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Invalid availability hours JSON for product {Sku}", product.Sku);
                }
            }

            return new MenuValidationResult { IsValid = true };
        }

        private long CalculateEffectivePrice(ProductInfo product, ValidationContext context)
        {
            var effectivePrice = product.BasePriceCents;

            // Apply time-based pricing rules
            effectivePrice = ApplyPricingRules(product, effectivePrice, context);

            return effectivePrice;
        }

        private long ApplyPricingRules(ProductInfo product, long basePrice, ValidationContext context)
        {
            using var command = _database.CreateCommand();
            command.CommandText = @"
                SELECT pr.discount_type, pr.discount_value, pr.conditions
                FROM pricing_rules pr
                JOIN pricing_rule_products prp ON pr.id = prp.rule_id
                WHERE prp.product_sku = @sku 
                  AND pr.is_active = 1
                  AND (pr.start_date IS NULL OR date('now') >= pr.start_date)
                  AND (pr.end_date IS NULL OR date('now') <= pr.end_date)";
            command.Parameters.AddWithValue("@sku", product.Sku);

            using var reader = command.ExecuteReader();
            var appliedPrice = basePrice;
            
            while (reader.Read())
            {
                var discountType = reader.GetString(0); // "discount_type"
                var discountValue = reader.GetDouble(1); // "discount_value"
                var conditionsJson = reader.IsDBNull(2) ? null : reader.GetString(2); // "conditions"

                // Check if conditions are met
                if (conditionsJson != null && !EvaluatePricingConditions(conditionsJson, context))
                {
                    continue;
                }

                // Apply discount
                appliedPrice = discountType switch
                {
                    "percentage" => (long)(appliedPrice * (1.0 - discountValue / 100.0)),
                    "fixed_amount" => Math.Max(0, appliedPrice - (long)(discountValue * 100)), // discountValue in dollars
                    _ => appliedPrice
                };
            }

            return appliedPrice;
        }

        private bool EvaluatePricingConditions(string conditionsJson, ValidationContext context)
        {
            try
            {
                var conditions = JsonSerializer.Deserialize<PricingConditions>(conditionsJson);
                if (conditions == null) 
                {
                    return true;
                }

                var now = DateTime.Now;

                // Check time-based conditions
                if (conditions.Days?.Length > 0)
                {
                    var currentDay = now.DayOfWeek.ToString().ToLowerInvariant();
                    if (!conditions.Days.Contains(currentDay))
                    {
                        return false;
                    }
                }

                if (!string.IsNullOrEmpty(conditions.StartTime) && !string.IsNullOrEmpty(conditions.EndTime))
                {
                    if (TimeSpan.TryParse(conditions.StartTime, out var startTime) && 
                        TimeSpan.TryParse(conditions.EndTime, out var endTime))
                    {
                        var currentTime = now.TimeOfDay;
                        if (currentTime < startTime || currentTime > endTime)
                        {
                            return false;
                        }
                    }
                }

                return true;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Invalid pricing conditions JSON: {Json}", conditionsJson);
                return false;
            }
        }

        public void Dispose()
        {
            _database?.Dispose();
        }
    }

    // Data structures
    public class RestaurantConfig
    {
        public string DatabasePath { get; set; } = "data/catalog/restaurant_catalog.db";
        public bool AllergenValidation { get; set; } = true;
        public bool PreparationTimeTracking { get; set; } = true;
        public bool MenuSchedulingEnabled { get; set; } = true;
        public bool SeasonalAvailability { get; set; } = true;
        public int MaxPreparationTimeMinutes { get; set; } = 30;
    }

    public class ValidationContext
    {
        public string StoreId { get; set; } = "";
        public string TerminalId { get; set; } = "";
        public string OperatorId { get; set; } = "";
        public DateTime RequestTime { get; set; } = DateTime.UtcNow;
        public Dictionary<string, object> AdditionalContext { get; set; } = new();
    }

    public class ProductInfo
    {
        public string Sku { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string CategoryId { get; set; } = "";
        public string CategoryName { get; set; } = "";
        public long BasePriceCents { get; set; }
        public bool IsActive { get; set; }
        public bool RequiresPreparation { get; set; }
        public int PreparationTimeMinutes { get; set; }
        public string TaxCategory { get; set; } = "";
        public int PopularityRank { get; set; }
        public List<AllergenInfo> Allergens { get; set; } = new();
        public Dictionary<string, object> Specifications { get; set; } = new();
        public Dictionary<string, List<CustomizationOption>> Customizations { get; set; } = new();
        public List<UpsellSuggestion> UpsellSuggestions { get; set; } = new();
    }

    public class AllergenInfo
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string ContaminationRisk { get; set; } = "";
    }

    public class CustomizationOption
    {
        public string Value { get; set; } = "";
        public long PriceModifierCents { get; set; }
        public bool IsDefault { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class UpsellSuggestion
    {
        public string Sku { get; set; } = "";
        public string Name { get; set; } = "";
        public string SuggestionType { get; set; } = "";
        public int Priority { get; set; }
        public long PriceCents { get; set; }
    }

    public class ProductValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = "";
        public ProductInfo? ProductInfo { get; set; }
        public long EffectivePriceCents { get; set; }
    }

    public class MenuValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = "";
    }

    public class AvailabilityHours
    {
        public double StartHour { get; set; }
        public double EndHour { get; set; }
    }

    public class PricingConditions
    {
        public string[]? Days { get; set; }
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
        public int? MinimumQuantity { get; set; }
        public bool? SameProduct { get; set; }
    }
}
