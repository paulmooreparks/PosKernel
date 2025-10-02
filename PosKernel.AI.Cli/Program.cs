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

using System.Diagnostics;
using PosKernel.Configuration;
using ParksComputing.Xfer.Lang;
using ParksComputing.Xfer.Lang.Attributes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PosKernel.AI.Core;
using PosKernel.AI.Services;
using PosKernel.AI.Models;
using PosKernel.AI.Common.Abstractions;

namespace PosKernel.AI.Cli
{
    /// <summary>
    /// CLI orchestrator for all PosKernel services.
    /// Manages Rust kernel, restaurant extension, AI chat service.
    /// </summary>
    internal class Program
    {
        private static readonly string LogsDirectory = Path.Combine(PosKernelConfiguration.ConfigDirectory, "logs");
        private static readonly string ConfigFilePath = Path.Combine(PosKernelConfiguration.ConfigDirectory, "services.xfer");
        private static Dictionary<string, ServiceInfo> Services = new();
        private static ServiceConfiguration? LoadedConfiguration = null;
        private static string? InvocationCommand = null;

        static async Task<int> Main(string[] args)
        {
            try
            {
                // Capture how this application was invoked for help output
                CaptureInvocationCommand();

                // Load configuration from XferLang file
                await LoadConfigurationAsync();

                if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
                {
                    ShowHelp();
                    return 0;
                }

                var command = args[0].ToLowerInvariant();

                return command switch
                {
                    "start" => await StartServicesAsync(args.Skip(1).ToArray()),
                    "stop" => await StopServicesAsync(args.Skip(1).ToArray()),
                    "restart" => await RestartServicesAsync(args.Skip(1).ToArray()),
                    "status" => await CheckServicesStatusAsync(args.Skip(1).ToArray()),
                    "send" => await SendPromptAsync(args.Skip(1)),
                    "logs" => await ShowLogsAsync(args.Skip(1).ToArray()),
                    "kill" => await KillAllServicesAsync(), // Emergency stop
                    "help" => ShowHelpAndReturn(), // Add explicit help command
                    _ => await SendPromptAsync(args) // Default: treat all args as a prompt
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                return 1;
            }
        }

        /// <summary>
        /// Load service configuration from XferLang file.
        /// </summary>
        private static async Task LoadConfigurationAsync()
        {
            try
            {
                if (!File.Exists(ConfigFilePath))
                {
                    Console.WriteLine($"⚠️  Configuration file not found: {ConfigFilePath}");
                    Console.WriteLine("📁 Creating default configuration...");
                    await CreateDefaultConfigurationAsync();
                }

                var xferContent = await File.ReadAllTextAsync(ConfigFilePath);

                // Use XferConvert to deserialize directly to C# objects
                var config = XferConvert.Deserialize<ServiceConfiguration>(xferContent);
                LoadedConfiguration = config; // Store for service ordering

                if (config?.Services == null) {
                    throw new InvalidOperationException(
                        "DESIGN DEFICIENCY: Configuration file missing 'services' section. " +
                        $"Check {ConfigFilePath} for proper service definitions.");
                }

                // Convert to ServiceInfo dictionary
                Services.Clear();
                foreach (var service in config.Services)
                {
                    if (service.Enabled) // Only load enabled services
                    {
                        Services[service.Name] = new ServiceInfo
                        {
                            Name = service.Name,
                            DisplayName = service.DisplayName,
                            Enabled = service.Enabled,
                            WorkingDirectory = service.WorkingDirectory,
                            Command = service.Command,
                            Arguments = service.Arguments,
                            LogFile = service.LogFile,
                            PidFile = service.PidFile,
                            DependsOn = service.DependsOn?.ToList() ?? new List<string>(),
                            StartupTimeout = service.StartupTimeout,
                            HealthCheck = service.HealthCheck != null ? new HealthCheckConfig
                            {
                                Enabled = service.HealthCheck.Enabled,
                                Port = service.HealthCheck.Port,
                                Path = service.HealthCheck.Path,
                                Interval = service.HealthCheck.Interval
                            } : null
                        };
                    }
                }

                Console.WriteLine($"✅ Loaded configuration for {Services.Count} enabled services");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"DESIGN DEFICIENCY: Failed to load configuration from {ConfigFilePath}. " +
                    $"Check file syntax and structure. Error: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Create default configuration file if it doesn't exist.
        /// </summary>
        private static async Task CreateDefaultConfigurationAsync()
        {
            var defaultConfig = @"<! document {
    version ""1.0""
    author ""PosKernel CLI Service Orchestrator""
    description ""Service configuration for PosKernel components""
    created @2025-10-02T00:00:00Z@
} !>

<! dynamicSource {
    posKernelRoot env ""POSKERNEL_ROOT""
    workspaceRoot const ""c:\Users\paul\source\repos\PosKernel""
    logsDir const ""<|USER_PROFILE|>\.poskernel\logs""
    pidDir const ""<|USER_PROFILE|>\.poskernel\pids""
} !>

{
    </ Service definitions in dependency order />
    services {
        rust {
            name ""Rust POS Kernel""
            enabled ~true
            workingDirectory <""pos-kernel-rs"">
            command <""cargo"">
            arguments <""run --bin pos-kernel-service"">
            logFile <""rust-kernel.log"">
            pidFile <""rust-kernel.pid"">
            useTeeLogging ~true
            dependsOn [ ]
            startupTimeout 10
            healthCheck {
                enabled ~true
                port 3030
                path ""/health""
                interval 5
            }
        }

        restaurant {
            name ""Restaurant Extension Service""
            enabled ~true
            workingDirectory <"""">
            command <""dotnet"">
            arguments <""run --project PosKernel.Extensions.Restaurant -- --store-type Kopitiam --store-name \""Uncle Tan's Kopitiam\"" --currency SGD"">
            logFile <""restaurant-extension.log"">
            pidFile <""restaurant-extension.pid"">
            useTeeLogging ~false
            dependsOn [ ""rust"" ]
            startupTimeout 15
            healthCheck {
                enabled ~false
            }
        }

        ai {
            name ""AI Chat Service""
            enabled ~false
            workingDirectory <"""">
            command <""dotnet"">
            arguments <""run --project PosKernel.AI.Demo"">
            logFile <""ai-chat.log"">
            pidFile <""ai-chat.pid"">
            useTeeLogging ~false
            dependsOn [ ""rust"" ""restaurant"" ]
            startupTimeout 20
            healthCheck {
                enabled ~false
            }
        }
    }

    </ Global configuration options />
    configuration {
        defaultTimeout 30
        debugMode ~false
        logLevel ""info""
        retryAttempts 3
        retryDelay 2

        </ Paths configuration />
        paths {
            workspaceRoot '<|workspaceRoot|>'
            logsDirectory '<|logsDir|>'
            pidDirectory '<|pidDir|>'
        }

        </ Environment-specific overrides />
        development {
            debugMode ~true
            logLevel ""debug""
            startupTimeout 60
        }

        production {
            debugMode ~false
            logLevel ""info""
            retryAttempts 5
        }
    }

    </ Service group definitions for bulk operations />
    serviceGroups {
        core [ ""rust"" ]
        backend [ ""rust"" ""restaurant"" ]
        all [ ""rust"" ""restaurant"" ""ai"" ]
        ai-stack [ ""restaurant"" ""ai"" ]
    }
}";

            // Ensure directory exists
            var configDir = Path.GetDirectoryName(ConfigFilePath);
            if (!string.IsNullOrEmpty(configDir) && !Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
            }

            await File.WriteAllTextAsync(ConfigFilePath, defaultConfig);
            Console.WriteLine($"✅ Created default configuration: {ConfigFilePath}");
        }

        private static async Task<int> StartServicesAsync(string[] args)
        {
            Console.WriteLine("🚀 Starting PosKernel services...");

            // Parse options
            var force = args.Contains("--force") || args.Contains("-f");
            var debug = args.Contains("--debug") || args.Contains("-d");
            var servicesToStart = ParseServicesList(args);

            // Ensure logs directory exists
            Directory.CreateDirectory(LogsDirectory);

            Console.WriteLine($"📋 Services to start: {string.Join(", ", servicesToStart)}");
            if (debug) Console.WriteLine("🔧 Debug mode enabled");
            Console.WriteLine();

            var startedCount = 0;

            // Start services in dependency order
            var orderedServices = GetServicesInDependencyOrder(servicesToStart);

            foreach (var serviceKey in orderedServices)
            {
                var service = Services[serviceKey];

                if (await IsServiceRunningAsync(serviceKey))
                {
                    Console.WriteLine($"⚠️  {service.DisplayName} is already running");
                    continue;
                }

                Console.WriteLine($"🔄 Starting {service.DisplayName}...");

                if (await StartSingleServiceAsync(serviceKey, service, debug))
                {
                    startedCount++;
                    Console.WriteLine($"✅ {service.Name} started");

                    // Give services time to start up before starting dependents
                    if (serviceKey == "rust") await Task.Delay(3000);
                    if (serviceKey == "restaurant") await Task.Delay(2000);
                }
                else
                {
                    Console.WriteLine($"❌ Failed to start {service.Name}");
                    break;
                }
            }

            Console.WriteLine();
            Console.WriteLine($"✅ Started {startedCount} services successfully");
            Console.WriteLine("💡 Use 'send \"your prompt\"' to interact with the AI service");
            Console.WriteLine("🔍 Use 'logs [service]' to view service logs");
            Console.WriteLine("🛑 Use 'stop' to stop all services");

            return startedCount > 0 ? 0 : 1;
        }

        private static async Task<int> StopServicesAsync(string[] args)
        {
            Console.WriteLine("🛑 Stopping PosKernel services...");

            var force = args.Contains("--force") || args.Contains("-f");
            var servicesToStop = ParseServicesList(args);

            var stoppedCount = 0;

            // Stop services in shutdown order
            var orderedServices = GetServicesInShutdownOrder(servicesToStop);

            foreach (var serviceKey in orderedServices)
            {
                var service = Services[serviceKey];

                if (!await IsServiceRunningAsync(serviceKey))
                {
                    Console.WriteLine($"ℹ️  {service.DisplayName} is not running");
                    continue;
                }

                Console.WriteLine($"🔄 Stopping {service.DisplayName}...");

                if (await StopSingleServiceAsync(serviceKey, force))
                {
                    stoppedCount++;
                    Console.WriteLine($"✅ {service.DisplayName} stopped");
                }
                else
                {
                    Console.WriteLine($"❌ Failed to stop {service.DisplayName}");
                }
            }

            Console.WriteLine($"🔄 Stopped {stoppedCount} services");
            return 0;
        }

        private static async Task<int> RestartServicesAsync(string[] args)
        {
            Console.WriteLine("🔄 Restarting PosKernel services...");

            await StopServicesAsync(args);
            await Task.Delay(2000); // Give services time to fully stop
            return await StartServicesAsync(args);
        }

        private static async Task<int> CheckServicesStatusAsync(string[] args)
        {
            var servicesToCheck = ParseServicesList(args);
            var runningCount = 0;

            Console.WriteLine("📊 PosKernel Services Status:");
            Console.WriteLine();

            foreach (var serviceKey in servicesToCheck)
            {
                var service = Services[serviceKey];
                var isRunning = await IsServiceRunningAsync(serviceKey);
                var status = isRunning ? "✅ Running" : "❌ Stopped";

                if (isRunning)
                {
                    var pid = await GetServicePidAsync(serviceKey);
                    status += $" (PID: {pid})";
                    runningCount++;
                }

                Console.WriteLine($"  {service.Name} ({service.DisplayName}): {status}");
            }

            Console.WriteLine();
            Console.WriteLine($"📈 {runningCount}/{servicesToCheck.Count} services running");

            return runningCount == servicesToCheck.Count ? 0 : 1;
        }

        private static async Task<int> KillAllServicesAsync()
        {
            Console.WriteLine("� Emergency kill - forcibly stopping all PosKernel services...");

            var killedCount = 0;
            foreach (var serviceKey in Services.Keys)
            {
                if (await StopSingleServiceAsync(serviceKey, force: true))
                {
                    killedCount++;
                    Console.WriteLine($"💀 Killed {Services[serviceKey].Name}");
                }
            }

            Console.WriteLine($"💀 Killed {killedCount} services");
            return 0;
        }

        private static async Task<int> SendPromptAsync(IEnumerable<string> promptArgs)
        {
            var prompt = string.Join(" ", promptArgs);
            if (string.IsNullOrWhiteSpace(prompt))
            {
                Console.WriteLine("❌ No prompt provided");
                ShowHelp();
                return 1;
            }

            Console.WriteLine($"💬 Processing: {prompt}");

            try
            {
                // ARCHITECTURAL PRINCIPLE: CLI directly integrates AI business logic
                // No HTTP calls to separate services - AI logic stays in PosKernel.AI

                var startTime = DateTime.UtcNow;

                // Initialize AI services directly
                var response = await ProcessAiRequestAsync(prompt);

                var processingTime = DateTime.UtcNow - startTime;

                Console.WriteLine($"🤖 AI Response: {response}");
                Console.WriteLine($"⏱️  Processing time: {processingTime.TotalMilliseconds:F0}ms");

                // Log the interaction to ai-service.log for debugging
                await LogAiInteractionAsync(prompt, response, processingTime);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ AI processing error: {ex.Message}");
                Console.WriteLine("💡 Check that Restaurant Extension is running and AI configuration is correct");
                return 1;
            }

            return 0;
        }

        private static async Task<string> ProcessAiRequestAsync(string prompt)
        {
            // ARCHITECTURAL PRINCIPLE: Use the same AI setup as PosKernel.AI.Demo but in CLI
            // This ensures consistent behavior across all AI interfaces

            var config = PosKernelConfiguration.Initialize();

            // Create minimal service collection for AI
            var services = new ServiceCollection();

            // Add logging
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            // Configure store using StoreConfigFactory (same as Demo)
            var storeConfig = StoreConfigFactory.CreateStore(StoreType.Kopitiam, useRealKernel: true);

            // Create personality using factory (same as Demo)
            var personality = AiPersonalityFactory.CreatePersonality(PersonalityType.SingaporeanKopitiamUncle, storeConfig);

            // Configure AI services using the same pattern as Demo
            await services.ConfigurePosAiServicesAsync(storeConfig, personality, false);

            using var serviceProvider = services.BuildServiceProvider();

            // Get AI agent and process the request
            var aiAgent = serviceProvider.GetRequiredService<PosAiAgent>();

            // Use the correct method name from PosAiAgent
            return await aiAgent.ProcessCustomerInputAsync(prompt);
        }

        private static async Task LogAiInteractionAsync(string prompt, string response, TimeSpan processingTime)
        {
            try
            {
                var logFile = Path.Combine(LogsDirectory, "ai-service.log");
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var logEntry = $"[{timestamp}] PROMPT: {prompt}\n" +
                              $"[{timestamp}] RESPONSE: {response}\n" +
                              $"[{timestamp}] PROCESSING_TIME: {processingTime.TotalMilliseconds:F0}ms\n" +
                              $"[{timestamp}] ---\n";

                await File.AppendAllTextAsync(logFile, logEntry);
            }
            catch
            {
                // Fail silently for logging issues
            }
        }

        // Helper methods for service management
        private static List<string> ParseServicesList(string[] args)
        {
            var services = new List<string>();
            var serviceArgs = args.Where(arg => !arg.StartsWith("--") && !arg.StartsWith("-") && Services.ContainsKey(arg)).ToArray();

            if (serviceArgs.Any())
            {
                services.AddRange(serviceArgs);
            }
            else
            {
                // Default to all services if none specified
                services.AddRange(Services.Keys);
            }

            return services;
        }

        private static List<string> GetServicesInDependencyOrder(List<string> servicesToOrder)
        {
            // First, try to use the configured service order from the configuration
            if (LoadedConfiguration?.Configuration?.ServiceOrder?.Startup != null)
            {
                var configuredOrder = LoadedConfiguration.Configuration.ServiceOrder.Startup
                    .Where(servicesToOrder.Contains)
                    .ToList();

                // Add any services not in the configured order at the end
                var missingServices = servicesToOrder.Except(configuredOrder).ToList();
                configuredOrder.AddRange(missingServices);

                return configuredOrder;
            }

            // Fallback: Use dependency-based ordering
            var ordered = new List<string>();
            var visited = new HashSet<string>();
            var visiting = new HashSet<string>();

            foreach (var service in servicesToOrder)
            {
                if (!visited.Contains(service))
                {
                    VisitService(service, servicesToOrder, visited, visiting, ordered);
                }
            }

            return ordered;
        }

        private static void VisitService(string serviceKey, List<string> allServices, HashSet<string> visited, HashSet<string> visiting, List<string> ordered)
        {
            if (visiting.Contains(serviceKey))
            {
                throw new InvalidOperationException($"Circular dependency detected involving service: {serviceKey}");
            }

            if (visited.Contains(serviceKey))
            {
                return;
            }

            visiting.Add(serviceKey);

            // Visit dependencies first
            if (Services.ContainsKey(serviceKey))
            {
                var service = Services[serviceKey];
                foreach (var dependency in service.DependsOn)
                {
                    if (allServices.Contains(dependency))
                    {
                        VisitService(dependency, allServices, visited, visiting, ordered);
                    }
                }
            }

            visiting.Remove(serviceKey);
            visited.Add(serviceKey);
            ordered.Add(serviceKey);
        }

        private static List<string> GetServicesInShutdownOrder(List<string> servicesToOrder)
        {
            // First, try to use the configured shutdown order from the configuration
            if (LoadedConfiguration?.Configuration?.ServiceOrder?.Shutdown != null)
            {
                var configuredOrder = LoadedConfiguration.Configuration.ServiceOrder.Shutdown
                    .Where(servicesToOrder.Contains)
                    .ToList();

                // Add any services not in the configured order at the end
                var missingServices = servicesToOrder.Except(configuredOrder).ToList();
                configuredOrder.AddRange(missingServices);

                return configuredOrder;
            }

            // Fallback: Use reverse of startup order
            return GetServicesInDependencyOrder(servicesToOrder).AsEnumerable().Reverse().ToList();
        }

        private static async Task<bool> StartSingleServiceAsync(string serviceKey, ServiceInfo service, bool debug)
        {
            try
            {
                // ARCHITECTURAL PRINCIPLE: Use configured workspace root instead of runtime discovery
                var rootDir = LoadedConfiguration?.Configuration?.Paths?.WorkspaceRoot
                    ?? Directory.GetCurrentDirectory();
                var workingDir = string.IsNullOrEmpty(service.WorkingDirectory)
                    ? rootDir
                    : Path.Combine(rootDir, service.WorkingDirectory);

                var arguments = service.Arguments;
                if (debug && serviceKey == "ai")
                {
                    arguments += " --debug";
                }

                // ARCHITECTURAL PRINCIPLE: Use consistent cross-platform .NET logging for all services
                // No PowerShell or platform-specific dependencies
                var startInfo = new ProcessStartInfo
                {
                    FileName = service.Command,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = workingDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                var process = Process.Start(startInfo);
                if (process == null)
                {
                    return false;
                }

                // Save PID for management
                await SaveServicePidAsync(serviceKey, process.Id);

                // Set up cross-platform logging for all services
                var logPath = Path.Combine(LogsDirectory, service.LogFile);

                // Ensure logs directory exists
                Directory.CreateDirectory(LogsDirectory);

                // Start logging tasks for both stdout and stderr
                _ = Task.Run(() => LogProcessOutputAsync(process.StandardOutput, logPath, "OUT"));
                _ = Task.Run(() => LogProcessOutputAsync(process.StandardError, logPath, "ERR"));

                // Give the process a moment to start
                await Task.Delay(500);

                return !process.HasExited;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error starting {service.Name}: {ex.Message}");
                return false;
            }
        }

        private static async Task<bool> StopSingleServiceAsync(string serviceKey, bool force)
        {
            try
            {
                var pid = await GetServicePidAsync(serviceKey);
                if (pid == null)
                {
                    return true; // Already stopped
                }

                try
                {
                    var process = Process.GetProcessById(pid.Value);
                    if (process.HasExited)
                    {
                        ClearServicePidAsync(serviceKey);
                        return true;
                    }

                    if (force)
                    {
                        process.Kill();
                        process.WaitForExit();
                    }
                    else
                    {
                        // Try graceful shutdown first
                        if (!process.CloseMainWindow())
                        {
                            process.Kill();
                        }

                        if (!process.WaitForExit(5000))
                        {
                            process.Kill();
                            process.WaitForExit();
                        }
                    }

                    ClearServicePidAsync(serviceKey);
                    return true;
                }
                catch (ArgumentException)
                {
                    // Process not found, clear PID file
                    ClearServicePidAsync(serviceKey);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error stopping {Services[serviceKey].Name}: {ex.Message}");
                return false;
            }
        }

        private static async Task<bool> IsServiceRunningAsync(string serviceKey)
        {
            var pid = await GetServicePidAsync(serviceKey);
            if (pid == null) return false;

            try
            {
                var process = Process.GetProcessById(pid.Value);
                return !process.HasExited;
            }
            catch
            {
                return false;
            }
        }

        private static async Task<int?> GetServicePidAsync(string serviceKey)
        {
            var pidFile = Path.Combine(LogsDirectory, Services[serviceKey].PidFile);
            if (!File.Exists(pidFile))
            {
                return null;
            }

            var pidText = await File.ReadAllTextAsync(pidFile);
            if (int.TryParse(pidText.Trim(), out var pid))
            {
                return pid;
            }

            return null;
        }

        private static async Task SaveServicePidAsync(string serviceKey, int pid)
        {
            var pidFile = Path.Combine(LogsDirectory, Services[serviceKey].PidFile);
            Directory.CreateDirectory(Path.GetDirectoryName(pidFile)!);
            await File.WriteAllTextAsync(pidFile, pid.ToString());
        }

        private static void ClearServicePidAsync(string serviceKey)
        {
            var pidFile = Path.Combine(LogsDirectory, Services[serviceKey].PidFile);
            if (File.Exists(pidFile))
            {
                File.Delete(pidFile);
            }
        }

        private static async Task LogProcessOutputAsync(StreamReader reader, string logPath, string streamType = "OUT")
        {
            try
            {
                // Ensure the log directory exists
                var logDir = Path.GetDirectoryName(logPath);
                if (!string.IsNullOrEmpty(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }

                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    var logLine = $"[{timestamp}] [{streamType}] {line}";

                    // Use async file writing with proper error handling
                    try
                    {
                        await File.AppendAllTextAsync(logPath, logLine + Environment.NewLine);
                    }
                    catch (Exception ex)
                    {
                        // If we can't write to the log file, at least try to output to console
                        Console.WriteLine($"[LOG_ERROR] Failed to write to {logPath}: {ex.Message}");
                        Console.WriteLine($"[LOG_FALLBACK] {logLine}");
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the logging error but don't crash the service
                Console.WriteLine($"[LOG_ERROR] LogProcessOutputAsync failed for {logPath}: {ex.Message}");
            }
        }

        private static async Task<int> ShowLogsAsync(string[] args)
        {
            if (!Directory.Exists(LogsDirectory))
            {
                Console.WriteLine($"❌ Logs directory not found: {LogsDirectory}");
                return 1;
            }

            var logType = args.Length > 0 ? args[0] : "debug";
            string logFile;

            // Check if it's a service-specific log
            if (Services.ContainsKey(logType))
            {
                logFile = Path.Combine(LogsDirectory, Services[logType].LogFile);
            }
            else
            {
                // Default logs (debug, chat, receipt, etc.)
                logFile = Path.Combine(LogsDirectory, $"{logType}.log");
            }

            if (!File.Exists(logFile))
            {
                Console.WriteLine($"❌ Log file not found: {logFile}");
                Console.WriteLine($"💡 Available logs: {string.Join(", ", Directory.GetFiles(LogsDirectory, "*.log").Select(Path.GetFileNameWithoutExtension))}");
                return 1;
            }

            Console.WriteLine($"📜 Showing {logType} logs:");
            Console.WriteLine(new string('=', 50));

            // Show last 50 lines by default
            var lines = await File.ReadAllLinesAsync(logFile);
            var startIndex = Math.Max(0, lines.Length - 50);

            for (int i = startIndex; i < lines.Length; i++)
            {
                Console.WriteLine(lines[i]);
            }

            return 0;
        }



        private static void CaptureInvocationCommand()
        {
            try
            {
                var commandLine = Environment.CommandLine;

                // First priority: Check if we were actually invoked via 'dotnet run'
                if (commandLine.Contains("dotnet") && commandLine.Contains("run") &&
                    !commandLine.ToLowerInvariant().Contains("poskernel"))
                {
                    InvocationCommand = "dotnet run --project PosKernel.AI.Cli";
                    return;
                }

                // Second priority: Check if poskernel is available (wrapper exists)
                if (IsInvokedViaWrapper())
                {
                    InvocationCommand = "poskernel";
                    return;
                }

                // Final fallback
                InvocationCommand = "dotnet run --project PosKernel.AI.Cli";
            }
            catch
            {
                InvocationCommand = "dotnet run --project PosKernel.AI.Cli";
            }
        }

        private static bool IsInvokedViaWrapper()
        {
            try
            {
                // Check if poskernel.ps1 is in the call stack by looking at environment
                var commandLine = Environment.CommandLine;

                // Check if we can find poskernel in the current working directory or PATH
                var currentDir = Directory.GetCurrentDirectory();
                var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? Array.Empty<string>();

                // Look for poskernel script in current directory or PATH
                foreach (var dir in new[] { currentDir }.Concat(pathDirs))
                {
                    if (string.IsNullOrEmpty(dir)) continue;

                    var possiblePaths = new[]
                    {
                        Path.Combine(dir, "poskernel.ps1"),
                        Path.Combine(dir, "poskernel.cmd"),
                        Path.Combine(dir, "poskernel.bat"),
                        Path.Combine(dir, "poskernel")
                    };

                    if (possiblePaths.Any(File.Exists))
                    {
                        return true;
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private static void ShowHelp()
        {
            var cmd = InvocationCommand ?? "dotnet run --project PosKernel.AI.Cli";

            Console.WriteLine("PosKernel Service Orchestrator CLI");
            Console.WriteLine();
            Console.WriteLine("USAGE:");
            Console.WriteLine($"  {cmd} <command> [services] [options]");
            Console.WriteLine();
            Console.WriteLine("COMMANDS:");
            Console.WriteLine("  start [services]       Start PosKernel services (default: all)");
            Console.WriteLine("  stop [services]        Stop PosKernel services (default: all)");
            Console.WriteLine("  restart [services]     Restart PosKernel services (default: all)");
            Console.WriteLine("  status [services]      Check service status (default: all)");
            Console.WriteLine("  send <prompt>          Send prompt to AI Chat Service");
            Console.WriteLine("  logs [service|type]    Show service logs (default: debug)");
            Console.WriteLine("  kill                   Emergency stop all services");
            Console.WriteLine("  <prompt>               Send prompt directly (default action)");
            Console.WriteLine();
            Console.WriteLine("SERVICES:");
            foreach (var service in Services.Values.OrderBy(s => s.Name))
            {
                var status = service.Enabled ? "" : " (disabled)";
                Console.WriteLine($"  {service.Name.PadRight(20)} {service.DisplayName}{status}");
            }
            Console.WriteLine();
            Console.WriteLine("OPTIONS:");
            Console.WriteLine("  --debug, -d            Enable debug logging");
            Console.WriteLine("  --force, -f            Force stop/restart services");
            Console.WriteLine();
            Console.WriteLine("EXAMPLES:");
            Console.WriteLine($"  {cmd} start");
            Console.WriteLine($"  {cmd} start rust restaurant --debug");
            Console.WriteLine($"  {cmd} send \"kopi c kosong\"");
            Console.WriteLine($"  {cmd} \"teh tarik, one sugar\"");
            Console.WriteLine($"  {cmd} stop --force");
            Console.WriteLine($"  {cmd} logs rust");
            Console.WriteLine($"  {cmd} logs ai");
            Console.WriteLine($"  {cmd} status");
            Console.WriteLine();
            Console.WriteLine("WORKFLOW:");
            Console.WriteLine("  1. Start all services: 'start --debug'");
            Console.WriteLine("  2. Send test prompts: 'send \"your order\"'");
            Console.WriteLine("  3. Check logs: 'logs ai' or 'logs restaurant'");
            Console.WriteLine("  4. Stop when done: 'stop'");
            Console.WriteLine();
            Console.WriteLine("LOGS:");
            Console.WriteLine("  All logs are written to ~/.poskernel/logs/");
            Console.WriteLine("  Service logs: rust-kernel.log, restaurant-extension.log, ai-chat.log");
            Console.WriteLine("  System logs: debug.log, chat.log, receipt.log");
        }

        private static int ShowHelpAndReturn()
        {
            ShowHelp();
            return 0;
        }
    }

    /// <summary>
    /// Information about a PosKernel service for orchestration.
    /// </summary>
    public class ServiceInfo
    {
        [XferProperty("name")]
        public string Name { get; set; } = "";

        [XferProperty("displayName")]
        public string DisplayName { get; set; } = "";

        [XferProperty("workingDirectory")]
        public string WorkingDirectory { get; set; } = "";

        [XferProperty("command")]
        public string Command { get; set; } = "";

        [XferProperty("arguments")]
        public string Arguments { get; set; } = "";

        [XferProperty("logFile")]
        public string LogFile { get; set; } = "";

        [XferProperty("pidFile")]
        public string PidFile { get; set; } = "";

        [XferProperty("enabled")]
        public bool Enabled { get; set; } = true;

        [XferProperty("dependsOn")]
        public List<string> DependsOn { get; set; } = new();

        [XferProperty("startupTimeout")]
        public int StartupTimeout { get; set; } = 30;

        [XferProperty("healthCheck")]
        public HealthCheckConfig? HealthCheck { get; set; }
    }

    /// <summary>
    /// Health check configuration for services.
    /// </summary>
    public class HealthCheckConfig
    {
        [XferProperty("enabled")]
        public bool Enabled { get; set; } = false;

        [XferProperty("port")]
        public int Port { get; set; } = 0;

        [XferProperty("path")]
        public string Path { get; set; } = "/health";

        [XferProperty("interval")]
        public int Interval { get; set; } = 5;
    }

    /// <summary>
    /// Top-level configuration structure for XferLang deserialization.
    /// </summary>
    public class ServiceConfiguration
    {
        [XferProperty("services")]
        public Service[] Services { get; set; } = Array.Empty<Service>();

        [XferProperty("configuration")]
        public Configuration? Configuration { get; set; }

        [XferProperty("serviceGroups")]
        public Dictionary<string, string[]>? ServiceGroups { get; set; }
    }

    /// <summary>
    /// Service configuration for XferLang deserialization.
    /// </summary>
    public class Service
    {
        [XferProperty("name")]
        public string Name { get; set; } = "";

        [XferProperty("displayName")]
        public string DisplayName { get; set; } = "";

        [XferProperty("enabled")]
        public bool Enabled { get; set; } = true;

        [XferProperty("workingDirectory")]
        public string WorkingDirectory { get; set; } = "";

        [XferProperty("command")]
        public string Command { get; set; } = "";

        [XferProperty("arguments")]
        public string Arguments { get; set; } = "";

        [XferProperty("logFile")]
        public string LogFile { get; set; } = "";

        [XferProperty("pidFile")]
        public string PidFile { get; set; } = "";

        [XferProperty("dependsOn")]
        public string[] DependsOn { get; set; } = Array.Empty<string>();

        [XferProperty("startupTimeout")]
        public int StartupTimeout { get; set; } = 30;

        [XferProperty("healthCheck")]
        public HealthCheck? HealthCheck { get; set; }
    }

    /// <summary>
    /// Health check configuration for XferLang deserialization.
    /// </summary>
    public class HealthCheck
    {
        [XferProperty("enabled")]
        public bool Enabled { get; set; } = false;

        [XferProperty("port")]
        public int Port { get; set; } = 0;

        [XferProperty("path")]
        public string Path { get; set; } = "/health";

        [XferProperty("interval")]
        public int Interval { get; set; } = 5;
    }

    /// <summary>
    /// Global configuration for XferLang deserialization.
    /// </summary>
    public class Configuration
    {
        [XferProperty("defaultTimeout")]
        public int DefaultTimeout { get; set; } = 30;

        [XferProperty("debugMode")]
        public bool DebugMode { get; set; } = false;

        [XferProperty("logLevel")]
        public string LogLevel { get; set; } = "info";

        [XferProperty("retryAttempts")]
        public int RetryAttempts { get; set; } = 3;

        [XferProperty("retryDelay")]
        public int RetryDelay { get; set; } = 2;

        [XferProperty("serviceOrder")]
        public ServiceOrder? ServiceOrder { get; set; }

        [XferProperty("paths")]
        public Paths? Paths { get; set; }

        [XferProperty("development")]
        public EnvironmentConfiguration? Development { get; set; }

        [XferProperty("production")]
        public EnvironmentConfiguration? Production { get; set; }
    }

    /// <summary>
    /// Service startup and shutdown order configuration.
    /// </summary>
    public class ServiceOrder
    {
        [XferProperty("startup")]
        public string[] Startup { get; set; } = Array.Empty<string>();

        [XferProperty("shutdown")]
        public string[] Shutdown { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// Path configuration for XferLang deserialization.
    /// </summary>
    public class Paths
    {
        [XferProperty("workspaceRoot")]
        public string WorkspaceRoot { get; set; } = "";

        [XferProperty("logsDirectory")]
        public string LogsDirectory { get; set; } = "";

        [XferProperty("pidDirectory")]
        public string PidDirectory { get; set; } = "";
    }

    /// <summary>
    /// Environment-specific configuration for development/production.
    /// </summary>
    public class EnvironmentConfiguration
    {
        [XferProperty("debugMode")]
        public bool DebugMode { get; set; } = false;

        [XferProperty("logLevel")]
        public string LogLevel { get; set; } = "info";

        [XferProperty("retryAttempts")]
        public int RetryAttempts { get; set; } = 3;

        [XferProperty("startupTimeout")]
        public int StartupTimeout { get; set; } = 30;
    }

    /// <summary>
    /// Response from AI chat service HTTP API.
    /// </summary>
    public class ChatResponse
    {
        [JsonPropertyName("response")]
        public string Response { get; set; } = "";

        [JsonPropertyName("processingTime")]
        public string? ProcessingTime { get; set; }

        [JsonPropertyName("success")]
        public bool Success { get; set; } = true;

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }
}
