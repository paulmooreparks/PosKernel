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
using System.Threading.Tasks;

namespace PosKernel.Abstractions.CAL.Products
{
    /// <summary>
    /// Product Customization Abstraction Layer (CAL) interfaces.
    /// Allows different business domains to plug in their own product catalog implementations
    /// without the kernel imposing any specific data structure assumptions.
    /// </summary>
    
    /// <summary>
    /// Minimal product interface that the kernel requires for transaction processing.
    /// All other product attributes are business-domain specific and handled by CAL providers.
    /// </summary>
    public interface IKernelProduct
    {
        /// <summary>
        /// Gets the unique identifier for this product (SKU, UPC, GTIN, etc.).
        /// The kernel treats this as an opaque string - format is CAL provider specific.
        /// </summary>
        string Identifier { get; }

        /// <summary>
        /// Gets a human-readable name for display purposes.
        /// Used in receipts and transaction logs.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Gets the base price in the transaction currency.
        /// May be overridden by pricing CAL providers.
        /// </summary>
        decimal BasePrice { get; }

        /// <summary>
        /// Gets whether this product is currently available for sale.
        /// Checked by the kernel before allowing line item creation.
        /// </summary>
        bool IsAvailableForSale { get; }
    }

    /// <summary>
    /// Product lookup result containing both the kernel-required data and business-specific attributes.
    /// </summary>
    public class ProductLookupResult
    {
        /// <summary>
        /// Gets the kernel product interface.
        /// </summary>
        public IKernelProduct KernelProduct { get; init; } = null!;

        /// <summary>
        /// Gets business-domain specific product attributes.
        /// Opaque to the kernel, used by business logic layers.
        /// </summary>
        public IReadOnlyDictionary<string, object> BusinessAttributes { get; init; } = new Dictionary<string, object>();

        /// <summary>
        /// Gets metadata about the lookup operation.
        /// </summary>
        public ProductLookupMetadata Metadata { get; init; } = new();
    }

    /// <summary>
    /// Metadata about a product lookup operation.
    /// </summary>
    public class ProductLookupMetadata
    {
        /// <summary>
        /// Gets the source system that provided this product data.
        /// </summary>
        public string Source { get; init; } = string.Empty;

        /// <summary>
        /// Gets the timestamp when this data was last updated.
        /// </summary>
        public DateTimeOffset LastUpdated { get; init; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Gets the cache TTL for this product data.
        /// </summary>
        public TimeSpan? CacheTimeToLive { get; init; }

        /// <summary>
        /// Gets any warnings or notices about this product.
        /// </summary>
        public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    }

