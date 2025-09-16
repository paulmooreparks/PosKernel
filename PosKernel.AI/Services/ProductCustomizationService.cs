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

using PosKernel.Abstractions.Services;

namespace PosKernel.AI.Services
{
    /// <summary>
    /// Represents a product modifier/customization (e.g., "kosong", "siew dai", "gao").
    /// </summary>
    public class ProductModifier
    {
        /// <summary>
        /// Gets the modifier identifier.
        /// </summary>
        public string Id { get; }
        
        /// <summary>
        /// Gets the modifier name.
        /// </summary>
        public string Name { get; }
        
        /// <summary>
        /// Gets the modifier description.
        /// </summary>
        public string Description { get; }
        
        /// <summary>
        /// Gets the price adjustment in cents (can be negative, zero, or positive).
        /// </summary>
        public long PriceAdjustmentCents { get; }
        
        /// <summary>
        /// Gets whether this modifier is applicable to the product category.
        /// </summary>
        public HashSet<string> ApplicableCategories { get; }
        
        /// <summary>
        /// Gets alternative names/spellings for this modifier.
        /// </summary>
        public List<string> Aliases { get; }

        /// <summary>
        /// Initializes a new instance of the ProductModifier.
        /// </summary>
        /// <param name="id">The modifier identifier.</param>
        /// <param name="name">The modifier name.</param>
        /// <param name="description">The modifier description.</param>
        /// <param name="priceAdjustmentCents">Price adjustment in cents.</param>
        /// <param name="applicableCategories">Categories this modifier applies to.</param>
        /// <param name="aliases">Alternative names for this modifier.</param>
        public ProductModifier(string id, string name, string description, long priceAdjustmentCents, 
                             HashSet<string> applicableCategories, List<string> aliases)
        {
            Id = id;
            Name = name;
            Description = description;
            PriceAdjustmentCents = priceAdjustmentCents;
            ApplicableCategories = applicableCategories;
            Aliases = aliases;
        }
    }

    /// <summary>
    /// Represents a customized product with base product + modifiers.
    /// </summary>
    public class CustomizedProduct
    {
        /// <summary>
        /// Gets the base product.
        /// </summary>
        public IProductInfo BaseProduct { get; }
        
        /// <summary>
        /// Gets the applied modifiers.
        /// </summary>
        public List<ProductModifier> Modifiers { get; }
        
        /// <summary>
        /// Gets the customized display name.
        /// </summary>
        public string CustomizedName { get; }
        
        /// <summary>
        /// Gets the total price including modifier adjustments.
        /// </summary>
        public long TotalPriceCents { get; }

        /// <summary>
        /// Initializes a new instance of the CustomizedProduct.
        /// </summary>
        /// <param name="baseProduct">The base product.</param>
        /// <param name="modifiers">Applied modifiers.</param>
        public CustomizedProduct(IProductInfo baseProduct, List<ProductModifier> modifiers)
        {
            BaseProduct = baseProduct;
            Modifiers = modifiers;
            
            // Build customized name
            if (modifiers.Any())
            {
                CustomizedName = $"{baseProduct.Name} {string.Join(" ", modifiers.Select(m => m.Name))}";
            }
            else
            {
                CustomizedName = baseProduct.Name;
            }
            
            // Calculate total price
            TotalPriceCents = baseProduct.BasePriceCents + modifiers.Sum(m => m.PriceAdjustmentCents);
        }
    }

    /// <summary>
    /// Service for parsing and resolving product customizations.
    /// Handles both kopitiam terminology and Western coffee customizations.
    /// </summary>
    public class ProductCustomizationService
    {
        private readonly Dictionary<string, ProductModifier> _modifiers;
        private readonly Dictionary<string, List<string>> _customizationPatterns;

        /// <summary>
        /// Initializes a new instance of the ProductCustomizationService.
        /// </summary>
        public ProductCustomizationService()
        {
            _modifiers = InitializeModifiers();
            _customizationPatterns = InitializeCustomizationPatterns();
        }

