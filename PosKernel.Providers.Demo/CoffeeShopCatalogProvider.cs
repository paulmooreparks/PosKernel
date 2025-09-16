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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PosKernel.Abstractions.CAL.Products;
using ParksComputing.Xfer.Lang;

namespace PosKernel.Providers.Demo
{
    /// <summary>
    /// Demo CAL provider that reads product catalog from XferLang files.
    /// Shows how to implement a proper CAL provider for coffee shop business domain.
    /// </summary>
    public class CoffeeShopCatalogProvider : IProductCatalogProvider
    {
        private readonly Dictionary<string, XferProduct> _products = new();
        private readonly Dictionary<string, XferCategory> _categories = new();
        private readonly XferLangConfiguration _config;
        private readonly string _catalogFilePath;
        private DateTime _lastFileModified = DateTime.MinValue;

        public string ProviderId => "demo_coffee_shop";
        public string BusinessDomain => "restaurant";
        public IReadOnlyList<string> SupportedIdentifierFormats => new[] { "sku", "barcode", "internal_code" };

        public CoffeeShopCatalogProvider(XferLangConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _catalogFilePath = Path.GetFullPath(_config.DataSource?.Replace("xfer://", "") ?? "data/catalog/coffee-shop-products.xfer");
        }

        public async Task<ProductLookupResult?> LookupProductAsync(string identifier, ProductLookupContext context)
        {
            await EnsureCatalogLoadedAsync();

            // Try to find product by various identifier formats
            var product = _products.Values.FirstOrDefault(p => 
                p.Identifiers.SKU == identifier ||
                p.Identifiers.Barcode == identifier ||
                p.Identifiers.InternalCode == identifier);

            if (product == null)
                return null;

            var kernelProduct = new KernelProduct
            {
                Identifier = product.Identifiers.SKU,
                DisplayName = product.Name,
                BasePrice = product.BasePrice,
                IsAvailableForSale = product.IsActive
            };

            // Build business-specific attributes
            var businessAttributes = new Dictionary<string, object>
            {
                ["description"] = product.Description,
                ["category"] = product.Category,
                ["allergens"] = product.Allergens,
                ["specifications"] = product.Specifications,
                ["popularity_rank"] = product.PopularityRank,
                ["preparation_time"] = product.PreparationTime,
                ["customizations"] = product.Customizations,
                ["nutritional"] = product.Nutritional,
                ["upsell_suggestions"] = product.UpsellSuggestions
            };

            // Get category information
            if (_categories.TryGetValue(product.Category, out var category))
            {
                businessAttributes["category_info"] = new Dictionary<string, object>
                {
                    ["requires_preparation"] = category.RequiresPreparation,
                    ["tax_category"] = category.TaxCategory,
                    ["average_prep_time"] = category.AveragePreparationTime,
                    ["serving_recommendations"] = category.ServingRecommendations
                };
            }

            return new ProductLookupResult
            {
                KernelProduct = kernelProduct,
                BusinessAttributes = businessAttributes,
                Metadata = new ProductLookupMetadata
                {
                    Source = ProviderId,
                    LastUpdated = _lastFileModified,
                    CacheTimeToLive = TimeSpan.FromMinutes(15)
                }
            };
        }

        public async Task<IReadOnlyList<ProductLookupResult>> SearchProductsAsync(ProductSearchCriteria criteria, ProductLookupContext context)
        {
            await EnsureCatalogLoadedAsync();

            var results = new List<ProductLookupResult>();

            if (string.IsNullOrWhiteSpace(criteria.SearchTerm))
            {
                // Return all products if no search term
                foreach (var product in _products.Values.Take(criteria.MaxResults))
                {
                    var lookupResult = await LookupProductAsync(product.Identifiers.SKU, context);
                    if (lookupResult != null)
                        results.Add(lookupResult);
                }
            }
            else
            {
                var searchTerm = criteria.SearchTerm.ToLowerInvariant();
                var matchingProducts = _products.Values.Where(p =>
                    p.Name.ToLowerInvariant().Contains(searchTerm) ||
                    p.Description.ToLowerInvariant().Contains(searchTerm) ||
                    p.Identifiers.SKU.ToLowerInvariant().Contains(searchTerm) ||
                    p.Category.ToLowerInvariant().Contains(searchTerm)
                ).Take(criteria.MaxResults);

                foreach (var product in matchingProducts)
                {
                    var lookupResult = await LookupProductAsync(product.Identifiers.SKU, context);
                    if (lookupResult != null)
                        results.Add(lookupResult);
                }
            }

            return results;
        }

