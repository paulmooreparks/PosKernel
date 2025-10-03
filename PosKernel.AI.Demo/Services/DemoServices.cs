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
using PosKernel.Abstractions.Services;
using PosKernel.AI.Services;
using PosKernel.AI.Demo.UI.Terminal;
using PosKernel.Configuration.Services;

namespace PosKernel.AI.Demo.Services;

/// <summary>
/// Custom logger provider that routes log messages to Terminal.Gui debug pane instead of console.
/// Prevents console output from corrupting the TUI display.
/// ARCHITECTURAL PRINCIPLE: UI utility class for proper console logging in Terminal.Gui
/// </summary>
internal class TerminalGuiLoggerProvider : ILoggerProvider
{
    private readonly TerminalUserInterface _terminalUi;

    public TerminalGuiLoggerProvider(TerminalUserInterface terminalUi)
    {
        _terminalUi = terminalUi ?? throw new ArgumentNullException(nameof(terminalUi));
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new TerminalGuiLogger(categoryName, _terminalUi);
    }

    public void Dispose() { }
}

/// <summary>
/// Custom logger that sends log messages to Terminal.Gui debug pane.
/// ARCHITECTURAL PRINCIPLE: UI utility class - no business logic, pure logging redirection
/// </summary>
internal class TerminalGuiLogger : ILogger
{
    private readonly string _categoryName;
    private readonly TerminalUserInterface _terminalUi;

    public TerminalGuiLogger(string categoryName, TerminalUserInterface terminalUi)
    {
        _categoryName = categoryName;
        _terminalUi = terminalUi;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel >= LogLevel.Information;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);
        var logLevelText = logLevel switch
        {
            LogLevel.Trace => "trce",
            LogLevel.Debug => "dbug",
            LogLevel.Information => "info",
            LogLevel.Warning => "warn",
            LogLevel.Error => "fail",
            LogLevel.Critical => "crit",
            _ => "unkn"
        };

        var categoryShort = _categoryName.Split('.').LastOrDefault() ?? _categoryName;
        var logEntry = $"{logLevelText}: {categoryShort}[{eventId.Id}] {message}";

        if (exception != null)
        {
            logEntry += $"\n      {exception}";
        }

        // Route to debug pane instead of console
        _terminalUi.LogDisplay?.AddLog(logEntry);
    }
}

/// <summary>
/// Simple implementation of IStoreConfigurationService for demo purposes.
/// In a real system, this would load store configuration from database or configuration files.
/// ARCHITECTURAL PRINCIPLE: Demo service - minimal implementation for UI testing
/// </summary>
internal class SimpleStoreConfigurationService : IStoreConfigurationService
{
    public StoreConfiguration GetStoreConfiguration(string storeId)
    {
        // For now, return a generic store configuration
        // In a real system, this would be looked up from a database or configuration
        return new StoreConfiguration
        {
            StoreId = storeId,
            Currency = "USD", // Default - will be overridden by StoreConfig
            Locale = "en-US"  // Default - will be overridden by StoreConfig
        };
    }
}
