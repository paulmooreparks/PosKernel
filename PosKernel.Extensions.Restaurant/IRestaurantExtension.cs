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

using PosKernel.Extensions.Restaurant;

namespace PosKernel.Extensions.Restaurant
{
    /// <summary>
    /// Represents a set definition from the database.
    /// </summary>
    public class SetDefinition
    {
        /// <summary>Set product SKU</summary>
        public string SetSku { get; set; } = string.Empty;
        /// <summary>Set type (e.g., TOAST_SET, LOCAL_SET)</summary>
        public string SetType { get; set; } = string.Empty;
        /// <summary>Main product SKU for the set</summary>
        public string MainProductSku { get; set; } = string.Empty;
        /// <summary>Base price in cents</summary>
        public decimal BasePriceCents { get; set; }
        /// <summary>Whether the set is active</summary>
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// Represents an available drink for a set.
    /// </summary>
    public class SetAvailableDrink
    {
        /// <summary>Set SKU this drink belongs to</summary>
        public string SetSku { get; set; } = string.Empty;
        /// <summary>Product SKU of the drink</summary>
        public string ProductSku { get; set; } = string.Empty;
        /// <summary>Display name of the drink</summary>
        public string ProductName { get; set; } = string.Empty;
        /// <summary>Drink size (e.g., MEDIUM, LARGE)</summary>
        public string DrinkSize { get; set; } = string.Empty;
        /// <summary>Whether this is the default drink choice</summary>
        public bool IsDefault { get; set; }
        /// <summary>Whether this drink option is active</summary>
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// Represents an available side for a set.
    /// </summary>
    public class SetAvailableSide
    {
        /// <summary>Set SKU this side belongs to</summary>
        public string SetSku { get; set; } = string.Empty;
        /// <summary>Product SKU of the side</summary>
        public string ProductSku { get; set; } = string.Empty;
        /// <summary>Display name of the side</summary>
        public string ProductName { get; set; } = string.Empty;
        /// <summary>Whether this is the default side choice</summary>
        public bool IsDefault { get; set; }
        /// <summary>Whether this side option is active</summary>
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// Interface for restaurant extension operations.
    /// Can be implemented as both in-process service or IPC client.
    /// </summary>
    public interface IRestaurantExtension : IDisposable
    {
        /// <summary>
        /// Validates a product and returns detailed information.
        /// </summary>
        Task<ProductValidationResult> ValidateProductAsync(
            string productId,
            string storeId = "STORE_DEFAULT",
            string terminalId = "TERMINAL_01",
            string operatorId = "SYSTEM",
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Searches for products matching the given search term.
        /// </summary>
        Task<List<ProductInfo>> SearchProductsAsync(
            string searchTerm,
            int maxResults = 50,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the most popular products for recommendations.
        /// </summary>
        Task<List<ProductInfo>> GetPopularItemsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all products in a specific category.
        /// </summary>
        Task<List<ProductInfo>> GetCategoryProductsAsync(
            string category,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all available categories.
        /// </summary>
        Task<List<string>> GetCategoriesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if the service is available.
        /// </summary>
        Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets set definition information for a product SKU.
        /// </summary>
        Task<SetDefinition?> GetSetDefinitionAsync(string productSku, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets available drinks for a set product.
        /// </summary>
        Task<List<SetAvailableDrink>?> GetSetAvailableDrinksAsync(string setSku, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets available sides for a set product.
        /// </summary>
        Task<List<SetAvailableSide>?> GetSetAvailableSidesAsync(string setSku, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Extensions to convert between Restaurant ProductInfo and PosKernel IProductInfo.
    /// </summary>
    public static class RestaurantExtensions
    {
        /// <summary>
        /// Converts Restaurant ProductInfo to PosKernel IProductInfo.
        /// </summary>
        public static PosKernel.Abstractions.Services.IProductInfo AsProductInfo(this ProductInfo productInfo)
        {
            return new RestaurantProductInfoAdapter(productInfo);
        }

        /// <summary>
        /// Converts list of Restaurant ProductInfo to PosKernel IProductInfo.
        /// </summary>
        public static List<PosKernel.Abstractions.Services.IProductInfo> AsProductInfoList(this List<ProductInfo> productInfos)
        {
            return productInfos.Select(p => p.AsProductInfo()).ToList();
        }
    }

    /// <summary>
    /// Adapter to convert Restaurant ProductInfo to PosKernel IProductInfo interface.
    /// </summary>
    internal class RestaurantProductInfoAdapter : PosKernel.Abstractions.Services.IProductInfo
    {
        private readonly ProductInfo _productInfo;

        public RestaurantProductInfoAdapter(ProductInfo productInfo)
        {
            _productInfo = productInfo;
        }

        public string Sku => _productInfo.Sku;
        public string Name => _productInfo.Name;
        public string Description => _productInfo.Description;
        public string Category => _productInfo.CategoryName;
        public decimal BasePrice => _productInfo.BasePriceCents / 100m;
        public long BasePriceCents => _productInfo.BasePriceCents;
        public bool IsActive => _productInfo.IsActive;
        public bool RequiresPreparation => false; // Default for legacy compatibility
        public int PreparationTimeMinutes => 0; // Default for legacy compatibility
        public string? NameLocalizationKey => null; // Default for legacy compatibility
        public string? DescriptionLocalizationKey => null; // Default for legacy compatibility
        public IReadOnlyDictionary<string, object> Attributes => new Dictionary<string, object>
        {
            ["CategoryName"] = _productInfo.CategoryName,
            ["SourceType"] = "Restaurant"
        };
    }
}