        public bool IsValidIdentifierFormat(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                return false;

            // SKU format: uppercase letters and underscores
            if (identifier.All(c => char.IsLetterOrDigit(c) || c == '_') && identifier.Any(char.IsLetter))
                return true;

            // Barcode format: 12-13 digits
            if (identifier.All(char.IsDigit) && identifier.Length >= 12 && identifier.Length <= 13)
                return true;

            return false;
        }

        public async Task<string> GetConfigurationSchemaAsync()
        {
            // Return XferLang configuration schema for this provider
            return await Task.FromResult(@"
</ Coffee Shop CAL Provider Configuration Schema />
{
    Provider {
        Configuration {
            DataSource ""xfer://path/to/catalog.xfer""  </ Path to XferLang catalog file />
            CacheTimeToLive ""PT15M""                   </ ISO 8601 duration />
            SupportedFormats [""sku"", ""barcode""]     </ Identifier formats />
            WatchForChanges true                        </ Auto-reload on file changes />
            BusinessDomain ""restaurant""               </ Business domain />
        }
    }
}");
        }

        private async Task EnsureCatalogLoadedAsync()
        {
            if (!File.Exists(_catalogFilePath))
            {
                throw new FileNotFoundException($"Catalog file not found: {_catalogFilePath}");
            }

            var fileModified = File.GetLastWriteTime(_catalogFilePath);
            if (fileModified <= _lastFileModified && _products.Any())
            {
                return; // Already loaded and up to date
            }

            try
            {
                var xferContent = await File.ReadAllTextAsync(_catalogFilePath);
                
                // Use proper XferLang API based on https://xferlang.org/api.html
                var parser = new XferParser();
                var document = parser.Parse(xferContent);

                // Clear existing data
                _products.Clear();
                _categories.Clear();

                // Navigate to Catalog root using proper API
                if (document.TryGetValue("Catalog", out var catalogValue) && catalogValue.IsObject)
                {
                    var catalog = catalogValue.AsObject();
                    
                    // Load categories
                    if (catalog.TryGetValue("Categories", out var categoriesValue) && categoriesValue.IsObject)
                    {
                        var categories = categoriesValue.AsObject();
                        foreach (var (key, value) in categories)
                        {
                            var category = ParseCategory(key, value);
                            _categories[key] = category;
                        }
                    }

                    // Load products
                    if (catalog.TryGetValue("Products", out var productsValue) && productsValue.IsObject)
                    {
                        var products = productsValue.AsObject();
                        foreach (var (key, value) in products)
                        {
                            var product = ParseProduct(key, value);
                            _products[key] = product;
                        }
                    }
                }

                _lastFileModified = fileModified;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to load catalog from {_catalogFilePath}: {ex.Message}", ex);
            }
        }

        private XferCategory ParseCategory(string key, IXferValue value)
        {
            if (!value.IsObject) return new XferCategory { Id = key };
            
            var obj = value.AsObject();
            return new XferCategory
            {
                Id = key,
                Name = obj.TryGetValue("Name", out var nameVal) && nameVal.IsString ? nameVal.AsString() : key,
                Description = obj.TryGetValue("Description", out var descVal) && descVal.IsString ? descVal.AsString() : "",
                TaxCategory = obj.TryGetValue("TaxCategory", out var taxVal) && taxVal.IsString ? taxVal.AsString() : "STANDARD",
                RequiresPreparation = obj.TryGetValue("RequiresPreparation", out var prepVal) && prepVal.IsBoolean && prepVal.AsBoolean(),
                AveragePreparationTime = obj.TryGetValue("AveragePreparationTime", out var timeVal) && timeVal.IsString ? timeVal.AsString() : "PT0M",
                ServingRecommendations = ParseStringArray(obj, "ServingRecommendations")
            };
        }

        private XferProduct ParseProduct(string key, IXferValue value)
        {
            if (!value.IsObject) return new XferProduct();
            
            var obj = value.AsObject();
            
            // Parse identifiers
            var identifiers = new ProductIdentifiers { SKU = key };
            if (obj.TryGetValue("Identifiers", out var idVal) && idVal.IsObject)
            {
                var idObj = idVal.AsObject();
                identifiers.SKU = idObj.TryGetValue("SKU", out var skuVal) && skuVal.IsString ? skuVal.AsString() : key;
                identifiers.Barcode = idObj.TryGetValue("Barcode", out var barcodeVal) && barcodeVal.IsString ? barcodeVal.AsString() : "";
                identifiers.InternalCode = idObj.TryGetValue("InternalCode", out var intVal) && intVal.IsString ? intVal.AsString() : "";
            }
            
            return new XferProduct
            {
                Identifiers = identifiers,
                Name = obj.TryGetValue("Name", out var nameVal) && nameVal.IsString ? nameVal.AsString() : key,
                Description = obj.TryGetValue("Description", out var descVal) && descVal.IsString ? descVal.AsString() : "",
                Category = obj.TryGetValue("Category", out var catVal) && catVal.IsString ? catVal.AsString() : "",
                BasePrice = obj.TryGetValue("BasePrice", out var priceVal) && priceVal.IsNumber ? (decimal)priceVal.AsDouble() : 0m,
                IsActive = obj.TryGetValue("IsActive", out var activeVal) && activeVal.IsBoolean ? activeVal.AsBoolean() : true,
                Allergens = ParseStringArray(obj, "Allergens"),
                Specifications = ParseObjectToDictionary(obj, "Specifications"),
                PopularityRank = obj.TryGetValue("PopularityRank", out var rankVal) && rankVal.IsNumber ? (int)rankVal.AsDouble() : 999,
                PreparationTime = obj.TryGetValue("PreparationTime", out var prepTimeVal) && prepTimeVal.IsString ? prepTimeVal.AsString() : "PT0M",
                Customizations = ParseObjectToDictionary(obj, "Customizations"),
                Nutritional = ParseObjectToDictionary(obj, "Nutritional"),
                UpsellSuggestions = ParseStringArray(obj, "UpsellSuggestions")
            };
        }

        private List<string> ParseStringArray(IXferObject obj, string key)
        {
            if (obj.TryGetValue(key, out var arrVal) && arrVal.IsArray)
            {
                var array = arrVal.AsArray();
                return array.Where(v => v.IsString).Select(v => v.AsString()).ToList();
            }
            return new List<string>();
        }

        private Dictionary<string, object> ParseObjectToDictionary(IXferObject obj, string key)
        {
            if (obj.TryGetValue(key, out var objVal) && objVal.IsObject)
            {
                var dict = new Dictionary<string, object>();
                var xferObj = objVal.AsObject();
                foreach (var (k, v) in xferObj)
                {
                    dict[k] = v.IsString ? v.AsString() : 
                             v.IsNumber ? v.AsDouble() : 
                             v.IsBoolean ? v.AsBoolean() : 
                             v.ToString();
                }
                return dict;
            }
            return new Dictionary<string, object>();
        }
    }

