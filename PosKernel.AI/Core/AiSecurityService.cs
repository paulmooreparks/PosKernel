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

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PosKernel.AI.Core
{
    /// <summary>
    /// Security service for AI interactions, ensuring safe AI integration.
    /// Implements prompt sanitization, response validation, and audit logging.
    /// </summary>
    public interface IAiSecurityService
    {
        /// <summary>
        /// Sanitizes user input before sending to AI models.
        /// </summary>
        Task<string> SanitizePromptAsync(string prompt);

        /// <summary>
        /// Validates and filters context data before AI processing.
        /// </summary>
        Task<McpContext> ValidateContextAsync(McpContext context);

        /// <summary>
        /// Filters sensitive data from objects before sending to AI.
        /// </summary>
        Task<object> FilterDataForAiAsync(object data);

        /// <summary>
        /// Validates AI responses for security concerns.
        /// </summary>
        Task<McpResponse> ValidateResponseAsync(McpResponse response);

        /// <summary>
        /// Logs AI interactions for audit and compliance.
        /// </summary>
        Task LogAiInteractionAsync(AiAuditEvent auditEvent);
    }

    /// <summary>
    /// Default implementation of AI security service with comprehensive protection.
    /// </summary>
    public class AiSecurityService : IAiSecurityService
    {
        private readonly IPromptSanitizer _promptSanitizer;
        private readonly IResponseValidator _responseValidator;
        private readonly IAuditLogger _auditLogger;
        private readonly List<string> _bannedPatterns;

        public AiSecurityService(
            IPromptSanitizer promptSanitizer,
            IResponseValidator responseValidator,
            IAuditLogger auditLogger)
        {
            _promptSanitizer = promptSanitizer ?? throw new ArgumentNullException(nameof(promptSanitizer));
            _responseValidator = responseValidator ?? throw new ArgumentNullException(nameof(responseValidator));
            _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
            
            // Initialize banned patterns for security
            _bannedPatterns = new List<string>
            {
                // Injection attempts
                "ignore previous instructions",
                "system prompt",
                "jailbreak",
                
                // Sensitive data patterns
                @"\b\d{4}[\s-]?\d{4}[\s-]?\d{4}[\s-]?\d{4}\b", // Credit card patterns
                @"\b\d{3}-\d{2}-\d{4}\b", // SSN patterns
                
                // PII indicators
                "credit card",
                "social security",
                "password",
                "token"
            };
        }

        public async Task<string> SanitizePromptAsync(string prompt)
        {
            if (string.IsNullOrEmpty(prompt))
                return string.Empty;

            // Check for banned patterns
            var lowerPrompt = prompt.ToLowerInvariant();
            foreach (var pattern in _bannedPatterns)
            {
                if (lowerPrompt.Contains(pattern.ToLowerInvariant()))
                {
                    await _auditLogger.LogSecurityEventAsync(new SecurityAuditEvent
                    {
                        EventType = "BannedPatternDetected",
                        Pattern = pattern,
                        Timestamp = DateTimeOffset.UtcNow,
                        Severity = SecuritySeverity.High
                    });
                    
                    throw new SecurityException($"Prompt contains banned pattern: {pattern}");
                }
            }

            // Use dedicated sanitizer
            var sanitized = await _promptSanitizer.SanitizeAsync(prompt);
            
            // Log sanitization if changes were made
            if (sanitized != prompt)
            {
                await _auditLogger.LogSecurityEventAsync(new SecurityAuditEvent
                {
                    EventType = "PromptSanitized",
                    Timestamp = DateTimeOffset.UtcNow,
                    Severity = SecuritySeverity.Medium,
                    Details = "Prompt was sanitized before AI processing"
                });
            }

            return sanitized;
        }

        public async Task<McpContext> ValidateContextAsync(McpContext context)
        {
            if (context == null)
                return new McpContext();

            // Create filtered context - never send PII to AI
            var validatedContext = new McpContext
            {
                SessionId = context.SessionId,
                StoreId = context.StoreId,
                SecurityTags = context.SecurityTags ?? new List<string>()
            };

            // Filter business context to remove sensitive data
            validatedContext.BusinessContext = await FilterBusinessContextAsync(context.BusinessContext);

            // Add security tags
            validatedContext.SecurityTags.Add($"validated_{DateTimeOffset.UtcNow:yyyyMMdd}");

            return validatedContext;
        }

        public async Task<object> FilterDataForAiAsync(object data)
        {
            if (data == null)
                return new { };

            // Use reflection to filter out sensitive properties
            var filteredData = new Dictionary<string, object>();
            var properties = data.GetType().GetProperties();

            foreach (var property in properties)
            {
                var name = property.Name.ToLowerInvariant();
                var value = property.GetValue(data);

                // Skip sensitive properties
                if (IsSensitiveProperty(name))
                {
                    await _auditLogger.LogSecurityEventAsync(new SecurityAuditEvent
                    {
                        EventType = "SensitiveDataFiltered",
                        Details = $"Filtered property: {property.Name}",
                        Timestamp = DateTimeOffset.UtcNow,
                        Severity = SecuritySeverity.Low
                    });
                    continue;
                }

                // Include safe properties
                filteredData[property.Name] = value ?? "";
            }

            return filteredData;
        }

        public async Task<McpResponse> ValidateResponseAsync(McpResponse response)
        {
            if (response == null)
                throw new SecurityException("Received null response from AI");

            // Validate response using dedicated validator
            var isValid = await _responseValidator.ValidateAsync(response);
            if (!isValid)
            {
                await _auditLogger.LogSecurityEventAsync(new SecurityAuditEvent
                {
                    EventType = "InvalidAiResponse",
                    Timestamp = DateTimeOffset.UtcNow,
                    Severity = SecuritySeverity.High,
                    Details = "AI response failed validation"
                });
                
                throw new SecurityException("AI response failed security validation");
            }

            // Check for potential data leakage in response
            await ValidateResponseForDataLeakageAsync(response);

            return response;
        }

        public async Task LogAiInteractionAsync(AiAuditEvent auditEvent)
        {
            if (auditEvent == null)
                return;

            // Enhance audit event with security context
            auditEvent.SecurityLevel = DetermineSecurityLevel(auditEvent);
            auditEvent.ComplianceFlags = await GetComplianceFlagsAsync(auditEvent);

            await _auditLogger.LogAiInteractionAsync(auditEvent);
        }

        private async Task<Dictionary<string, object>> FilterBusinessContextAsync(Dictionary<string, object> businessContext)
        {
            var filtered = new Dictionary<string, object>();

            foreach (var kvp in businessContext ?? new Dictionary<string, object>())
            {
                if (!IsSensitiveProperty(kvp.Key.ToLowerInvariant()))
                {
                    filtered[kvp.Key] = kvp.Value;
                }
            }

            return filtered;
        }

        private bool IsSensitiveProperty(string propertyName)
        {
            var sensitiveNames = new[]
            {
                "password", "token", "key", "secret",
                "creditcard", "cardnumber", "cvv", "pin",
                "ssn", "socialsecurity", "taxid",
                "email", "phone", "address",
                "customerinfo", "paymentdetails", "personaldata"
            };

            return Array.Exists(sensitiveNames, name => propertyName.Contains(name));
        }

        private async Task ValidateResponseForDataLeakageAsync(McpResponse response)
        {
            // Check if AI response contains patterns that look like sensitive data
            var content = response.Content?.ToLowerInvariant() ?? "";

            foreach (var pattern in _bannedPatterns)
            {
                if (content.Contains(pattern.ToLowerInvariant()))
                {
                    await _auditLogger.LogSecurityEventAsync(new SecurityAuditEvent
                    {
                        EventType = "PotentialDataLeakage",
                        Pattern = pattern,
                        Timestamp = DateTimeOffset.UtcNow,
                        Severity = SecuritySeverity.Critical,
                        Details = "AI response may contain sensitive data"
                    });
                    
                    // Sanitize the response
                    response.Content = "[REDACTED: Potential sensitive data detected]";
                    response.Warnings.Add("Response content was sanitized for security");
                }
            }
        }

        private SecurityLevel DetermineSecurityLevel(AiAuditEvent auditEvent)
        {
            // Determine security level based on request type and context
            return auditEvent.RequestType switch
            {
                "analysis" when auditEvent.Success => SecurityLevel.Medium,
                "prediction" when auditEvent.Success => SecurityLevel.Medium,
                "completion" when auditEvent.Success => SecurityLevel.Low,
                _ when !auditEvent.Success => SecurityLevel.High,
                _ => SecurityLevel.Low
            };
        }

        private async Task<List<string>> GetComplianceFlagsAsync(AiAuditEvent auditEvent)
        {
            var flags = new List<string>();

            // Add compliance flags based on the interaction
            if (auditEvent.RequestType == "analysis")
                flags.Add("DataProcessing");
            
            if (!string.IsNullOrEmpty(auditEvent.Error))
                flags.Add("ErrorReporting");

            return flags;
        }
    }

    /// <summary>
    /// Interface for prompt sanitization services.
    /// </summary>
    public interface IPromptSanitizer
    {
        Task<string> SanitizeAsync(string prompt);
    }

    /// <summary>
    /// Interface for response validation services.
    /// </summary>
    public interface IResponseValidator
    {
        Task<bool> ValidateAsync(McpResponse response);
    }

    /// <summary>
    /// Interface for audit logging services.
    /// </summary>
    public interface IAuditLogger
    {
        Task LogAiInteractionAsync(AiAuditEvent auditEvent);
        Task LogSecurityEventAsync(SecurityAuditEvent securityEvent);
    }

    /// <summary>
    /// Audit event for AI interactions.
    /// </summary>
    public class AiAuditEvent
    {
        public string RequestType { get; set; } = "";
        public DateTimeOffset Timestamp { get; set; }
        public bool Success { get; set; }
        public string? Error { get; set; }
        public int ResponseLength { get; set; }
        public SecurityLevel SecurityLevel { get; set; }
        public List<string> ComplianceFlags { get; set; } = new();
    }

    /// <summary>
    /// Security-specific audit event.
    /// </summary>
    public class SecurityAuditEvent
    {
        public string EventType { get; set; } = "";
        public string? Pattern { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public SecuritySeverity Severity { get; set; }
        public string? Details { get; set; }
    }

    /// <summary>
    /// Security levels for AI interactions.
    /// </summary>
    public enum SecurityLevel
    {
        Low,
        Medium,
        High,
        Critical
    }

    /// <summary>
    /// Security severity levels.
    /// </summary>
    public enum SecuritySeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    /// <summary>
    /// Security exception for AI-related security violations.
    /// </summary>
    public class SecurityException : Exception
    {
        public SecurityException(string message) : base(message) { }
        public SecurityException(string message, Exception innerException) : base(message, innerException) { }
    }
}
