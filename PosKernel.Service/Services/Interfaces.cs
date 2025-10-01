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

using PosKernel.Abstractions;
using System.Text.Json;

namespace PosKernel.Service.Services
{
    /// <summary>
    /// Core POS Kernel engine interface for service-based operations.
    /// Wraps the Rust kernel and provides session-aware transaction management.
    /// </summary>
    public interface IPosKernelEngine
    {
        /// <summary>
        /// Creates a new transaction session.
        /// </summary>
        Task<string> CreateSessionAsync(string terminalId, string operatorId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts a new transaction within a session.
        /// ARCHITECTURAL PRINCIPLE: Currency must be explicitly provided - no defaults allowed.
        /// </summary>
        Task<TransactionResult> StartTransactionAsync(string sessionId, string currency, CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds a line item to the current transaction.
        /// ARCHITECTURAL PRINCIPLE: Accepts optional product metadata from store extension.
        /// Kernel stores all provided metadata for display - does not perform lookups.
        /// </summary>
        Task<TransactionResult> AddLineItemAsync(string sessionId, string transactionId, string productId, int quantity, decimal unitPrice, string? productName = null, string? productDescription = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Processes payment for the transaction.
        /// </summary>
        Task<TransactionResult> ProcessPaymentAsync(string sessionId, string transactionId, decimal amount, string paymentType = "cash", CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current transaction state.
        /// </summary>
        Task<TransactionResult> GetTransactionAsync(string sessionId, string transactionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Closes a session and cleans up resources.
        /// </summary>
        Task CloseSessionAsync(string sessionId, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Session management interface for multi-terminal support.
    /// </summary>
    public interface ISessionManager
    {
        /// <summary>
        /// Creates a new session.
        /// </summary>
        Task<SessionInfo> CreateSessionAsync(string terminalId, string operatorId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets session information.
        /// </summary>
        Task<SessionInfo?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates session activity.
        /// </summary>
        Task UpdateSessionActivityAsync(string sessionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Closes a session.
        /// </summary>
        Task CloseSessionAsync(string sessionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all active sessions.
        /// </summary>
        Task<IReadOnlyList<SessionInfo>> GetActiveSessionsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Cleans up expired sessions.
        /// </summary>
        Task CleanupExpiredSessionsAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Named pipe server interface for local IPC communication.
    /// </summary>
    public interface INamedPipeServer
    {
        /// <summary>
        /// Starts the named pipe server.
        /// </summary>
        Task StartAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops the named pipe server.
        /// </summary>
        Task StopAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Processes a request from a client.
        /// </summary>
        Task<string> ProcessRequestAsync(string requestJson, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// JSON-RPC server interface for remote network communication.
    /// </summary>
    public interface IJsonRpcServer
    {
        /// <summary>
        /// Starts the JSON-RPC server.
        /// </summary>
        Task StartAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops the JSON-RPC server.
        /// </summary>
        Task StopAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Processes a JSON-RPC request.
        /// </summary>
        Task<JsonElement> ProcessRequestAsync(JsonElement request, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Metrics collection interface for performance monitoring.
    /// </summary>
    public interface IMetricsCollector
    {
        /// <summary>
        /// Records a transaction metric.
        /// </summary>
        void RecordTransaction(string operation, TimeSpan duration, bool success);

        /// <summary>
        /// Records a session metric.
        /// </summary>
        void RecordSession(string sessionId, string eventType);

        /// <summary>
        /// Gets current performance metrics.
        /// </summary>
        Task<MetricsSnapshot> GetMetricsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Resets metrics counters.
        /// </summary>
        Task ResetMetricsAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Health checking interface for service monitoring.
    /// </summary>
    public interface IHealthChecker
    {
        /// <summary>
        /// Performs a health check.
        /// </summary>
        Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current health status.
        /// </summary>
        Task<HealthStatus> GetHealthStatusAsync(CancellationToken cancellationToken = default);
    }

    // Data Transfer Objects

    /// <summary>
    /// Information about an active session.
    /// </summary>
    public class SessionInfo
    {
        /// <summary>
        /// Gets or sets the unique session identifier.
        /// </summary>
        public string SessionId { get; set; } = "";

        /// <summary>
        /// Gets or sets the terminal identifier.
        /// </summary>
        public string TerminalId { get; set; } = "";

        /// <summary>
        /// Gets or sets the operator identifier.
        /// </summary>
        public string OperatorId { get; set; } = "";

        /// <summary>
        /// Gets or sets when the session was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the last activity timestamp.
        /// </summary>
        public DateTime LastActivity { get; set; }

        /// <summary>
        /// Gets or sets whether the session is active.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Gets or sets the current transaction identifier, if any.
        /// </summary>
        public string? CurrentTransactionId { get; set; }
    }

    /// <summary>
    /// Result of a transaction operation.
    /// </summary>
    public class TransactionResult
    {
        /// <summary>
        /// Gets or sets whether the operation was successful.
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
        /// Gets or sets the transaction total amount.
        /// </summary>
        public decimal Total { get; set; }

        /// <summary>
        /// Gets or sets the transaction state.
        /// </summary>
        public string State { get; set; } = "";

        /// <summary>
        /// Gets or sets additional transaction data.
        /// </summary>
        public object? Data { get; set; }
    }

    /// <summary>
    /// Snapshot of performance metrics.
    /// </summary>
    public class MetricsSnapshot
    {
        /// <summary>
        /// Gets or sets the timestamp when the snapshot was taken.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the total number of transactions processed.
        /// </summary>
        public int TotalTransactions { get; set; }

        /// <summary>
        /// Gets or sets the number of currently active sessions.
        /// </summary>
        public int ActiveSessions { get; set; }

        /// <summary>
        /// Gets or sets the average transaction processing time in milliseconds.
        /// </summary>
        public double AverageTransactionTime { get; set; }

        /// <summary>
        /// Gets or sets the number of requests processed per second.
        /// </summary>
        public double RequestsPerSecond { get; set; }

        /// <summary>
        /// Gets or sets the current memory usage in bytes.
        /// </summary>
        public long MemoryUsageBytes { get; set; }

        /// <summary>
        /// Gets or sets additional custom metrics.
        /// </summary>
        public Dictionary<string, object> CustomMetrics { get; set; } = new();
    }

    /// <summary>
    /// Result of a health check operation.
    /// </summary>
    public class HealthCheckResult
    {
        /// <summary>
        /// Gets or sets the overall health status.
        /// </summary>
        public HealthStatus Status { get; set; }

        /// <summary>
        /// Gets or sets a description of the health check result.
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// Gets or sets detailed health check information.
        /// </summary>
        public Dictionary<string, object> Details { get; set; } = new();

        /// <summary>
        /// Gets or sets the duration of the health check operation.
        /// </summary>
        public TimeSpan Duration { get; set; }
    }

    /// <summary>
    /// Overall health status of the service.
    /// </summary>
    public enum HealthStatus
    {
        /// <summary>
        /// The service is operating normally.
        /// </summary>
        Healthy,

        /// <summary>
        /// The service is operational but with reduced performance.
        /// </summary>
        Degraded,

        /// <summary>
        /// The service is not operating correctly.
        /// </summary>
        Unhealthy
    }
}
