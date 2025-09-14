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
        public virtual async Task<McpResponse> CompleteAsync(string prompt, McpContext? context = null)
        {
            if (string.IsNullOrEmpty(prompt)) {
                throw new ArgumentException("Prompt cannot be null or empty", nameof(prompt));
            }

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
        public virtual async Task<McpResponse> AnalyzeAsync(string analysisType, object data)
        {
            if (string.IsNullOrEmpty(analysisType)) {
                throw new ArgumentException("Analysis type cannot be null or empty", nameof(analysisType));
            }

            if (data == null) {
                throw new ArgumentNullException(nameof(data));
            }

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
        public virtual async Task<McpResponse> PredictAsync(string predictionType, object data)
        {
            if (string.IsNullOrEmpty(predictionType)) {
                throw new ArgumentException("Prediction type cannot be null or empty", nameof(predictionType));
            }

            if (data == null) {
                throw new ArgumentNullException(nameof(data));
            }

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

                if (mcpResponse == null) {
                    throw new McpException("Received null response from MCP server");
                }

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
            catch (Exception ex) {
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
        /// <summary>
        /// Gets or sets the base URL for the MCP server.
        /// </summary>
        public string BaseUrl { get; set; } = "https://api.openai.com/v1";
        
        /// <summary>
        /// Gets or sets the completion endpoint path.
        /// </summary>
        public string CompletionEndpoint { get; set; } = "chat/completions";
        
        /// <summary>
        /// Gets or sets the API key for authentication.
        /// </summary>
        public string? ApiKey { get; set; }
        
        /// <summary>
        /// Gets or sets the request timeout in seconds.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;
        
        /// <summary>
        /// Gets or sets the default parameters sent with requests.
        /// </summary>
        public Dictionary<string, object> DefaultParameters { get; set; } = new();
        
        /// <summary>
        /// Gets or sets the JSON serialization options.
        /// </summary>
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
        /// <summary>
        /// Gets or sets the request type (completion, analysis, prediction).
        /// </summary>
        public string Type { get; set; } = "";
        
        /// <summary>
        /// Gets or sets the prompt for completion requests.
        /// </summary>
        public string? Prompt { get; set; }
        
        /// <summary>
        /// Gets or sets the analysis type for analysis requests.
        /// </summary>
        public string? AnalysisType { get; set; }
        
        /// <summary>
        /// Gets or sets the prediction type for prediction requests.
        /// </summary>
        public string? PredictionType { get; set; }
        
        /// <summary>
        /// Gets or sets the data payload for analysis/prediction requests.
        /// </summary>
        public object? Data { get; set; }
        
        /// <summary>
        /// Gets or sets the context information for the request.
        /// </summary>
        public McpContext? Context { get; set; }
        
        /// <summary>
        /// Gets or sets additional parameters for the request.
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    /// <summary>
    /// Response structure from MCP protocol.
    /// </summary>
    public class McpResponse
    {
        /// <summary>
        /// Gets or sets the unique identifier for the response.
        /// </summary>
        public string Id { get; set; } = "";
        
        /// <summary>
        /// Gets or sets the response type.
        /// </summary>
        public string Type { get; set; } = "";
        
        /// <summary>
        /// Gets or sets the main content of the response.
        /// </summary>
        public string Content { get; set; } = "";
        
        /// <summary>
        /// Gets or sets the confidence level of the AI response (0-1).
        /// </summary>
        public double Confidence { get; set; }
        
        /// <summary>
        /// Gets or sets additional metadata about the response.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
        
        /// <summary>
        /// Gets or sets any warnings or notices about the response.
        /// </summary>
        public List<string> Warnings { get; set; } = new();
    }

    /// <summary>
    /// Context information for MCP requests.
    /// </summary>
    public class McpContext
    {
        /// <summary>
        /// Gets or sets the unique session identifier.
        /// </summary>
        public string SessionId { get; set; } = Guid.NewGuid().ToString();
        
        /// <summary>
        /// Gets or sets the user identifier.
        /// </summary>
        public string UserId { get; set; } = "";
        
        /// <summary>
        /// Gets or sets the store identifier.
        /// </summary>
        public string StoreId { get; set; } = "";
        
        /// <summary>
        /// Gets or sets business-specific context data.
        /// </summary>
        public Dictionary<string, object> BusinessContext { get; set; } = new();
        
        /// <summary>
        /// Gets or sets security tags for audit and compliance.
        /// </summary>
        public List<string> SecurityTags { get; set; } = new();
    }

    /// <summary>
    /// Exception thrown for MCP-related errors.
    /// </summary>
    public class McpException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="McpException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public McpException(string message) : base(message) { }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="McpException"/> class with a specified error message and inner exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public McpException(string message, Exception innerException) : base(message, innerException) { }
    }
}