    /// <summary>
    /// Product search criteria - minimal interface that all CAL providers must support.
    /// </summary>
    public class ProductSearchCriteria
    {
        /// <summary>
        /// Gets or sets the search term (SKU, name, barcode, etc.).
        /// Interpretation is CAL provider specific.
        /// </summary>
        public string? SearchTerm { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of results to return.
        /// </summary>
        public int MaxResults { get; set; } = 50;

        /// <summary>
        /// Gets or sets CAL provider specific search filters.
        /// </summary>
        public IReadOnlyDictionary<string, object> ProviderFilters { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Core CAL interface for product catalog operations.
    /// Each business domain implements this to provide their specific product catalog logic.
    /// </summary>
    public interface IProductCatalogProvider
    {
        /// <summary>
        /// Gets the unique identifier for this CAL provider.
        /// </summary>
        string ProviderId { get; }

        /// <summary>
        /// Gets the business domain this provider supports (e.g., "retail", "restaurant", "pharmacy").
        /// </summary>
        string BusinessDomain { get; }

        /// <summary>
        /// Gets the supported identifier formats (e.g., "sku", "upc", "gtin", "ndc").
        /// </summary>
        IReadOnlyList<string> SupportedIdentifierFormats { get; }

        /// <summary>
        /// Looks up a product by its identifier.
        /// </summary>
        /// <param name="identifier">The product identifier (format specific to provider).</param>
        /// <param name="context">Store/terminal context for the lookup.</param>
        /// <returns>The product if found, otherwise null.</returns>
        Task<ProductLookupResult?> LookupProductAsync(string identifier, ProductLookupContext context);

        /// <summary>
        /// Searches for products based on criteria.
        /// </summary>
        /// <param name="criteria">The search criteria.</param>
        /// <param name="context">Store/terminal context for the search.</param>
        /// <returns>Matching products.</returns>
        Task<IReadOnlyList<ProductLookupResult>> SearchProductsAsync(ProductSearchCriteria criteria, ProductLookupContext context);

        /// <summary>
        /// Validates that a product identifier is properly formatted for this provider.
        /// </summary>
        /// <param name="identifier">The identifier to validate.</param>
        /// <returns>True if the identifier format is valid.</returns>
        bool IsValidIdentifierFormat(string identifier);

        /// <summary>
        /// Gets configuration schema for this provider.
        /// Used by setup tools to generate configuration templates.
        /// </summary>
        Task<string> GetConfigurationSchemaAsync();
    }

    /// <summary>
    /// Context information for product lookups.
    /// </summary>
    public class ProductLookupContext
    {
        /// <summary>
        /// Gets or sets the store identifier.
        /// </summary>
        public string StoreId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the terminal identifier.
        /// </summary>
        public string TerminalId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the transaction currency.
        /// </summary>
        public string Currency { get; set; } = "USD";

        /// <summary>
        /// Gets or sets the operator performing the lookup.
        /// </summary>
        public string OperatorId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets additional context data specific to the provider.
        /// </summary>
        public IReadOnlyDictionary<string, object> ProviderContext { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Factory for creating and managing product catalog providers.
    /// </summary>
    public interface IProductCatalogProviderFactory
    {
        /// <summary>
        /// Creates a product catalog provider based on configuration.
        /// </summary>
        /// <param name="providerId">The provider identifier from configuration.</param>
        /// <param name="configuration">Provider-specific configuration data.</param>
        /// <returns>The configured provider instance.</returns>
        Task<IProductCatalogProvider> CreateProviderAsync(string providerId, IReadOnlyDictionary<string, object> configuration);

        /// <summary>
        /// Gets available provider types that can be created.
        /// </summary>
        /// <returns>List of available provider identifiers and descriptions.</returns>
        IReadOnlyList<(string ProviderId, string Description, string BusinessDomain)> GetAvailableProviders();

        /// <summary>
        /// Registers a new provider type.
        /// </summary>
        void RegisterProvider<T>() where T : class, IProductCatalogProvider;
    }

    /// <summary>
    /// High-level service that orchestrates multiple product catalog providers.
    /// The kernel uses this interface for all product-related operations.
    /// </summary>
    public interface IProductCatalogService
    {
        /// <summary>
        /// Looks up a product using all configured providers in priority order.
        /// </summary>
        /// <param name="identifier">The product identifier.</param>
        /// <param name="context">Lookup context.</param>
        /// <returns>The first successful product lookup result.</returns>
        Task<ProductLookupResult?> LookupProductAsync(string identifier, ProductLookupContext context);

        /// <summary>
        /// Searches across all configured providers.
        /// </summary>
        /// <param name="criteria">Search criteria.</param>
        /// <param name="context">Search context.</param>
        /// <returns>Aggregated search results from all providers.</returns>
        Task<IReadOnlyList<ProductLookupResult>> SearchProductsAsync(ProductSearchCriteria criteria, ProductLookupContext context);

        /// <summary>
        /// Gets the active providers for a given store/terminal.
        /// </summary>
        /// <param name="storeId">Store identifier.</param>
        /// <param name="terminalId">Terminal identifier.</param>
        /// <returns>List of active providers in priority order.</returns>
        Task<IReadOnlyList<IProductCatalogProvider>> GetActiveProvidersAsync(string storeId, string terminalId);
    }
}