        /// <summary>
        /// Attempts to parse a customer request into base product + customizations.
        /// </summary>
        /// <param name="customerInput">What the customer said (e.g., "kopi c kosong").</param>
        /// <param name="availableProducts">Available base products to match against.</param>
        /// <returns>Parsed customization result.</returns>
        public CustomizationParseResult ParseCustomization(string customerInput, IReadOnlyList<IProductInfo> availableProducts)
        {
            var normalizedInput = customerInput.ToLowerInvariant().Trim();
            
            // KOPITIAM CULTURAL TRANSLATION FIRST
            // Apply cultural translation before parsing
            normalizedInput = ApplyKopitiamTranslation(normalizedInput);
            
            // Try to identify base product and modifiers
            var baseProductCandidates = new List<IProductInfo>();
            var detectedModifiers = new List<ProductModifier>();
            
            // Extract known modifiers from the input
            foreach (var modifier in _modifiers.Values)
            {
                if (ContainsModifier(normalizedInput, modifier))
                {
                    detectedModifiers.Add(modifier);
                    // Remove modifier terms from input to help with base product matching
                    normalizedInput = RemoveModifierTerms(normalizedInput, modifier);
                }
            }
            
            // Clean up extra spaces after modifier removal
            normalizedInput = string.Join(" ", normalizedInput.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
            
            // Now try to match base product with cleaned input
            foreach (var product in availableProducts)
            {
                if (MatchesBaseProduct(normalizedInput, product, detectedModifiers))
                {
                    baseProductCandidates.Add(product);
                }
            }
            
            return new CustomizationParseResult
            {
                OriginalInput = customerInput,
                BaseProductCandidates = baseProductCandidates,
                DetectedModifiers = detectedModifiers,
                IsCustomized = detectedModifiers.Any(),
                ParsedSuccessfully = baseProductCandidates.Any()
            };
        }

        /// <summary>
        /// Applies kopitiam-specific cultural translation before parsing.
        /// </summary>
        private string ApplyKopitiamTranslation(string input)
        {
            var translations = new Dictionary<string, string>
            {
                { "roti kaya", "kaya toast" },
                { "roti kaya butter", "kaya butter toast" },
                { "teh si", "teh c" },
                { "kopi si", "kopi c" }
            };
            
            var result = input;
            foreach (var (from, to) in translations)
            {
                // Use word boundary matching to avoid partial matches
                result = System.Text.RegularExpressions.Regex.Replace(result, @"\b" + System.Text.RegularExpressions.Regex.Escape(from) + @"\b", to, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }
            
            return result;
        }

        /// <summary>
        /// Creates a customized product from base product + modifiers.
        /// </summary>
        /// <param name="baseProduct">The base product.</param>
        /// <param name="modifiers">Modifiers to apply.</param>
        /// <returns>Customized product.</returns>
        public CustomizedProduct CreateCustomizedProduct(IProductInfo baseProduct, List<ProductModifier> modifiers)
        {
            // Filter modifiers that are applicable to this product category
            var applicableModifiers = modifiers.Where(m => 
                m.ApplicableCategories.Contains("All") || 
                m.ApplicableCategories.Contains(baseProduct.Category))
                .ToList();
            
            return new CustomizedProduct(baseProduct, applicableModifiers);
        }

        private bool ContainsModifier(string input, ProductModifier modifier)
        {
            var modifierTerms = new List<string> { modifier.Name.ToLowerInvariant() };
            modifierTerms.AddRange(modifier.Aliases.Select(a => a.ToLowerInvariant()));
            
            return modifierTerms.Any(term => input.Contains(term));
        }

        private string RemoveModifierTerms(string input, ProductModifier modifier)
        {
            var modifierTerms = new List<string> { modifier.Name.ToLowerInvariant() };
            modifierTerms.AddRange(modifier.Aliases.Select(a => a.ToLowerInvariant()));
            
            foreach (var term in modifierTerms)
            {
                input = input.Replace(term, "").Trim();
            }
            
            return input;
        }

        private bool MatchesBaseProduct(string cleanedInput, IProductInfo product, List<ProductModifier> modifiers)
        {
            var productName = product.Name.ToLowerInvariant();
            
            // Direct match
            if (productName.Contains(cleanedInput) || cleanedInput.Contains(productName))
            {
                return true;
            }
            
            // Handle kopitiam-specific base matching
            if (IsKopitiamProduct(product))
            {
                return MatchesKopitiamBase(cleanedInput, product);
            }
            
            // Handle exact product name matches
            if (cleanedInput == productName)
            {
                return true;
            }
            
            // Handle partial matches for common cases
            var inputWords = cleanedInput.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var productWords = productName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            // If all input words are found in product name
            return inputWords.All(word => productWords.Any(pWord => pWord.Contains(word)));
        }

        private bool IsKopitiamProduct(IProductInfo product)
        {
            var name = product.Name.ToLowerInvariant();
            return name.Contains("kopi") || name.Contains("teh");
        }

        private bool MatchesKopitiamBase(string input, IProductInfo product)
        {
            var productName = product.Name.ToLowerInvariant();
            
            // Direct base product matching for kopitiam terms
            if (input.Contains("kopi"))
            {
                // Match base kopi products
                if (input == "kopi" && productName == "kopi")
                {
                    return true; // Base Kopi (with condensed milk)
                }
                if (input.Contains("kopi c") && productName == "kopi c")
                {
                    return true; // Base Kopi C (with evaporated milk)
                }
                if (input.Contains("kopi o") && productName == "kopi o")
                {
                    return true; // Base Kopi O (black with sugar)
                }
            }
            
            if (input.Contains("teh"))
            {
                // Match base teh products
                if (input == "teh" && productName == "teh")
                {
                    return true; // Base Teh (with condensed milk)
                }
                if (input.Contains("teh c") && productName == "teh c")
                {
                    return true; // Base Teh C (with evaporated milk)
                }
                if (input.Contains("teh o") && productName == "teh o")
                {
                    return true; // Base Teh O (black with sugar)
                }
            }
            
            // Handle toast products  
            if (input.Contains("kaya toast"))
            {
                return productName.Contains("kaya toast");
            }
            
            // Handle Western coffee terms
            if (input.Contains("coffee"))
            {
                return productName.Contains("coffee") || productName.Contains("kopi");
            }
            
            if (input.Contains("tea"))
            {
                return productName.Contains("tea") || productName.Contains("teh");
            }
            
            return false;
        }

        private Dictionary<string, ProductModifier> InitializeModifiers()
        {
            var modifiers = new List<ProductModifier>
            {
                // Kopitiam Sugar Modifiers
                new ProductModifier("KOSONG", "Kosong", "No sugar", 0, 
                    new HashSet<string> { "Beverages" }, 
                    new List<string> { "no sugar", "without sugar", "plain", "kosong" }),
                
                new ProductModifier("SIEW_DAI", "Siew Dai", "Less sugar", 0, 
                    new HashSet<string> { "Beverages" }, 
                    new List<string> { "less sugar", "little sugar", "siew dai", "siew-dai" }),
                
                new ProductModifier("GA_DAI", "Ga Dai", "Extra sugar", 10, 
                    new HashSet<string> { "Beverages" }, 
                    new List<string> { "extra sugar", "more sugar", "ga dai", "ga-dai", "sweet" }),
                
                // Kopitiam Strength/Concentration Modifiers
                new ProductModifier("GAO", "Gao", "Strong/thick", 0, 
                    new HashSet<string> { "Beverages" }, 
                    new List<string> { "thick", "strong", "concentrated", "gao", "kaw" }),
                
                new ProductModifier("POH", "Poh", "Weak/diluted", 0, 
                    new HashSet<string> { "Beverages" }, 
                    new List<string> { "weak", "diluted", "light", "poh", "po" }),
                
                // Kopitiam Temperature Modifiers
                new ProductModifier("PENG", "Peng", "Iced", 20, 
                    new HashSet<string> { "Beverages" }, 
                    new List<string> { "ice", "iced", "cold", "peng" }),
                
                new ProductModifier("JIAK_GANG", "Jiak Gang", "Extra hot", 0, 
                    new HashSet<string> { "Beverages" }, 
                    new List<string> { "extra hot", "very hot", "jiak gang", "burning" }),
                
                // Western Coffee Modifiers
                new ProductModifier("SOY_MILK", "Soy Milk", "Replace with soy milk", 30, 
                    new HashSet<string> { "Beverages" }, 
                    new List<string> { "soy", "soy milk", "soymilk" }),
                
                new ProductModifier("ALMOND_MILK", "Almond Milk", "Replace with almond milk", 40, 
                    new HashSet<string> { "Beverages" }, 
                    new List<string> { "almond", "almond milk" }),
                
                new ProductModifier("OAT_MILK", "Oat Milk", "Replace with oat milk", 40, 
                    new HashSet<string> { "Beverages" }, 
                    new List<string> { "oat", "oat milk" }),
                
                new ProductModifier("HALF_CAF", "Half-Caf", "Half caffeine", 0, 
                    new HashSet<string> { "Beverages" }, 
                    new List<string> { "half caf", "half-caf", "half caff", "decaf half" }),
                
                new ProductModifier("DECAF", "Decaf", "Decaffeinated", 0, 
                    new HashSet<string> { "Beverages" }, 
                    new List<string> { "decaf", "decaffeinated", "no caffeine" }),
                
                new ProductModifier("EXTRA_SHOT", "Extra Shot", "Additional espresso shot", 60, 
                    new HashSet<string> { "Beverages" }, 
                    new List<string> { "extra shot", "double shot", "add shot", "additional shot" }),
                
                // Syrup Modifiers
                new ProductModifier("VANILLA_SYRUP", "Vanilla Syrup", "Add vanilla syrup", 30, 
                    new HashSet<string> { "Beverages" }, 
                    new List<string> { "vanilla", "vanilla syrup" }),
                
                new ProductModifier("HAZELNUT_SYRUP", "Hazelnut Syrup", "Add hazelnut syrup", 30, 
                    new HashSet<string> { "Beverages" }, 
                    new List<string> { "hazelnut", "hazelnut syrup" }),
                
                new ProductModifier("CARAMEL_SYRUP", "Caramel Syrup", "Add caramel syrup", 30, 
                    new HashSet<string> { "Beverages" }, 
                    new List<string> { "caramel", "caramel syrup" })
            };
            
            return modifiers.ToDictionary(m => m.Id);
        }

        private Dictionary<string, List<string>> InitializeCustomizationPatterns()
        {
            return new Dictionary<string, List<string>>
            {
                // Common patterns for easier parsing
                ["kopitiam_drinks"] = new List<string> { "kopi", "teh", "c", "o", "kosong", "siew dai", "gao", "poh", "peng" },
                ["western_coffee"] = new List<string> { "latte", "cappuccino", "americano", "soy", "almond", "half-caf", "extra shot", "vanilla", "hazelnut" }
            };
        }
    }

    /// <summary>
    /// Result of parsing customer input for product customization.
    /// </summary>
    public class CustomizationParseResult
    {
        /// <summary>
        /// Gets or sets the original customer input.
        /// </summary>
        public string OriginalInput { get; set; } = "";
        
        /// <summary>
        /// Gets or sets the base product candidates found.
        /// </summary>
        public List<IProductInfo> BaseProductCandidates { get; set; } = new();
        
        /// <summary>
        /// Gets or sets the detected modifiers.
        /// </summary>
        public List<ProductModifier> DetectedModifiers { get; set; } = new();
        
        /// <summary>
        /// Gets or sets whether customizations were detected.
        /// </summary>
        public bool IsCustomized { get; set; }
        
        /// <summary>
        /// Gets or sets whether parsing was successful.
        /// </summary>
        public bool ParsedSuccessfully { get; set; }
        
        /// <summary>
        /// Gets or sets any parsing errors or ambiguities.
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}
