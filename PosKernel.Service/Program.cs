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
                    logging.AddEventLog(); // Windows Event Log
                    logging.AddSystemdConsole(); // Linux systemd journal
                });
    }

    /// <summary>
    /// Configuration options for POS Kernel Service.
    /// </summary>
    public class PosKernelServiceOptions
    {
        public string ServiceName { get; set; } = "PosKernel";
        public string DisplayName { get; set; } = "POS Kernel Service";
        public string Description { get; set; } = "High-performance POS transaction kernel service";
        
        public NamedPipeOptions NamedPipe { get; set; } = new();
        public JsonRpcOptions JsonRpc { get; set; } = new();
        public SessionOptions Session { get; set; } = new();
        public PerformanceOptions Performance { get; set; } = new();
        public SecurityOptions Security { get; set; } = new();
    }

    public class NamedPipeOptions
    {
        public string PipeName { get; set; } = "poskernel-service";
        public int MaxServerInstances { get; set; } = 254;
        public int BufferSize { get; set; } = 65536;
    }

    public class JsonRpcOptions
    {
        public int Port { get; set; } = 8080;
        public bool Enabled { get; set; } = false;
        public string[] AllowedHosts { get; set; } = ["127.0.0.1", "localhost"];
    }

    public class SessionOptions
    {
        public int TimeoutMinutes { get; set; } = 30;
        public int MaxConcurrentSessions { get; set; } = 100;
        public bool EnableSessionRecovery { get; set; } = true;
    }

    public class PerformanceOptions
    {
        public bool EnableMetrics { get; set; } = true;
        public int MetricsInterval { get; set; } = 60;
        public bool EnableHealthCheck { get; set; } = true;
        public int HealthCheckInterval { get; set; } = 30;
    }

    public class SecurityOptions
    {
        public bool RequireAuthentication { get; set; } = false;
        public bool AllowAnonymous { get; set; } = true;
        public bool EnableAuditLog { get; set; } = true;
    }
}
