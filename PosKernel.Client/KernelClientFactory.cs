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

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PosKernel.Client.Rust;

namespace PosKernel.Client
{
    /// <summary>
    /// Factory for creating POS kernel clients with configurable backend.
    /// Supports both C# service and Rust service implementations.
    /// </summary>
    public static class PosKernelClientFactory
    {
        /// <summary>
        /// Kernel implementation types.
        /// </summary>
        public enum KernelType
        {
            /// <summary>
            /// C# service implementation (legacy).
            /// </summary>
            CSharpService,

            /// <summary>
            /// Rust service implementation via gRPC (recommended).
            /// </summary>
            RustService,

            /// <summary>
            /// Automatic detection - tries Rust first, falls back to C#.
            /// </summary>
            Auto
        }

        /// <summary>
        /// Creates a kernel client based on configuration.
        /// </summary>
        /// <param name="logger">Logger for the client.</param>
        /// <param name="configuration">Application configuration.</param>
        /// <returns>Configured kernel client.</returns>
        public static IPosKernelClient CreateClient(ILogger logger, IConfiguration? configuration = null)
        {
            var kernelTypeStr = configuration?.GetSection("PosKernel:KernelType")?.Value ?? "Auto";
            
            if (!Enum.TryParse<KernelType>(kernelTypeStr, ignoreCase: true, out var kernelType))
            {
                logger.LogWarning("Invalid kernel type '{KernelType}' in configuration, using Auto", kernelTypeStr);
                kernelType = KernelType.Auto;
            }

            return CreateClient(logger, kernelType, configuration);
        }

        /// <summary>
        /// Creates a kernel client of the specified type.
        /// </summary>
        /// <param name="logger">Logger for the client.</param>
        /// <param name="kernelType">Type of kernel implementation to use.</param>
        /// <param name="configuration">Application configuration.</param>
        /// <returns>Configured kernel client.</returns>
        public static IPosKernelClient CreateClient(ILogger logger, KernelType kernelType, IConfiguration? configuration = null)
        {
            logger.LogInformation("Creating POS kernel client of type: {KernelType}", kernelType);

            return kernelType switch
            {
                KernelType.CSharpService => CreateCSharpServiceClient(logger, configuration),
                KernelType.RustService => CreateRustServiceClient(logger, configuration),
                KernelType.Auto => CreateAutoClient(logger, configuration),
                _ => throw new ArgumentException($"Unknown kernel type: {kernelType}", nameof(kernelType))
            };
        }

        /// <summary>
        /// Creates a C# service client.
        /// </summary>
        internal static IPosKernelClient CreateCSharpServiceClient(ILogger logger, IConfiguration? configuration)
        {
            logger.LogInformation("Creating C# service client");
            
            var options = new PosKernelClientOptions();
            if (configuration != null)
            {
                var section = configuration.GetSection("PosKernel:CSharpService");
                if (section.Exists())
                {
                    section.Bind(options);
                }
            }

            var typedLogger = logger as ILogger<PosKernelClient> ?? 
                new LoggerWrapper<PosKernelClient>(logger);

            return new PosKernelClient(typedLogger, options);
        }

        /// <summary>
        /// Creates a Rust service client.
        /// </summary>
        internal static IPosKernelClient CreateRustServiceClient(ILogger logger, IConfiguration? configuration)
        {
            logger.LogInformation("Creating Rust service gRPC client");
            
            var options = new RustKernelClientOptions();
            if (configuration != null)
            {
                var section = configuration.GetSection("PosKernel:RustService");
                if (section.Exists())
                {
                    section.Bind(options);
                }
            }

            var typedLogger = logger as ILogger<RustKernelClient> ?? 
                new LoggerWrapper<RustKernelClient>(logger);

            return new RustKernelClient(typedLogger, options);
        }

        /// <summary>
        /// Creates a client with automatic fallback logic.
        /// </summary>
        private static IPosKernelClient CreateAutoClient(ILogger logger, IConfiguration? configuration)
        {
            logger.LogInformation("Creating auto-detecting kernel client (Rust → C# fallback)");
            
            return new AutoKernelClient(logger, configuration);
        }
    }

    /// <summary>
    /// Auto-detecting kernel client that tries Rust first, falls back to C#.
    /// </summary>
    internal class AutoKernelClient : IPosKernelClient
    {
        private readonly ILogger _logger;
        private readonly IConfiguration? _configuration;
        private IPosKernelClient? _activeClient;
        private bool _disposed = false;

        public AutoKernelClient(ILogger logger, IConfiguration? configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public bool IsConnected => _activeClient?.IsConnected ?? false;

        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(AutoKernelClient));
            }

