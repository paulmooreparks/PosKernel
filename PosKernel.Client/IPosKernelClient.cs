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
    /// Result of a client transaction operation.
    /// </summary>
    public class TransactionClientResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public string? TransactionId { get; set; }
        public string? SessionId { get; set; }
        public decimal Total { get; set; }
        public string State { get; set; } = "";
        public object? Data { get; set; }
    }

    /// <summary>
    /// Configuration options for the POS Kernel Client.
    /// </summary>
    public class PosKernelClientOptions
    {
        public string PipeName { get; set; } = "poskernel-service";
        public int ConnectionTimeoutMs { get; set; } = 5000;
        public int RequestTimeoutMs { get; set; } = 30000;
        public int MaxRetryAttempts { get; set; } = 3;
        public int RetryDelayMs { get; set; } = 1000;
        public bool AutoReconnect { get; set; } = true;
        public int KeepAliveIntervalMs { get; set; } = 30000;
    }
}
