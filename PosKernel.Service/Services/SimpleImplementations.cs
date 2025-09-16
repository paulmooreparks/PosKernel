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
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace PosKernel.Service.Services
{
    /// <summary>
    /// Simple metrics collector implementation for performance monitoring.
    /// </summary>
    public class MetricsCollector : IMetricsCollector
    {
        private readonly ILogger<MetricsCollector> _logger;
        private readonly ConcurrentDictionary<string, long> _counters;
        private readonly ConcurrentDictionary<string, double> _timings;
        private readonly object _lockObject = new();
        private int _totalTransactions;
        private int _successfulTransactions;
        private double _totalTransactionTime;

        /// <summary>
        /// Initializes a new instance of the MetricsCollector class.
        /// </summary>
        /// <param name="logger">Logger for debugging and diagnostics.</param>
        public MetricsCollector(ILogger<MetricsCollector> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _counters = new ConcurrentDictionary<string, long>();
            _timings = new ConcurrentDictionary<string, double>();
        }

        /// <summary>
        /// Records a transaction metric for performance monitoring.
        /// </summary>
        /// <param name="operation">The operation name.</param>
        /// <param name="duration">The operation duration.</param>
        /// <param name="success">Whether the operation was successful.</param>
        public void RecordTransaction(string operation, TimeSpan duration, bool success)
        {
            lock (_lockObject)
            {
                _totalTransactions++;
                if (success)
                {
                    _successfulTransactions++;
                }

                var durationMs = duration.TotalMilliseconds;
                _totalTransactionTime += durationMs;

                // Update operation-specific metrics
                _counters.AddOrUpdate($"{operation}_count", 1, (k, v) => v + 1);
                _timings.AddOrUpdate($"{operation}_avg_time", durationMs, (k, v) => (v + durationMs) / 2);
            }

            _logger.LogDebug("Recorded transaction: {Operation} took {Duration}ms, Success: {Success}",
                operation, duration.TotalMilliseconds, success);
        }

        /// <summary>
        /// Records a session event for monitoring.
        /// </summary>
        /// <param name="sessionId">The session identifier.</param>
        /// <param name="eventName">The event name.</param>
        public void RecordSession(string sessionId, string eventName)
        {
            _counters.AddOrUpdate($"session_{eventName}", 1, (k, v) => v + 1);
            _logger.LogDebug("Recorded session event: {SessionId} -> {Event}", sessionId, eventName);
        }

        /// <summary>
        /// Gets the current performance metrics snapshot.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A snapshot of current performance metrics.</returns>
        public async Task<MetricsSnapshot> GetMetricsAsync(CancellationToken cancellationToken = default)
        {
            var process = Process.GetCurrentProcess();
            
            MetricsSnapshot result;
            lock (_lockObject)
            {
                var averageTime = _totalTransactions > 0 ? _totalTransactionTime / _totalTransactions : 0;
                
                result = new MetricsSnapshot
                {
                    Timestamp = DateTime.UtcNow,
                    TotalTransactions = _totalTransactions,
                    ActiveSessions = (int)_counters.GetValueOrDefault("session_created", 0) - 
                                   (int)_counters.GetValueOrDefault("session_closed", 0),
                    AverageTransactionTime = averageTime,
                    RequestsPerSecond = CalculateRequestsPerSecond(),
                    MemoryUsageBytes = process.WorkingSet64,
                    CustomMetrics = new Dictionary<string, object>
                    {
                        ["success_rate"] = _totalTransactions > 0 ? (double)_successfulTransactions / _totalTransactions : 1.0,
                        ["operation_counts"] = _counters.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value),
                        ["operation_timings"] = _timings.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value)
                    }
                };
            }

            // Fix CS1996 warning - await outside of lock
            await Task.CompletedTask;
            return result;
        }

        /// <summary>
        /// Resets all collected metrics.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous reset operation.</returns>
        public async Task ResetMetricsAsync(CancellationToken cancellationToken = default)
        {
            lock (_lockObject)
            {
                _totalTransactions = 0;
                _successfulTransactions = 0;
                _totalTransactionTime = 0;
                _counters.Clear();
                _timings.Clear();
            }

            _logger.LogInformation("Metrics reset");
            
            // Fix CS1998 warning
            await Task.CompletedTask;
        }

        private double CalculateRequestsPerSecond()
        {
            // Simple calculation - in a real implementation, you'd track this over time windows
            return _totalTransactions > 0 ? _totalTransactions / Math.Max(1, DateTime.UtcNow.Hour) : 0;
        }
    }

    /// <summary>
    /// Simple health checker implementation for service monitoring.
    /// </summary>
    public class HealthChecker : IHealthChecker
    {
        private readonly ILogger<HealthChecker> _logger;
        private readonly IPosKernelEngine _kernelEngine;
        private readonly ISessionManager _sessionManager;

        /// <summary>
        /// Initializes a new instance of the HealthChecker class.
        /// </summary>
        /// <param name="logger">Logger for debugging and diagnostics.</param>
        /// <param name="kernelEngine">The POS kernel engine to monitor.</param>
        /// <param name="sessionManager">The session manager to monitor.</param>
        public HealthChecker(
            ILogger<HealthChecker> logger,
            IPosKernelEngine kernelEngine,
            ISessionManager sessionManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kernelEngine = kernelEngine ?? throw new ArgumentNullException(nameof(kernelEngine));
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        }

        /// <summary>
        /// Performs a comprehensive health check of the system.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The health check result with detailed information.</returns>
        public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;
            var details = new Dictionary<string, object>();
            var status = HealthStatus.Healthy;
            var description = "All systems operational";

            try
            {
                // Check memory usage
                var process = Process.GetCurrentProcess();
                var memoryMB = process.WorkingSet64 / (1024 * 1024);
                details["memory_mb"] = memoryMB;

                if (memoryMB > 1000) // 1GB threshold
                {
                    status = HealthStatus.Degraded;
                    description = "High memory usage detected";
                }

                // Check session manager
                var activeSessions = await _sessionManager.GetActiveSessionsAsync(cancellationToken);
                details["active_sessions"] = activeSessions.Count;

                // Check disk space (simplified)
                var currentDir = Directory.GetCurrentDirectory();
                var drive = new DriveInfo(Path.GetPathRoot(currentDir)!);
                var freeSpaceGB = drive.AvailableFreeSpace / (1024 * 1024 * 1024);
                details["free_space_gb"] = freeSpaceGB;

                if (freeSpaceGB < 1) // Less than 1GB free
                {
                    status = HealthStatus.Degraded;
                    description = "Low disk space detected";
                }

                details["uptime_minutes"] = (DateTime.UtcNow - process.StartTime).TotalMinutes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during health check");
                status = HealthStatus.Unhealthy;
                description = $"Health check failed: {ex.Message}";
                details["error"] = ex.Message;
            }

            var duration = DateTime.UtcNow - startTime;

            return new HealthCheckResult
            {
                Status = status,
                Description = description,
                Details = details,
                Duration = duration
            };
        }

        /// <summary>
        /// Gets the current health status of the system.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The current health status.</returns>
        public async Task<HealthStatus> GetHealthStatusAsync(CancellationToken cancellationToken = default)
        {
            var result = await CheckHealthAsync(cancellationToken);
            return result.Status;
        }
    }

    /// <summary>
    /// Simple JSON-RPC server implementation for remote network communication.
    /// Currently a placeholder - full implementation would use ASP.NET Core or similar.
    /// </summary>
    public class JsonRpcServer : IJsonRpcServer
    {
        private readonly ILogger<JsonRpcServer> _logger;

        /// <summary>
        /// Initializes a new instance of the JsonRpcServer class.
        /// </summary>
        /// <param name="logger">Logger for debugging and diagnostics.</param>
        public JsonRpcServer(ILogger<JsonRpcServer> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Starts the JSON-RPC server (placeholder implementation).
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous start operation.</returns>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("JSON-RPC Server start requested (placeholder implementation)");
            
            // In a real implementation, this would start an HTTP server
            // For now, just log that it's disabled
            _logger.LogInformation("JSON-RPC Server is disabled in this version - use Named Pipes for communication");
            
            // Fix CS1998 warning
            await Task.CompletedTask;
        }

        /// <summary>
        /// Stops the JSON-RPC server (placeholder implementation).
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous stop operation.</returns>
        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("JSON-RPC Server stop requested");
            
            // Fix CS1998 warning
            await Task.CompletedTask;
        }

        /// <summary>
        /// Processes a JSON-RPC request (placeholder implementation).
        /// </summary>
        /// <param name="request">The JSON-RPC request to process.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The JSON-RPC response as a JsonElement.</returns>
        public async Task<JsonElement> ProcessRequestAsync(JsonElement request, CancellationToken cancellationToken = default)
        {
            // Placeholder implementation
            _logger.LogDebug("JSON-RPC request received (placeholder)");
            
            var result = JsonDocument.Parse("""{"jsonrpc": "2.0", "error": {"code": "not_implemented", "message": "JSON-RPC server not implemented in this version"}}""").RootElement;
            
            // Fix CS1998 warning
            await Task.CompletedTask;
            return result;
        }
    }
}
