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

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PosKernel.Service.Services
{
    /// <summary>
    /// Main POS Kernel service host that coordinates all service components.
    /// </summary>
    public class PosKernelServiceHost : BackgroundService
    {
        private readonly ILogger<PosKernelServiceHost> _logger;
        private readonly IPosKernelEngine _kernelEngine;
        private readonly ISessionManager _sessionManager;

        public PosKernelServiceHost(
            ILogger<PosKernelServiceHost> logger,
            IPosKernelEngine kernelEngine,
            ISessionManager sessionManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kernelEngine = kernelEngine ?? throw new ArgumentNullException(nameof(kernelEngine));
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("POS Kernel Service starting...");

            try
            {
                // Service is ready
                _logger.LogInformation("POS Kernel Service started successfully");

                // Keep service running until cancellation is requested
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
                _logger.LogInformation("POS Kernel Service is stopping...");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "POS Kernel Service encountered an error");
                throw;
            }
            finally
            {
                _logger.LogInformation("POS Kernel Service stopped");
            }
        }
    }

    /// <summary>
    /// Hosted service for the Named Pipe server.
    /// </summary>
    public class NamedPipeServerHost : BackgroundService
    {
        private readonly ILogger<NamedPipeServerHost> _logger;
        private readonly INamedPipeServer _namedPipeServer;

        public NamedPipeServerHost(ILogger<NamedPipeServerHost> logger, INamedPipeServer namedPipeServer)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _namedPipeServer = namedPipeServer ?? throw new ArgumentNullException(nameof(namedPipeServer));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation("Starting Named Pipe Server...");
                await _namedPipeServer.StartAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Named Pipe Server");
                throw;
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Stopping Named Pipe Server...");
                await _namedPipeServer.StopAsync(cancellationToken);
                await base.StopAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping Named Pipe Server");
            }
        }
    }

    /// <summary>
    /// Hosted service for the optional JSON-RPC server.
    /// </summary>
    public class JsonRpcServerHost : BackgroundService
    {
        private readonly ILogger<JsonRpcServerHost> _logger;
        private readonly IJsonRpcServer _jsonRpcServer;

        public JsonRpcServerHost(ILogger<JsonRpcServerHost> logger, IJsonRpcServer jsonRpcServer)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _jsonRpcServer = jsonRpcServer ?? throw new ArgumentNullException(nameof(jsonRpcServer));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation("Starting JSON-RPC Server...");
                await _jsonRpcServer.StartAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in JSON-RPC Server");
                throw;
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Stopping JSON-RPC Server...");
                await _jsonRpcServer.StopAsync(cancellationToken);
                await base.StopAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping JSON-RPC Server");
            }
        }
    }

    /// <summary>
    /// Hosted service for metrics collection.
    /// </summary>
    public class MetricsCollectorHost : BackgroundService
    {
        private readonly ILogger<MetricsCollectorHost> _logger;
        private readonly IMetricsCollector _metricsCollector;

        public MetricsCollectorHost(ILogger<MetricsCollectorHost> logger, IMetricsCollector metricsCollector)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _metricsCollector = metricsCollector ?? throw new ArgumentNullException(nameof(metricsCollector));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Metrics Collector started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Collect metrics every minute
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

                    var metrics = await _metricsCollector.GetMetricsAsync(stoppingToken);
                    _logger.LogInformation("Metrics: Transactions={TotalTransactions}, Sessions={ActiveSessions}, AvgTime={AverageTime}ms",
                        metrics.TotalTransactions, metrics.ActiveSessions, metrics.AverageTransactionTime);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error collecting metrics");
                }
            }

            _logger.LogInformation("Metrics Collector stopped");
        }
    }

    /// <summary>
    /// Hosted service for health checking.
    /// </summary>
    public class HealthCheckerHost : BackgroundService
    {
        private readonly ILogger<HealthCheckerHost> _logger;
        private readonly IHealthChecker _healthChecker;

        public HealthCheckerHost(ILogger<HealthCheckerHost> logger, IHealthChecker healthChecker)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _healthChecker = healthChecker ?? throw new ArgumentNullException(nameof(healthChecker));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Health Checker started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Check health every 30 seconds
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

                    var healthResult = await _healthChecker.CheckHealthAsync(stoppingToken);
                    
                    if (healthResult.Status != HealthStatus.Healthy)
                    {
                        _logger.LogWarning("Health check failed: {Status} - {Description}", 
                            healthResult.Status, healthResult.Description);
                    }
                    else
                    {
                        _logger.LogDebug("Health check passed: {Duration}ms", healthResult.Duration.TotalMilliseconds);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during health check");
                }
            }

            _logger.LogInformation("Health Checker stopped");
        }
    }
}
