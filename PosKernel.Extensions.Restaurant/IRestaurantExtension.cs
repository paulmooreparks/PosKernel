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
