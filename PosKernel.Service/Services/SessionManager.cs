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
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace PosKernel.Service.Services
{
    /// <summary>
    /// Session manager implementation for multi-terminal support.
    /// Handles session lifecycle, timeout management, and cleanup.
    /// </summary>
    public class SessionManager : ISessionManager, IDisposable
    {
        private readonly ILogger<SessionManager> _logger;
        private readonly PosKernelServiceOptions _options;
        private readonly ConcurrentDictionary<string, SessionInfo> _sessions;
        private readonly Timer _cleanupTimer;
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="SessionManager"/> class.
        /// </summary>
        /// <param name="logger">Logger for diagnostics and debugging.</param>
        /// <param name="options">Configuration options for the session manager.</param>
        public SessionManager(ILogger<SessionManager> logger, IOptions<PosKernelServiceOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _sessions = new ConcurrentDictionary<string, SessionInfo>();

            // Start cleanup timer for expired sessions
            _cleanupTimer = new Timer(CleanupExpiredSessionsCallback, null, 
                TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

            _logger.LogInformation("Session manager initialized with timeout {TimeoutMinutes} minutes, max sessions {MaxSessions}",
                _options.Session.TimeoutMinutes, _options.Session.MaxConcurrentSessions);
        }

        /// <summary>
        /// Creates a new session for the specified terminal and operator.
        /// </summary>
        /// <param name="terminalId">The terminal identifier.</param>
        /// <param name="operatorId">The operator identifier.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The created session information.</returns>
        public Task<SessionInfo> CreateSessionAsync(string terminalId, string operatorId, CancellationToken cancellationToken = default)
        {
            try
            {
                // Check max concurrent sessions
                var activeSessions = _sessions.Count(s => s.Value.IsActive);
                if (activeSessions >= _options.Session.MaxConcurrentSessions)
                {
                    throw new InvalidOperationException($"Maximum concurrent sessions reached: {_options.Session.MaxConcurrentSessions}");
                }

                // Generate unique session ID
                var sessionId = GenerateSessionId();
                var now = DateTime.UtcNow;

                var sessionInfo = new SessionInfo
                {
                    SessionId = sessionId,
                    TerminalId = terminalId,
                    OperatorId = operatorId,
                    CreatedAt = now,
                    LastActivity = now,
                    IsActive = true
                };

                _sessions.TryAdd(sessionId, sessionInfo);

                _logger.LogInformation("Session created: {SessionId} for terminal {TerminalId}, operator {OperatorId}",
                    sessionId, terminalId, operatorId);

                return Task.FromResult(sessionInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating session for terminal {TerminalId}, operator {OperatorId}",
                    terminalId, operatorId);
                throw;
            }
        }

        /// <summary>
        /// Gets session information by session identifier.
        /// </summary>
        /// <param name="sessionId">The session identifier.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Session information if found, otherwise null.</returns>
        public Task<SessionInfo?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_sessions.TryGetValue(sessionId, out var sessionInfo))
                {
                    // Check if session has expired
                    var timeoutMinutes = _options.Session.TimeoutMinutes;
                    var isExpired = DateTime.UtcNow - sessionInfo.LastActivity > TimeSpan.FromMinutes(timeoutMinutes);

                    if (isExpired && sessionInfo.IsActive)
                    {
                        _logger.LogInformation("Session {SessionId} expired, marking as inactive", sessionId);
                        sessionInfo.IsActive = false;
                    }

                    return Task.FromResult<SessionInfo?>(sessionInfo);
                }

                return Task.FromResult<SessionInfo?>(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting session {SessionId}", sessionId);
                throw;
            }
        }

        /// <summary>
        /// Updates the last activity timestamp for a session.
        /// </summary>
        /// <param name="sessionId">The session identifier.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public Task UpdateSessionActivityAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_sessions.TryGetValue(sessionId, out var sessionInfo))
                {
                    sessionInfo.LastActivity = DateTime.UtcNow;
                    _logger.LogDebug("Updated activity for session {SessionId}", sessionId);
                }
                else
                {
                    _logger.LogWarning("Attempted to update activity for non-existent session {SessionId}", sessionId);
                }
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating session activity for {SessionId}", sessionId);
                throw;
            }
        }

        /// <summary>
        /// Closes a session and marks it as inactive.
        /// </summary>
        /// <param name="sessionId">The session identifier.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public Task CloseSessionAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_sessions.TryGetValue(sessionId, out var sessionInfo))
                {
                    sessionInfo.IsActive = false;
                    _logger.LogInformation("Session {SessionId} closed", sessionId);
                }
                else
                {
                    _logger.LogWarning("Attempted to close non-existent session {SessionId}", sessionId);
                }
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing session {SessionId}", sessionId);
                throw;
            }
        }

        /// <summary>
        /// Gets all currently active sessions.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A list of active session information.</returns>
        public Task<IReadOnlyList<SessionInfo>> GetActiveSessionsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var activeSessions = _sessions.Values
                    .Where(s => s.IsActive)
                    .OrderBy(s => s.CreatedAt)
                    .ToArray();

                return Task.FromResult<IReadOnlyList<SessionInfo>>(activeSessions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active sessions");
                throw;
            }
        }

        /// <summary>
        /// Cleans up expired sessions and removes old inactive sessions.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public Task CleanupExpiredSessionsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var timeoutMinutes = _options.Session.TimeoutMinutes;
                var cutoffTime = DateTime.UtcNow - TimeSpan.FromMinutes(timeoutMinutes);
                var expiredSessionIds = new List<string>();

                foreach (var kvp in _sessions)
                {
                    var sessionInfo = kvp.Value;
                    if (sessionInfo.IsActive && sessionInfo.LastActivity < cutoffTime)
                    {
                        sessionInfo.IsActive = false;
                        expiredSessionIds.Add(kvp.Key);
                    }
                }

                if (expiredSessionIds.Count > 0)
                {
                    _logger.LogInformation("Marked {Count} sessions as expired", expiredSessionIds.Count);
                    
                    // Optional: Remove old inactive sessions to prevent memory leaks
                    var veryOldCutoff = DateTime.UtcNow - TimeSpan.FromHours(24);
                    var sessionsToRemove = _sessions
                        .Where(kvp => !kvp.Value.IsActive && kvp.Value.LastActivity < veryOldCutoff)
                        .Select(kvp => kvp.Key)
                        .ToArray();

                    foreach (var sessionId in sessionsToRemove)
                    {
                        _sessions.TryRemove(sessionId, out _);
                    }

                    if (sessionsToRemove.Length > 0)
                    {
                        _logger.LogInformation("Removed {Count} old inactive sessions", sessionsToRemove.Length);
                    }
                }
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during session cleanup");
                throw;
            }
        }

        private void CleanupExpiredSessionsCallback(object? state)
        {
            try
            {
                _ = CleanupExpiredSessionsAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in cleanup timer callback");
            }
        }

        private static string GenerateSessionId()
        {
            // Generate a unique session ID with timestamp and random component
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var random = Guid.NewGuid().ToString("N")[..8];
            return $"session_{timestamp}_{random}";
        }

        /// <summary>
        /// Disposes the session manager and releases all resources.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _cleanupTimer?.Dispose();
                _sessions.Clear();
                _disposed = true;
            }
        }
    }
}
