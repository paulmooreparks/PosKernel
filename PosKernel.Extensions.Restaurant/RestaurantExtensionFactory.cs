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

using Microsoft.Extensions.Logging;
using PosKernel.Extensions.Restaurant.Client;

namespace PosKernel.Extensions.Restaurant
{
    /// <summary>
    /// Factory for creating restaurant extension instances.
    /// Supports both in-process and IPC modes.
    /// </summary>
    public static class RestaurantExtensionFactory
    {
        /// <summary>
        /// Creates an in-process restaurant extension instance.
        /// </summary>
        /// <param name="logger">Logger instance.</param>
        /// <param name="databasePath">Optional database path override.</param>
        /// <returns>In-process restaurant extension.</returns>
        public static IRestaurantExtension CreateInProcess(ILogger logger, string? databasePath = null)
        {
            var typedLogger = logger as ILogger<InProcessRestaurantExtension> ?? 
                new LoggerWrapper<InProcessRestaurantExtension>(logger);
            return new InProcessRestaurantExtension(typedLogger, databasePath);
        }

        /// <summary>
        /// Creates an IPC restaurant extension client.
        /// </summary>
        /// <param name="logger">Logger instance.</param>
        /// <param name="pipeName">Named pipe name for IPC.</param>
        /// <returns>IPC restaurant extension client.</returns>
        public static IRestaurantExtension CreateIpcClient(ILogger logger, string pipeName = "poskernel-restaurant-extension")
        {
            var typedLogger = logger as ILogger<RestaurantExtensionClient> ?? 
                new LoggerWrapper<RestaurantExtensionClient>(logger);
            return new RestaurantExtensionClient(typedLogger, pipeName);
        }

        /// <summary>
        /// Creates a restaurant extension with automatic fallback.
        /// Tries IPC first, falls back to in-process if service is not available.
        /// </summary>
        /// <param name="logger">Logger instance.</param>
        /// <param name="pipeName">Named pipe name for IPC.</param>
        /// <param name="databasePath">Database path for in-process fallback.</param>
        /// <returns>Restaurant extension with automatic fallback.</returns>
        public static IRestaurantExtension CreateWithFallback(ILogger logger, string pipeName = "poskernel-restaurant-extension", string? databasePath = null)
        {
            return new FallbackRestaurantExtension(logger, pipeName, databasePath);
        }
    }

    /// <summary>
    /// Logger wrapper to adapt ILogger to ILogger&lt;T&gt;.
    /// </summary>
    internal class LoggerWrapper<T> : ILogger<T>
    {
        private readonly ILogger _logger;

        public LoggerWrapper(ILogger logger)
        {
            _logger = logger;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => _logger.BeginScope(state);
        public bool IsEnabled(LogLevel logLevel) => _logger.IsEnabled(logLevel);
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) 
            => _logger.Log(logLevel, eventId, state, exception, formatter);
    }

    /// <summary>
    /// Restaurant extension that automatically falls back from IPC to in-process.
    /// </summary>
    internal class FallbackRestaurantExtension : IRestaurantExtension, IDisposable
    {
        private readonly ILogger _logger;
        private readonly string _pipeName;
        private readonly string? _databasePath;
        private IRestaurantExtension? _activeExtension;
        private bool _disposed = false;

        public FallbackRestaurantExtension(ILogger logger, string pipeName, string? databasePath)
        {
            _logger = logger;
            _pipeName = pipeName;
            _databasePath = databasePath;
        }

        private async Task<IRestaurantExtension> GetActiveExtensionAsync(CancellationToken cancellationToken = default)
        {
            if (_activeExtension != null)
            {
                return _activeExtension;
            }

            // Try IPC first
            try
            {
                var ipcExtension = RestaurantExtensionFactory.CreateIpcClient(_logger, _pipeName);
                if (await ipcExtension.IsAvailableAsync(cancellationToken))
                {
                    _logger.LogInformation("Using IPC restaurant extension service");
                    _activeExtension = ipcExtension;
                    return _activeExtension;
                }
                
                ipcExtension.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to connect to restaurant extension service, falling back to in-process");
            }

            // Fallback to in-process
            _logger.LogInformation("Using in-process restaurant extension");
            _activeExtension = RestaurantExtensionFactory.CreateInProcess(_logger, _databasePath);
            return _activeExtension;
        }

        public async Task<ProductValidationResult> ValidateProductAsync(string productId, string storeId = "STORE_DEFAULT", string terminalId = "TERMINAL_01", string operatorId = "SYSTEM", CancellationToken cancellationToken = default)
        {
            var extension = await GetActiveExtensionAsync(cancellationToken);
            return await extension.ValidateProductAsync(productId, storeId, terminalId, operatorId, cancellationToken);
        }

        public async Task<List<ProductInfo>> SearchProductsAsync(string searchTerm, int maxResults = 50, CancellationToken cancellationToken = default)
        {
            var extension = await GetActiveExtensionAsync(cancellationToken);
            return await extension.SearchProductsAsync(searchTerm, maxResults, cancellationToken);
        }

        public async Task<List<ProductInfo>> GetPopularItemsAsync(CancellationToken cancellationToken = default)
        {
            var extension = await GetActiveExtensionAsync(cancellationToken);
            return await extension.GetPopularItemsAsync(cancellationToken);
        }

        public async Task<List<ProductInfo>> GetCategoryProductsAsync(string category, CancellationToken cancellationToken = default)
        {
            var extension = await GetActiveExtensionAsync(cancellationToken);
            return await extension.GetCategoryProductsAsync(category, cancellationToken);
        }

        public async Task<List<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
        {
            var extension = await GetActiveExtensionAsync(cancellationToken);
            return await extension.GetCategoriesAsync(cancellationToken);
        }

        public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
        {
            var extension = await GetActiveExtensionAsync(cancellationToken);
            return await extension.IsAvailableAsync(cancellationToken);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _activeExtension?.Dispose();
                _disposed = true;
            }
        }
    }
}
