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

namespace PosKernel.Client
{
    /// <summary>
    /// Client interface for communicating with POS Kernel Service.
    /// Provides the same API as the in-process kernel but over IPC.
    /// </summary>
    public interface IPosKernelClient : IDisposable
    {
        /// <summary>
        /// Connects to the POS Kernel Service.
        /// </summary>
        Task ConnectAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Disconnects from the POS Kernel Service.
        /// </summary>
        Task DisconnectAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the connection status.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Creates a new session.
        /// </summary>
        Task<string> CreateSessionAsync(string terminalId, string operatorId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts a new transaction.
        /// </summary>
        Task<TransactionClientResult> StartTransactionAsync(string sessionId, string currency = "USD", CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds a line item to the transaction.
        /// </summary>
        Task<TransactionClientResult> AddLineItemAsync(string sessionId, string transactionId, string productId, int quantity, decimal unitPrice, CancellationToken cancellationToken = default);

        /// <summary>
        /// Processes payment for the transaction.
        /// </summary>
        Task<TransactionClientResult> ProcessPaymentAsync(string sessionId, string transactionId, decimal amount, string paymentType = "cash", CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current transaction state.
        /// </summary>
        Task<TransactionClientResult> GetTransactionAsync(string sessionId, string transactionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Closes a session.
        /// </summary>
        Task CloseSessionAsync(string sessionId, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Result from client transaction operations.
    /// </summary>
    public class TransactionClientResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the operation succeeded.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the error message if the operation failed.
        /// </summary>
        public string? Error { get; set; }

        /// <summary>
        /// Gets or sets the transaction identifier.
        /// </summary>
        public string? TransactionId { get; set; }

        /// <summary>
        /// Gets or sets the session identifier.
        /// </summary>
        public string? SessionId { get; set; }

        /// <summary>
        /// Gets or sets the total amount.
        /// </summary>
        public decimal Total { get; set; }

        /// <summary>
        /// Gets or sets the transaction state.
        /// </summary>
        public string State { get; set; } = "";

        /// <summary>
        /// Gets or sets additional result data.
        /// </summary>
        public object? Data { get; set; }
    }

    /// <summary>
    /// Configuration options for POS Kernel client connections.
    /// </summary>
    public class PosKernelClientOptions
    {
        /// <summary>
        /// Gets or sets the named pipe name.
        /// </summary>
        public string PipeName { get; set; } = "poskernel-service";

        /// <summary>
        /// Gets or sets the connection timeout in milliseconds.
        /// </summary>
        public int ConnectionTimeoutMs { get; set; } = 5000;

        /// <summary>
        /// Gets or sets the request timeout in milliseconds.
        /// </summary>
        public int RequestTimeoutMs { get; set; } = 30000;

        /// <summary>
        /// Gets or sets the maximum retry attempts.
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// Gets or sets the retry delay in milliseconds.
        /// </summary>
        public int RetryDelayMs { get; set; } = 1000;

        /// <summary>
        /// Gets or sets a value indicating whether to auto-reconnect on failure.
        /// </summary>
        public bool AutoReconnect { get; set; } = true;

        /// <summary>
        /// Gets or sets the keep-alive interval in milliseconds.
        /// </summary>
        public int KeepAliveIntervalMs { get; set; } = 30000;
    }
}
