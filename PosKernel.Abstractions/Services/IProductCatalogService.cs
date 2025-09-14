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
    /// Generic product catalog interface that any client (AI demo, UI, etc.) can use.
    /// This abstracts away the specific domain extension implementation.
    /// </summary>
    public interface IProductCatalogService
    {
        /// <summary>
        /// Validates that a product is available for sale and returns detailed information.
        /// </summary>
        Task<ProductValidationResult> ValidateProductAsync(
            string productId, 
            ProductLookupContext context,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Searches for products matching the given criteria.
        /// </summary>
        Task<IReadOnlyList<IProductInfo>> SearchProductsAsync(
            string searchTerm, 
            int maxResults = 50,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets popular/recommended products for upselling and suggestions.
        /// </summary>
        Task<IReadOnlyList<IProductInfo>> GetPopularItemsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all products in a specific category.
        /// </summary>
        Task<IReadOnlyList<IProductInfo>> GetProductsByCategoryAsync(
            string category, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets available product categories.
        /// </summary>
        Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Generic product information interface that works across all business domains.
    /// </summary>
    public interface IProductInfo
    {
        /// <summary>
        /// Gets the unique product identifier (SKU).
        /// </summary>
        string Sku { get; }

        /// <summary>
        /// Gets the product name for display.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the product description.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets the product category.
        /// </summary>
        string Category { get; }

        /// <summary>
        /// Gets the base price in cents.
        /// </summary>
        long BasePriceCents { get; }

        /// <summary>
        /// Gets whether this product is currently available for sale.
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// Gets additional business-domain specific attributes.
        /// </summary>
        IReadOnlyDictionary<string, object> Attributes { get; }
    }

    /// <summary>
    /// Context for product lookup operations.
    /// </summary>
    public class ProductLookupContext
    {
        public string StoreId { get; set; } = "STORE_DEFAULT";
        public string TerminalId { get; set; } = "TERMINAL_01";
        public string OperatorId { get; set; } = "SYSTEM";
        public DateTime RequestTime { get; set; } = DateTime.Now;
        public Dictionary<string, object> AdditionalContext { get; set; } = new();
    }

    /// <summary>
    /// Result of product validation operation.
    /// </summary>
    public class ProductValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = "";
        public IProductInfo? Product { get; set; }
        public long EffectivePriceCents { get; set; }
    }

    /// <summary>
    /// Generic product information implementation.
    /// </summary>
    public class GenericProductInfo : IProductInfo
    {
        public string Sku { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Category { get; set; } = "";
        public long BasePriceCents { get; set; }
        public bool IsActive { get; set; } = true;
        public IReadOnlyDictionary<string, object> Attributes { get; set; } = new Dictionary<string, object>();
    }
}