    // Internal data structures for XferLang parsing remain the same
    internal class XferProduct
    {
        public ProductIdentifiers Identifiers { get; set; } = new();
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Category { get; set; } = "";
        public decimal BasePrice { get; set; }
        public bool IsActive { get; set; } = true;
        public List<string> Allergens { get; set; } = new();
        public Dictionary<string, object> Specifications { get; set; } = new();
        public int PopularityRank { get; set; } = 999;
        public string PreparationTime { get; set; } = "PT0M";
        public Dictionary<string, object> Customizations { get; set; } = new();
        public Dictionary<string, object> Nutritional { get; set; } = new();
        public List<string> UpsellSuggestions { get; set; } = new();
    }

    internal class ProductIdentifiers
    {
        public string SKU { get; set; } = "";
        public string Barcode { get; set; } = "";
        public string InternalCode { get; set; } = "";
    }

    internal class XferCategory
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string TaxCategory { get; set; } = "";
        public bool RequiresPreparation { get; set; }
        public string AveragePreparationTime { get; set; } = "";
        public List<string> ServingRecommendations { get; set; } = new();
    }

    internal class XferLangConfiguration
    {
        public string? DataSource { get; set; }
        public string CacheTimeToLive { get; set; } = "PT15M";
        public List<string> SupportedFormats { get; set; } = new();
    }

    // Kernel product implementation
    internal class KernelProduct : IKernelProduct
    {
        public string Identifier { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public decimal BasePrice { get; set; }
        public bool IsAvailableForSale { get; set; }
    }
}
