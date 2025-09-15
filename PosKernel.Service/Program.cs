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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PosKernel.Service.Services;
using System.Runtime.InteropServices;

namespace PosKernel.Service
{
    /// <summary>
    /// Main program for POS Kernel Service Host.
    /// Provides cross-platform service hosting for POS Kernel with IPC communication,
    /// session management, and enterprise features.
    /// </summary>
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            try
            {
                var builder = CreateHostBuilder(args);
                var host = builder.Build();

                Console.WriteLine("🏪 POS Kernel Service Host v0.5.0");
                Console.WriteLine("Enterprise-grade POS transaction kernel with domain extensions");
                Console.WriteLine();

                if (args.Length > 0 && args[0] == "--console")
                {
                    Console.WriteLine("Running in console mode for development/testing...");
                    await host.RunAsync();
                }
                else
                {
                    Console.WriteLine($"Starting as {(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Windows Service" : "System Daemon")}...");
                    await host.RunAsync();
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"❌ Fatal error starting POS Kernel Service: {ex.Message}");
                Console.Error.WriteLine($"Details: {ex}");
                return 1;
            }
        }

        static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseConsoleLifetime() // For development
                .UseWindowsService(options =>
                {
                    options.ServiceName = "PosKernel";
                })
                .ConfigureServices((hostContext, services) =>
                {
                    // Core service configuration
                    services.Configure<PosKernelServiceOptions>(
                        hostContext.Configuration.GetSection("PosKernelService"));

                    // Register core services
                    services.AddSingleton<IPosKernelEngine, PosKernelEngine>();
                    services.AddSingleton<ISessionManager, SessionManager>();
                    services.AddSingleton<INamedPipeServer, NamedPipeServer>();
                    services.AddSingleton<IJsonRpcServer, JsonRpcServer>();
                    services.AddSingleton<IMetricsCollector, MetricsCollector>();
                    services.AddSingleton<IHealthChecker, HealthChecker>();

                    // Register hosted services
                    services.AddHostedService<PosKernelServiceHost>();
                    services.AddHostedService<NamedPipeServerHost>();
                    services.AddHostedService<MetricsCollectorHost>();
                    services.AddHostedService<HealthCheckerHost>();

                    // Optional JSON-RPC server for remote access
                    services.AddHostedService<JsonRpcServerHost>();
                })
                .ConfigureLogging((hostContext, logging) =>
                {
                    logging.AddConfiguration(hostContext.Configuration.GetSection("Logging"));
                    logging.AddConsole();
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        logging.AddEventLog(); // Windows Event Log
                    }
                    logging.AddSystemdConsole(); // Linux systemd journal
                });
    }

    /// <summary>
    /// Configuration options for POS Kernel Service.
    /// </summary>
    public class PosKernelServiceOptions
    {
        /// <summary>
        /// Gets or sets the service name.
        /// </summary>
        public string ServiceName { get; set; } = "PosKernel";

        /// <summary>
        /// Gets or sets the display name for the service.
        /// </summary>
        public string DisplayName { get; set; } = "POS Kernel Service";

        /// <summary>
        /// Gets or sets the service description.
        /// </summary>
        public string Description { get; set; } = "High-performance POS transaction kernel service";
        
        /// <summary>
        /// Gets or sets the named pipe configuration options.
        /// </summary>
        public NamedPipeOptions NamedPipe { get; set; } = new();

        /// <summary>
        /// Gets or sets the JSON-RPC configuration options.
        /// </summary>
        public JsonRpcOptions JsonRpc { get; set; } = new();

        /// <summary>
        /// Gets or sets the session configuration options.
        /// </summary>
        public SessionOptions Session { get; set; } = new();

        /// <summary>
        /// Gets or sets the performance configuration options.
        /// </summary>
        public PerformanceOptions Performance { get; set; } = new();

        /// <summary>
        /// Gets or sets the security configuration options.
        /// </summary>
        public SecurityOptions Security { get; set; } = new();
    }

    /// <summary>
    /// Named pipe server configuration options.
    /// </summary>
    public class NamedPipeOptions
    {
        /// <summary>
        /// Gets or sets the named pipe name.
        /// </summary>
        public string PipeName { get; set; } = "poskernel-service";

        /// <summary>
        /// Gets or sets the maximum number of server instances.
        /// </summary>
        public int MaxServerInstances { get; set; } = 254;

        /// <summary>
        /// Gets or sets the buffer size for named pipe operations.
        /// </summary>
        public int BufferSize { get; set; } = 65536;
    }

    /// <summary>
    /// JSON-RPC server configuration options.
    /// </summary>
    public class JsonRpcOptions
    {
        /// <summary>
        /// Gets or sets the port number for JSON-RPC server.
        /// </summary>
        public int Port { get; set; } = 8080;

        /// <summary>
        /// Gets or sets a value indicating whether JSON-RPC server is enabled.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Gets or sets the list of allowed host addresses.
        /// </summary>
        public string[] AllowedHosts { get; set; } = ["127.0.0.1", "localhost"];
    }

    /// <summary>
    /// Session management configuration options.
    /// </summary>
    public class SessionOptions
    {
        /// <summary>
        /// Gets or sets the session timeout in minutes.
        /// </summary>
        public int TimeoutMinutes { get; set; } = 30;

        /// <summary>
        /// Gets or sets the maximum number of concurrent sessions.
        /// </summary>
        public int MaxConcurrentSessions { get; set; } = 100;

        /// <summary>
        /// Gets or sets a value indicating whether session recovery is enabled.
        /// </summary>
        public bool EnableSessionRecovery { get; set; } = true;
    }

    /// <summary>
    /// Performance monitoring configuration options.
    /// </summary>
    public class PerformanceOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether metrics collection is enabled.
        /// </summary>
        public bool EnableMetrics { get; set; } = true;

        /// <summary>
        /// Gets or sets the metrics collection interval in seconds.
        /// </summary>
        public int MetricsInterval { get; set; } = 60;

        /// <summary>
        /// Gets or sets a value indicating whether health checks are enabled.
        /// </summary>
        public bool EnableHealthCheck { get; set; } = true;

        /// <summary>
        /// Gets or sets the health check interval in seconds.
        /// </summary>
        public int HealthCheckInterval { get; set; } = 30;
    }

    /// <summary>
    /// Security configuration options.
    /// </summary>
    public class SecurityOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether authentication is required.
        /// </summary>
        public bool RequireAuthentication { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether anonymous access is allowed.
        /// </summary>
        public bool AllowAnonymous { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether audit logging is enabled.
        /// </summary>
        public bool EnableAuditLog { get; set; } = true;
    }
}