            // Try Rust service first
            try
            {
                _logger.LogInformation("Attempting to connect to Rust kernel service...");
                _activeClient = PosKernelClientFactory.CreateRustServiceClient(_logger, _configuration);
                await _activeClient.ConnectAsync(cancellationToken);
                
                _logger.LogInformation("✅ Connected to Rust kernel service successfully");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to connect to Rust kernel service, trying C# service fallback...");
                _activeClient?.Dispose();
                _activeClient = null;
            }

            // Fallback to C# service
            try
            {
                _logger.LogInformation("Attempting to connect to C# kernel service...");
                _activeClient = PosKernelClientFactory.CreateCSharpServiceClient(_logger, _configuration);
                await _activeClient.ConnectAsync(cancellationToken);
                
                _logger.LogInformation("✅ Connected to C# kernel service as fallback");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to connect to any kernel service");
                _activeClient?.Dispose();
                _activeClient = null;
                throw new InvalidOperationException("Unable to connect to any kernel service (tried Rust and C#)", ex);
            }
        }

        public async Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            if (_activeClient != null)
            {
                await _activeClient.DisconnectAsync(cancellationToken);
            }
        }

        public void Disconnect()
        {
            _activeClient?.Disconnect();
        }

        public Task<object> GetStoreConfigAsync(CancellationToken cancellationToken = default)
            => EnsureConnected().GetStoreConfigAsync(cancellationToken);

        public Task<TransactionClientResult> AddChildLineItemAsync(string sessionId, string transactionId, string productId, int quantity, decimal unitPrice, int parentLineNumber, CancellationToken cancellationToken = default)
            => EnsureConnected().AddChildLineItemAsync(sessionId, transactionId, productId, quantity, unitPrice, parentLineNumber, cancellationToken);

        private IPosKernelClient EnsureConnected()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(AutoKernelClient));
            }
            
            if (_activeClient == null)
            {
                throw new InvalidOperationException("Not connected to any kernel service. Call ConnectAsync first.");
            }
            
            return _activeClient;
        }

        // Delegate all operations to the active client
        public Task<string> CreateSessionAsync(string terminalId, string operatorId, CancellationToken cancellationToken = default)
            => EnsureConnected().CreateSessionAsync(terminalId, operatorId, cancellationToken);

        public Task<TransactionClientResult> StartTransactionAsync(string sessionId, string currency = "USD", CancellationToken cancellationToken = default)
            => EnsureConnected().StartTransactionAsync(sessionId, currency, cancellationToken);

        public Task<TransactionClientResult> AddLineItemAsync(string sessionId, string transactionId, string productId, int quantity, decimal unitPrice, CancellationToken cancellationToken = default)
            => EnsureConnected().AddLineItemAsync(sessionId, transactionId, productId, quantity, unitPrice, cancellationToken);

        public Task<TransactionClientResult> AddModificationAsync(string sessionId, string transactionId, int parentLineNumber, string modificationId, int quantity, decimal unitPrice, LineItemType itemType = LineItemType.Modification, CancellationToken cancellationToken = default)
            => EnsureConnected().AddModificationAsync(sessionId, transactionId, parentLineNumber, modificationId, quantity, unitPrice, itemType, cancellationToken);

        public Task<TransactionClientResult> VoidLineItemAsync(string sessionId, string transactionId, int lineNumber, CancellationToken cancellationToken = default)
            => EnsureConnected().VoidLineItemAsync(sessionId, transactionId, lineNumber, cancellationToken);

        public Task<TransactionClientResult> UpdateLineItemQuantityAsync(string sessionId, string transactionId, int lineNumber, int newQuantity, CancellationToken cancellationToken = default)
            => EnsureConnected().UpdateLineItemQuantityAsync(sessionId, transactionId, lineNumber, newQuantity, cancellationToken);

        public Task<TransactionClientResult> ProcessPaymentAsync(string sessionId, string transactionId, decimal amount, string paymentType = "cash", CancellationToken cancellationToken = default)
            => EnsureConnected().ProcessPaymentAsync(sessionId, transactionId, amount, paymentType, cancellationToken);

        public Task<TransactionClientResult> GetTransactionAsync(string sessionId, string transactionId, CancellationToken cancellationToken = default)
            => EnsureConnected().GetTransactionAsync(sessionId, transactionId, cancellationToken);

        public Task CloseSessionAsync(string sessionId, CancellationToken cancellationToken = default)
            => EnsureConnected().CloseSessionAsync(sessionId, cancellationToken);

        public void Dispose()
        {
            if (!_disposed)
            {
                _activeClient?.Dispose();
                _disposed = true;
            }
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
}
