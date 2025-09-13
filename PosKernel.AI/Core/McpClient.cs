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
using System.Text.Json;
using System.Threading.Tasks;

namespace PosKernel.AI.Core
{
    /// <summary>
    /// Core Model Context Protocol (MCP) client for AI integration.
    /// Provides the foundational interface to AI models while maintaining security isolation from the kernel.
    /// </summary>
    public class McpClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly McpConfiguration _configuration;
        private readonly IAiSecurityService _securityService;
        private bool _disposed;

        public McpClient(McpConfiguration configuration, IAiSecurityService securityService)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _securityService = securityService ?? throw new ArgumentNullException(nameof(securityService));
            
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(_configuration.BaseUrl),
                Timeout = TimeSpan.FromSeconds(_configuration.TimeoutSeconds)
            };
            
            // Add authentication headers if configured
            if (!string.IsNullOrEmpty(_configuration.ApiKey))
            {
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _configuration.ApiKey);
            }
        }

        /// <summary>
        /// Sends a completion request to the AI model with security validation.
        /// </summary>
        public async Task<McpResponse> CompleteAsync(string prompt, McpContext? context = null)
        {
            if (string.IsNullOrEmpty(prompt))
                throw new ArgumentException("Prompt cannot be null or empty", nameof(prompt));

            // Security: Sanitize prompt and validate context
            var sanitizedPrompt = await _securityService.SanitizePromptAsync(prompt);
            var validatedContext = context != null 
                ? await _securityService.ValidateContextAsync(context)
                : new McpContext();

            var request = new McpRequest
            {
                Type = "completion",
                Prompt = sanitizedPrompt,
                Context = validatedContext,
                Parameters = _configuration.DefaultParameters
            };

            return await SendRequestAsync(request);
        }

        /// <summary>
        /// Sends an analysis request for structured data analysis.
        /// </summary>
        public async Task<McpResponse> AnalyzeAsync(string analysisType, object data)
        {
            if (string.IsNullOrEmpty(analysisType))
                throw new ArgumentException("Analysis type cannot be null or empty", nameof(analysisType));
            
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            // Security: Filter sensitive data before sending to AI
            var filteredData = await _securityService.FilterDataForAiAsync(data);

            var request = new McpRequest
            {
                Type = "analysis",
                AnalysisType = analysisType,
                Data = filteredData,
                Parameters = _configuration.DefaultParameters
            };

            return await SendRequestAsync(request);
        }

        /// <summary>
        /// Sends a prediction request for forecasting and predictive analytics.
        /// </summary>
        public async Task<McpResponse> PredictAsync(string predictionType, object data)
        {
            if (string.IsNullOrEmpty(predictionType))
                throw new ArgumentException("Prediction type cannot be null or empty", nameof(predictionType));
            
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            // Security: Filter sensitive data
            var filteredData = await _securityService.FilterDataForAiAsync(data);

            var request = new McpRequest
            {
                Type = "prediction",
                PredictionType = predictionType,
                Data = filteredData,
                Parameters = _configuration.DefaultParameters
            };

            return await SendRequestAsync(request);
        }

        private async Task<McpResponse> SendRequestAsync(McpRequest request)
        {
            try
            {
                var json = JsonSerializer.Serialize(request, _configuration.JsonOptions);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var httpResponse = await _httpClient.PostAsync(_configuration.CompletionEndpoint, content);
                httpResponse.EnsureSuccessStatusCode();

                var responseJson = await httpResponse.Content.ReadAsStringAsync();
                var mcpResponse = JsonSerializer.Deserialize<McpResponse>(responseJson, _configuration.JsonOptions);

                if (mcpResponse == null)
                    throw new McpException("Received null response from MCP server");

                // Security: Validate response before returning
                var validatedResponse = await _securityService.ValidateResponseAsync(mcpResponse);

                // Audit: Log the AI interaction
                await _securityService.LogAiInteractionAsync(new AiAuditEvent
                {
                    RequestType = request.Type,
                    Timestamp = DateTimeOffset.UtcNow,
                    Success = true,
                    ResponseLength = responseJson.Length
                });

                return validatedResponse;
            }
            catch (Exception ex)
            {
                // Audit: Log the failure
                await _securityService.LogAiInteractionAsync(new AiAuditEvent
                {
                    RequestType = request.Type,
                    Timestamp = DateTimeOffset.UtcNow,
                    Success = false,
                    Error = ex.Message
                });

                throw new McpException($"MCP request failed: {ex.Message}", ex);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _httpClient?.Dispose();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Configuration for MCP client connections and behavior.
    /// </summary>
    public class McpConfiguration
    {
        public string BaseUrl { get; set; } = "https://api.openai.com/v1";
        public string CompletionEndpoint { get; set; } = "chat/completions";
        public string? ApiKey { get; set; }
        public int TimeoutSeconds { get; set; } = 30;
        public Dictionary<string, object> DefaultParameters { get; set; } = new();
        public JsonSerializerOptions JsonOptions { get; set; } = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <summary>
    /// Request structure for MCP protocol.
    /// </summary>
    public class McpRequest
    {
        public string Type { get; set; } = "";
        public string? Prompt { get; set; }
        public string? AnalysisType { get; set; }
        public string? PredictionType { get; set; }
        public object? Data { get; set; }
        public McpContext? Context { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    /// <summary>
    /// Response structure from MCP protocol.
    /// </summary>
    public class McpResponse
    {
        public string Id { get; set; } = "";
        public string Type { get; set; } = "";
        public string Content { get; set; } = "";
        public double Confidence { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }

    /// <summary>
    /// Context information for MCP requests.
    /// </summary>
    public class McpContext
    {
        public string SessionId { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = "";
        public string StoreId { get; set; } = "";
        public Dictionary<string, object> BusinessContext { get; set; } = new();
        public List<string> SecurityTags { get; set; } = new();
    }

    /// <summary>
    /// Exception thrown for MCP-related errors.
    /// </summary>
    public class McpException : Exception
    {
        public McpException(string message) : base(message) { }
        public McpException(string message, Exception innerException) : base(message, innerException) { }
    }
}
