// Copyright 2025 Paul Moore Parks and contributors
// Licensed under the Apache License, Version 2.0
//
// Simple centralized file logger for the AI Demo UI panes. Writes logs under ~/.poskernel/logs.
// Uses PosKernelConfiguration to resolve the base directory and follows fail-fast principles
// only for directory creation. Individual write failures are non-fatal (best-effort logging).

using System.Text;
using System.Text.RegularExpressions;
using PosKernel.Configuration;

namespace PosKernel.AI.Demo.UI.Terminal
{
    public sealed class LogFileManager : IDisposable
    {
        private readonly object _sync = new();
        private string? _logsDir;

        public string LogsDirectory
        {
            get
            {
                if (_logsDir == null)
                {
                    // Resolve ~/.poskernel via central configuration helper
                    _logsDir = Path.Combine(PosKernelConfiguration.ConfigDirectory, "logs");
                }
                return _logsDir;
            }
        }

        public void EnsureLogsDirectory()
        {
            try
            {
                var dir = LogsDirectory;
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }
            catch (Exception ex)
            {
                // Fail-fast for directory creation problems – demo cannot continue writing logs
                throw new InvalidOperationException(
                    $"DESIGN DEFICIENCY: Failed to create logs directory at {LogsDirectory}. " +
                    "Logging is required for diagnostics. Ensure ~/.poskernel is writable.", ex);
            }
        }

        private static string Ts()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture);
        }

        private void AppendLine(string fileName, string line)
        {
            try
            {
                var path = Path.Combine(LogsDirectory, fileName);
                lock (_sync)
                {
                    File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch
            {
                // Best-effort: ignore write errors to avoid impacting UI
            }
        }

        private void AppendBlock(string fileName, string header, string content)
        {
            try
            {
                var path = Path.Combine(LogsDirectory, fileName);
                var sb = new StringBuilder();
                sb.AppendLine(header);
                sb.AppendLine(content);
                sb.AppendLine();
                lock (_sync)
                {
                    File.AppendAllText(path, sb.ToString(), Encoding.UTF8);
                }
            }
            catch
            {
                // Best-effort
            }
        }

        // Chat pane logging
        public void WriteChat(string line)
        {
            AppendLine("chat.log", line);
        }

        // Debug pane logging
        public void WriteDebug(string line)
        {
            AppendLine("debug.log", line);
        }

        // Prompt pane logging – keep every prompt with timestamp
        public void WritePrompt(string prompt)
        {
            var header = $"=== PROMPT [{Ts()}] ===";
            AppendBlock("prompt.log", header, prompt);
        }

        // Receipt pane logging – snapshot with timestamp
        public void WriteReceiptSnapshot(string receiptText)
        {
            var header = $"=== RECEIPT SNAPSHOT [{Ts()}] ===";
            AppendBlock("receipt.log", header, receiptText);
        }

        // Try to duplicate lines that appear to originate from kernel/extension services
        public void TryDuplicateToKernelOrExtension(string line)
        {
            // Heuristics only – no hard coupling. Only duplicates if obvious.
            // Kernel indicators
            if (line.Contains("pos-kernel-rs", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Rust kernel", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Kernel Service", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("KernelClient", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("[KERNEL]", StringComparison.OrdinalIgnoreCase))
            {
                AppendLine("kernel.log", line);
            }

            // Extension indicators – capture fully qualified extension name if present
            var match = Regex.Match(line, @"PosKernel\.Extensions\.[A-Za-z0-9_]+", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var extName = match.Value; // e.g., PosKernel.Extensions.Restaurant
                AppendLine($"{extName}.log", line);
            }
            else if (line.Contains("Restaurant Extension", StringComparison.OrdinalIgnoreCase))
            {
                AppendLine("PosKernel.Extensions.Restaurant.log", line);
            }
        }

        public void Dispose()
        {
            // No open streams in this simple implementation
        }
    }
}
