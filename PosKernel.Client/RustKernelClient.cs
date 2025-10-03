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
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Linq; // Add LINQ using directive for .Select() extension method

namespace PosKernel.Client.Rust
{
    /// <summary>
    /// HTTP client adapter for the POS Kernel Service.
    /// Implements IPosKernelClient to provide a seamless interface to the AI demo.
    /// </summary>
    public class PosClient : IPosKernelClient
    {
        private readonly ILogger<PosClient> _logger;
        private readonly RustKernelClientOptions _options;
        private readonly HttpClient _httpClient;
        private bool _disposed = false;
        private bool _connected = false;

        /// <summary>
        /// Initializes a new instance of the PosClient.
        /// </summary>
        /// <param name="logger">Logger for diagnostics.</param>
        /// <param name="options">Client configuration options.</param>
        public PosClient(ILogger<PosClient> logger, RustKernelClientOptions? options = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? new RustKernelClientOptions();

            _httpClient = new HttpClient()
            {
                BaseAddress = new Uri(_options.Address),
                Timeout = TimeSpan.FromMilliseconds(_options.RequestTimeoutMs)
            };
        }

        /// <inheritdoc/>
        public bool IsConnected => _connected && !_disposed;

        /// <inheritdoc/>
        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(RustKernelClient));
            }

            _logger.LogInformation("Connecting to POS Kernel Service at {Address}", _options.Address);

            try
            {
                // Test connection with health check
                var response = await _httpClient.GetAsync("/health", cancellationToken);
                response.EnsureSuccessStatusCode();

                var healthJson = await response.Content.ReadAsStringAsync(cancellationToken);
                var healthData = JsonSerializer.Deserialize<JsonElement>(healthJson);

                // Support both formats: {"healthy": true} and {"status": "healthy"}
                bool isHealthy = false;
                if (healthData.TryGetProperty("healthy", out var healthyProp) && healthyProp.GetBoolean())
                {
                    isHealthy = true;
                }
                else if (healthData.TryGetProperty("status", out var statusProp) &&
                         statusProp.GetString()?.Equals("healthy", StringComparison.OrdinalIgnoreCase) == true)
                {
                    isHealthy = true;
                }

                if (isHealthy)
                {
                    _connected = true;
                    _logger.LogInformation("✅ Connected to POS Kernel Service successfully");
                }
                else
                {
                    throw new InvalidOperationException("POS kernel service reported unhealthy status");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to POS Kernel Service");
                throw new InvalidOperationException("Unable to connect to POS Kernel Service", ex);
            }
        }

        /// <inheritdoc/>
        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
            {
                return Task.CompletedTask;
            }

            _logger.LogInformation("Disconnecting from Rust POS Kernel Service");
            _connected = false;
            _logger.LogInformation("Disconnected from Rust POS Kernel Service");
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public void Disconnect()
        {
            DisconnectAsync().Wait();
        }

        /// <inheritdoc/>
        public async Task<string> CreateSessionAsync(string terminalId, string operatorId, CancellationToken cancellationToken = default)
        {
            EnsureConnected();

            _logger.LogDebug("Creating session for terminal {TerminalId}, operator {OperatorId}", terminalId, operatorId);

            try
            {
                var request = new
                {
                    terminal_id = terminalId,
                    operator_id = operatorId
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/api/sessions", content, cancellationToken);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                var responseData = JsonSerializer.Deserialize<JsonElement>(responseJson);

                if (responseData.TryGetProperty("success", out var successProp) && successProp.GetBoolean())
                {
                    var sessionId = responseData.GetProperty("session_id").GetString()!;
                    _logger.LogInformation("Session {SessionId} created successfully", sessionId);
                    return sessionId;
                }
                else
                {
                    var error = responseData.TryGetProperty("error", out var errorProp) ? errorProp.GetString() : "Unknown error";
                    throw new InvalidOperationException($"Failed to create session: {error}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating session");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<TransactionClientResult> StartTransactionAsync(string sessionId, string currency, CancellationToken cancellationToken = default)
        {
            EnsureConnected();

            _logger.LogDebug("Starting transaction in session {SessionId} with currency {Currency}", sessionId, currency);

            try
            {
                // TEMPORARY FIX: Use hardcoded store name to bypass store configuration requirement
                // TODO: Implement proper store configuration injection via constructor or factory
                var storeName = "DEFAULT_STORE";

                var request = new
                {
                    session_id = sessionId,
                    store = storeName, // TEMPORARY: Hardcoded until store config injection is implemented
                    currency = currency
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"/api/sessions/{sessionId}/transactions", content, cancellationToken);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                var responseData = JsonSerializer.Deserialize<JsonElement>(responseJson);

                var result = new TransactionClientResult
                {
                    Success = responseData.GetProperty("success").GetBoolean(),
                    Error = responseData.TryGetProperty("error", out var errorProp) ? errorProp.GetString() : null,
                    SessionId = responseData.TryGetProperty("session_id", out var sidProp) ? sidProp.GetString() : sessionId,
                    TransactionId = responseData.TryGetProperty("transaction_id", out var tidProp) ? tidProp.GetString() : null,
                    Total = responseData.TryGetProperty("total", out var totalProp) ? (decimal)totalProp.GetDouble() : 0,
                    State = responseData.TryGetProperty("state", out var stateProp) ? stateProp.GetString() ?? "" : "",
                    Data = responseData
                };

                if (result.Success)
                {
                    _logger.LogInformation("Transaction {TransactionId} started successfully", result.TransactionId);
                }
                else
                {
                    _logger.LogWarning("Failed to start transaction: {Error}", result.Error);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting transaction");
                return new TransactionClientResult
                {
                    Success = false,
                    Error = ex.Message,
                    SessionId = sessionId
                };
            }
        }

        /// <inheritdoc/>
        public async Task<TransactionClientResult> AddLineItemAsync(string sessionId, string transactionId, string productId, int quantity, decimal unitPrice, string? productName = null, string? productDescription = null, CancellationToken cancellationToken = default)
        {
            EnsureConnected();

            _logger.LogDebug("Adding line item {ProductId} x{Quantity} @ {UnitPrice} to transaction {TransactionId}",
                productId, quantity, unitPrice, transactionId);

            try
            {
                var request = new
                {
                    session_id = sessionId,
                    transaction_id = transactionId,
                    product_id = productId,
                    quantity = quantity,
                    unit_price = (double)unitPrice,
                    product_name = productName,
                    product_description = productDescription
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"/api/sessions/{sessionId}/transactions/{transactionId}/lines", content, cancellationToken);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                var responseData = JsonSerializer.Deserialize<JsonElement>(responseJson);

                var result = ParseTransactionResponse(responseData, sessionId, transactionId);

                if (result.Success)
                {
                    _logger.LogInformation("Line item added to transaction {TransactionId}, new total: {Total:C}",
                        result.TransactionId, result.Total);
                }
                else
                {
                    _logger.LogWarning("Failed to add line item: {Error}", result.Error);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding line item");
                return new TransactionClientResult
                {
                    Success = false,
                    Error = ex.Message,
                    SessionId = sessionId,
                    TransactionId = transactionId
                };
            }
        }

        /// <inheritdoc/>
        public async Task<TransactionClientResult> AddChildLineItemAsync(string sessionId, string transactionId, string productId, int quantity, decimal unitPrice, int parentLineNumber, string? productName = null, string? productDescription = null, CancellationToken cancellationToken = default)
        {
            EnsureConnected();

            _logger.LogDebug("Adding child line item {ProductId} x{Quantity} @ {UnitPrice} to transaction {TransactionId}, parent line {ParentLineNumber}",
                productId, quantity, unitPrice, transactionId, parentLineNumber);

            try
            {
                var request = new
                {
                    session_id = sessionId,
                    transaction_id = transactionId,
                    product_id = productId,
                    quantity = quantity,
                    unit_price = (double)unitPrice,
                    parent_line_number = parentLineNumber,
                    product_name = productName,
                    product_description = productDescription
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"/api/sessions/{sessionId}/transactions/{transactionId}/child-lines", content, cancellationToken);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                var responseData = JsonSerializer.Deserialize<JsonElement>(responseJson);

                var result = ParseTransactionResponse(responseData, sessionId, transactionId);

                if (result.Success)
                {
                    _logger.LogInformation("Child line item added to transaction {TransactionId}, new total: {Total:C}",
                        result.TransactionId, result.Total);
                }
                else
                {
                    _logger.LogWarning("Failed to add child line item: {Error}", result.Error);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding child line item");
                return new TransactionClientResult
                {
                    Success = false,
                    Error = ex.Message,
                    SessionId = sessionId,
                    TransactionId = transactionId
                };
            }
        }

        /// <inheritdoc/>
        public async Task<TransactionClientResult> AddModificationAsync(string sessionId, string transactionId, int parentLineNumber, string modificationId, int quantity, decimal unitPrice, LineItemType itemType = LineItemType.Modification, CancellationToken cancellationToken = default)
        {
            EnsureConnected();

            _logger.LogDebug("Adding modification {ModificationId} x{Quantity} @ {UnitPrice:C} to line {ParentLineNumber} in transaction {TransactionId}",
                modificationId, quantity, unitPrice, parentLineNumber, transactionId);

            try
            {
                // NRF COMPLIANCE: Use child-lines endpoint for all parent-child relationships
                var request = new
                {
                    session_id = sessionId,
                    transaction_id = transactionId,
                    product_id = modificationId, // Rust service expects product_id field name
                    quantity = quantity,
                    unit_price = (double)unitPrice,
                    parent_line_number = parentLineNumber
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // ARCHITECTURAL FIX: Use /child-lines endpoint instead of /modifications
                var response = await _httpClient.PostAsync($"/api/sessions/{sessionId}/transactions/{transactionId}/child-lines", content, cancellationToken);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                var responseData = JsonSerializer.Deserialize<JsonElement>(responseJson);

                var result = ParseTransactionResponse(responseData, sessionId, transactionId);

                if (result.Success)
                {
                    _logger.LogInformation("Modification {ModificationId} added to line {ParentLineNumber} in transaction {TransactionId}, new total: {Total:C}",
                        modificationId, parentLineNumber, result.TransactionId, result.Total);
                }
                else
                {
                    _logger.LogWarning("Failed to add modification: {Error}", result.Error);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding modification");
                return new TransactionClientResult
                {
                    Success = false,
                    Error = ex.Message,
                    SessionId = sessionId,
                    TransactionId = transactionId
                };
            }
        }

        /// <inheritdoc/>
        public async Task<TransactionClientResult> VoidLineItemAsync(string sessionId, string transactionId, int lineNumber, CancellationToken cancellationToken = default)
        {
            EnsureConnected();

            _logger.LogDebug("Voiding line item {LineNumber} from transaction {TransactionId}", lineNumber, transactionId);

            try
            {
                // Send void request with JSON body containing reason and operator_id
                var request = new
                {
                    reason = "customer requested",
                    operator_id = (string?)null  // AI Assistant doesn't have operator ID
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Create HttpRequestMessage for DELETE with body
                var httpRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/sessions/{sessionId}/transactions/{transactionId}/lines/{lineNumber}")
                {
                    Content = content
                };

                var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                var responseData = JsonSerializer.Deserialize<JsonElement>(responseJson);

                var result = new TransactionClientResult
                {
                    Success = responseData.GetProperty("success").GetBoolean(),
                    Error = responseData.TryGetProperty("error", out var errorProp) ? errorProp.GetString() : null,
                    SessionId = responseData.TryGetProperty("session_id", out var sidProp) ? sidProp.GetString() : sessionId,
                    TransactionId = responseData.TryGetProperty("transaction_id", out var tidProp) ? tidProp.GetString() : transactionId,
                    Total = responseData.TryGetProperty("total", out var totalProp) ? (decimal)totalProp.GetDouble() : 0,
                    State = responseData.TryGetProperty("state", out var stateProp) ? stateProp.GetString() ?? "" : "",
                    Data = responseData
                };

                if (result.Success)
                {
                    _logger.LogInformation("Line item {LineNumber} voided from transaction {TransactionId}, new total: {Total:C}",
                        lineNumber, result.TransactionId, result.Total);
                }
                else
                {
                    _logger.LogWarning("Failed to void line item: {Error}", result.Error);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error voiding line item");
                return new TransactionClientResult
                {
                    Success = false,
                    Error = ex.Message,
                    SessionId = sessionId,
                    TransactionId = transactionId
                };
            }
        }

        /// <inheritdoc/>
        public async Task<TransactionClientResult> UpdateLineItemQuantityAsync(string sessionId, string transactionId, int lineNumber, int newQuantity, CancellationToken cancellationToken = default)
        {
            EnsureConnected();

            _logger.LogDebug("Updating line item {LineNumber} quantity to {NewQuantity} in transaction {TransactionId}",
                lineNumber, newQuantity, transactionId);

            try
            {
                // Rust service expects only new_quantity and operator_id in JSON body
                var request = new
                {
                    new_quantity = newQuantity,
                    operator_id = (string?)null  // AI Assistant doesn't have operator ID
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync($"/api/sessions/{sessionId}/transactions/{transactionId}/lines/{lineNumber}", content, cancellationToken);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                var responseData = JsonSerializer.Deserialize<JsonElement>(responseJson);

                var result = new TransactionClientResult
                {
                    Success = responseData.GetProperty("success").GetBoolean(),
                    Error = responseData.TryGetProperty("error", out var errorProp) ? errorProp.GetString() : null,
                    SessionId = responseData.TryGetProperty("session_id", out var sidProp) ? sidProp.GetString() : sessionId,
                    TransactionId = responseData.TryGetProperty("transaction_id", out var tidProp) ? tidProp.GetString() : transactionId,
                    Total = responseData.TryGetProperty("total", out var totalProp) ? (decimal)totalProp.GetDouble() : 0,
                    State = responseData.TryGetProperty("state", out var stateProp) ? stateProp.GetString() ?? "" : "",
                    Data = responseData
                };

                if (result.Success)
                {
                    _logger.LogInformation("Line item {LineNumber} quantity updated to {NewQuantity} in transaction {TransactionId}, new total: {Total:C}",
                        lineNumber, newQuantity, result.TransactionId, result.Total);
                }
                else
                {
                    _logger.LogWarning("Failed to update line item quantity: {Error}", result.Error);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating line item quantity");
                return new TransactionClientResult
                {
                    Success = false,
                    Error = ex.Message,
                    SessionId = sessionId,
                    TransactionId = transactionId
                };
            }
        }

        /// <inheritdoc/>
        public async Task<TransactionClientResult> ProcessPaymentAsync(string sessionId, string transactionId, decimal amount, string paymentType = "cash", CancellationToken cancellationToken = default)
        {
            EnsureConnected();

            _logger.LogDebug("Processing {PaymentType} payment of {Amount:C} for transaction {TransactionId}",
                paymentType, amount, transactionId);

            try
            {
                var request = new
                {
                    session_id = sessionId,
                    transaction_id = transactionId,
                    amount = (double)amount,
                    payment_type = paymentType
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"/api/sessions/{sessionId}/transactions/{transactionId}/payment", content, cancellationToken);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                var responseData = JsonSerializer.Deserialize<JsonElement>(responseJson);

                var result = new TransactionClientResult
                {
                    Success = responseData.GetProperty("success").GetBoolean(),
                    Error = responseData.TryGetProperty("error", out var errorProp) ? errorProp.GetString() : null,
                    SessionId = responseData.TryGetProperty("session_id", out var sidProp) ? sidProp.GetString() : sessionId,
                    TransactionId = responseData.TryGetProperty("transaction_id", out var tidProp) ? tidProp.GetString() : transactionId,
                    Total = responseData.TryGetProperty("total", out var totalProp) ? (decimal)totalProp.GetDouble() : 0,
                    State = responseData.TryGetProperty("state", out var stateProp) ? stateProp.GetString() ?? "" : "",
                    Data = responseData
                };

                if (result.Success)
                {
                    var change = responseData.TryGetProperty("change", out var changeProp) ? (decimal)changeProp.GetDouble() : 0;
                    _logger.LogInformation("Payment processed for transaction {TransactionId}, change due: {Change:C}",
                        result.TransactionId, change);
                }
                else
                {
                    _logger.LogWarning("Failed to process payment: {Error}", result.Error);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing payment");
                return new TransactionClientResult
                {
                    Success = false,
                    Error = ex.Message,
                    SessionId = sessionId,
                    TransactionId = transactionId
                };
            }
        }

        /// <inheritdoc/>
        public async Task<TransactionClientResult> GetTransactionAsync(string sessionId, string transactionId, CancellationToken cancellationToken = default)
        {
            EnsureConnected();

            try
            {
                var response = await _httpClient.GetAsync($"/api/sessions/{sessionId}/transactions/{transactionId}", cancellationToken);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync(cancellationToken);

                // CRITICAL DEBUG: Log the actual JSON response from the Rust service
                _logger.LogInformation("RUST_SERVICE_JSON: Raw response length: {Length} characters", json.Length);
                _logger.LogInformation("RUST_SERVICE_JSON: Content: {JsonContent}", json);

                // Parse the JSON response to get transaction details
                var transactionResponse = ParseTransactionResponse(json);

                if (!transactionResponse.Success)
                {
                    return new TransactionClientResult
                    {
                        Success = false,
                        Error = transactionResponse.Error,
                        SessionId = sessionId,
                        TransactionId = transactionId
                    };
                }

                // CRITICAL FIX: Properly populate LineItems from the Rust service response with NRF parent-child relationships
                var result = new TransactionClientResult
                {
                    Success = true,
                    SessionId = sessionId,
                    TransactionId = transactionResponse.TransactionId,
                    Total = (decimal)transactionResponse.Total,
                    State = transactionResponse.State,
                    LineItems = transactionResponse.LineItems?.Select(item => new TransactionLineItem
                    {
                        LineItemId = item.LineItemId, // ARCHITECTURAL FIX: Map stable line item ID from kernel
                        LineNumber = item.LineNumber,
                        ParentLineNumber = (int)(item.ParentLineNumber ?? 0), // NRF COMPLIANCE: Read parent ID from JSON
                        ProductId = item.ProductId,
                        ProductName = item.ProductName, // ARCHITECTURAL FIX: Copy product metadata
                        ProductDescription = item.ProductDescription, // ARCHITECTURAL FIX: Copy product metadata
                        ItemType = DetermineLineItemType(item.ParentLineNumber), // Determine type based on parent relationship
                        Quantity = item.Quantity,
                        UnitPrice = (decimal)item.UnitPrice,
                        ExtendedPrice = (decimal)item.ExtendedPrice,
                        DisplayIndentLevel = DetermineIndentLevel(item.ParentLineNumber), // Calculate indent based on hierarchy
                        IsVoided = false, // TODO: Add void status from service when available
                        VoidReason = null,
                        Metadata = new Dictionary<string, string>()
                    }).ToList() ?? new List<TransactionLineItem>()
                };

                _logger.LogInformation("Retrieved transaction {TransactionId} with {LineCount} line items, total: {Total}",
                    result.TransactionId, result.LineItems.Count, result.Total);

                // CRITICAL DEBUG: Log the actual line items being returned
                foreach (var item in result.LineItems)
                {
                    _logger.LogInformation("LineItem: Line {LineNumber}, Product: {ProductId}, Qty: {Quantity}, Unit: {UnitPrice}, Extended: {ExtendedPrice}",
                        item.LineNumber, item.ProductId, item.Quantity, item.UnitPrice, item.ExtendedPrice);
                }

                // CRITICAL DEBUG: Log the parsed TransactionResponse to see if line items made it through JSON parsing
                _logger.LogInformation("TRANSACTION_RESPONSE_DEBUG: LineItems count from JSON: {Count}", transactionResponse.LineItems?.Count ?? 0);
                if (transactionResponse.LineItems != null)
                {
                    foreach (var jsonItem in transactionResponse.LineItems)
                    {
                        _logger.LogInformation("JSON_LINEITEM: Line {LineNumber}, ProductId: {ProductId}, Qty: {Quantity}, UnitPrice: {UnitPrice}",
                            jsonItem.LineNumber, jsonItem.ProductId, jsonItem.Quantity, jsonItem.UnitPrice);
                    }
                }

                return result;
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("404"))
            {
                return new TransactionClientResult
                {
                    Success = false,
                    Error = $"Transaction {transactionId} not found in session {sessionId}",
                    SessionId = sessionId,
                    TransactionId = transactionId
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to get transaction {TransactionId} from session {SessionId}", transactionId, sessionId);
                return new TransactionClientResult
                {
                    Success = false,
                    Error = $"Failed to get transaction details: {ex.Message}",
                    SessionId = sessionId,
                    TransactionId = transactionId
                };
            }
        }

        /// <inheritdoc/>
        public async Task CloseSessionAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            EnsureConnected();

            _logger.LogDebug("Closing session {SessionId}", sessionId);

            try
            {
                // Send close session request - assuming the service has this endpoint
                var response = await _httpClient.DeleteAsync($"/api/sessions/{sessionId}", cancellationToken);
                response.EnsureSuccessStatusCode();

                _logger.LogInformation("Session {SessionId} closed successfully", sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing session {SessionId}", sessionId);
                throw new InvalidOperationException($"Failed to close session {sessionId}: {ex.Message}", ex);
            }
        }

        private TransactionResponse ParseTransactionResponse(string json)
        {
            try
            {
                // CRITICAL DEBUG: Log the actual JSON response from the Rust service
                _logger.LogInformation("RUST_SERVICE_JSON: Raw response length: {Length} characters", json.Length);
                _logger.LogInformation("RUST_SERVICE_JSON: Content: {JsonContent}", json);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var result = JsonSerializer.Deserialize<TransactionResponse>(json, options)
                    ?? throw new InvalidOperationException("Failed to deserialize transaction response");

                // CRITICAL DEBUG: Log what was actually deserialized
                _logger.LogInformation("RUST_SERVICE_PARSED: Success: {Success}, LineItems count: {Count}, Total: {Total}",
                    result.Success, result.LineItems?.Count ?? 0, result.Total);

                if (result.LineItems != null)
                {
                    foreach (var item in result.LineItems)
                    {
                        _logger.LogInformation("RUST_SERVICE_LINEITEM: Line {LineNumber}, ProductId: {ProductId}, Qty: {Quantity}, UnitPrice: {UnitPrice}",
                            item.LineNumber, item.ProductId, item.Quantity, item.UnitPrice);
                        _logger.LogInformation("🔍 RUST_SERVICE_METADATA: Line {LineNumber}, ProductName: '{ProductName}', ProductDescription: '{ProductDescription}'",
                            item.LineNumber, item.ProductName ?? "(null)", item.ProductDescription ?? "(null)");
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse Rust service transaction response JSON: {Json}", json);
                throw new InvalidOperationException($"Failed to parse transaction response JSON: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Parses a JSON response into a TransactionClientResult.
        /// /// Adds null-coalescing operators to ensure LineItems is never null
        /// </summary>
        /// <param name="responseData">The JSON element from the service.</param>
        /// <param name="sessionId">The session ID.</param>
        /// <param name="transactionId">The transaction ID.</param>
        /// <returns>A TransactionClientResult instance.</returns>
        private TransactionClientResult ParseTransactionResponse(JsonElement responseData, string sessionId, string transactionId)
        {
            var result = new TransactionClientResult
            {
                Success = responseData.GetProperty("success").GetBoolean(),
                Error = responseData.TryGetProperty("error", out var errorProp) ? errorProp.GetString() : null,
                SessionId = responseData.TryGetProperty("session_id", out var sidProp) ? sidProp.GetString() : sessionId,
                TransactionId = responseData.TryGetProperty("transaction_id", out var tidProp) ? tidProp.GetString() : transactionId,
                Total = responseData.TryGetProperty("total", out var totalProp) ? (decimal)totalProp.GetDouble() : 0,
                State = responseData.TryGetProperty("state", out var stateProp) ? stateProp.GetString() ?? "" : "",
                Data = responseData
            };

            // NRF COMPLIANCE: Parse line items with parent-child relationships
            if (responseData.TryGetProperty("line_items", out var lineItemsArray) && lineItemsArray.ValueKind == JsonValueKind.Array)
            {
                result.LineItems = lineItemsArray.EnumerateArray()
                    .Select(item => {
                        var productId = item.TryGetProperty("product_id", out var prodProp) ? prodProp.GetString() ?? "" : "";
                        var productName = item.TryGetProperty("product_name", out var nameProp) ? nameProp.GetString() ?? "" : "";
                        var productDescription = item.TryGetProperty("product_description", out var descProp) ? descProp.GetString() ?? "" : "";

                        _logger.LogInformation("🔍 PARSE_DEBUG: ProductId='{ProductId}', ProductName='{ProductName}', ProductDescription='{ProductDescription}'",
                            productId, productName, productDescription);

                        return new TransactionLineItem
                        {
                            LineItemId = item.TryGetProperty("line_item_id", out var lineItemIdProp) ? lineItemIdProp.GetString() ?? "" : "", // ARCHITECTURAL FIX: Map stable ID
                            LineNumber = item.TryGetProperty("line_number", out var lineNumProp) ? lineNumProp.GetInt32() : 0,
                            ParentLineNumber = item.TryGetProperty("parent_line_number", out var parentProp) && parentProp.ValueKind != JsonValueKind.Null
                                ? parentProp.GetInt32()
                                : 0, // NRF COMPLIANCE: Read parent relationship from JSON
                            ProductId = productId,
                            ProductName = productName,
                            ProductDescription = productDescription,
                            ItemType = DetermineLineItemType(item.TryGetProperty("parent_line_number", out var parentTypeProp) && parentTypeProp.ValueKind != JsonValueKind.Null
                                ? (uint)parentTypeProp.GetInt32()
                                : null),
                            Quantity = item.TryGetProperty("quantity", out var qtyProp) ? qtyProp.GetInt32() : 0,
                            UnitPrice = item.TryGetProperty("unit_price", out var unitProp) ? (decimal)unitProp.GetDouble() : 0,
                            ExtendedPrice = item.TryGetProperty("extended_price", out var extProp) ? (decimal)extProp.GetDouble() : 0,
                            DisplayIndentLevel = DetermineIndentLevel(item.TryGetProperty("parent_line_number", out var parentIndentProp) && parentIndentProp.ValueKind != JsonValueKind.Null
                                ? (uint)parentIndentProp.GetInt32()
                                : null),
                            IsVoided = false, // TODO: Add void status from service when available
                            VoidReason = null,
                            Metadata = new Dictionary<string, string>()
                        };
                    }).ToList();
            }
            else
            {
                result.LineItems = new List<TransactionLineItem>();
            }

            return result;
        }

        private string FormatCurrency(decimal amount)
        {
            // ARCHITECTURAL FIX: FAIL FAST - No hardcoded currency formatting
            throw new InvalidOperationException(
                "DESIGN DEFICIENCY: Currency formatting requires ICurrencyFormattingService. " +
                "Client cannot decide currency symbols, decimal places, or formatting rules. " +
                "Register ICurrencyFormattingService in DI container and inject into RustKernelClient. " +
                "Current implementation hardcoded S$.F2 format violated architectural principles.");
        }

        // JSON response DTOs
        private class TransactionResponse
        {
            public bool Success { get; set; }
            public string Error { get; set; } = "";

            [System.Text.Json.Serialization.JsonPropertyName("transaction_id")]
            public string TransactionId { get; set; } = "";

            public double Total { get; set; }
            public double Tendered { get; set; }
            public double Change { get; set; }
            public string State { get; set; } = "";

            // CRITICAL FIX: Add JsonPropertyName to match the snake_case JSON from Rust service
            [System.Text.Json.Serialization.JsonPropertyName("line_items")]
            public List<LineItemDto> LineItems { get; set; } = new();
        }

        private class LineItemDto
        {
            // CRITICAL FIX: Add JsonPropertyName attributes to match Rust JSON format
            [System.Text.Json.Serialization.JsonPropertyName("line_item_id")]
            public string LineItemId { get; set; } = "";

            [System.Text.Json.Serialization.JsonPropertyName("line_number")]
            public int LineNumber { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("product_id")]
            public string ProductId { get; set; } = "";

            public int Quantity { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("unit_price")]
            public double UnitPrice { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("extended_price")]
            public double ExtendedPrice { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("parent_line_number")]
            public uint? ParentLineNumber { get; set; } // NRF COMPLIANCE: Use parent_line_number field from Rust

            // ARCHITECTURAL FIX: Add product metadata fields for AI display
            [System.Text.Json.Serialization.JsonPropertyName("product_name")]
            public string ProductName { get; set; } = "";

            [System.Text.Json.Serialization.JsonPropertyName("product_description")]
            public string ProductDescription { get; set; } = "";
        }

        private void EnsureConnected()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(RustKernelClient));
            }

            if (!_connected)
            {
                throw new InvalidOperationException("Not connected to Rust kernel service. Call ConnectAsync first.");
            }
        }

        /// <summary>
        /// Determines the line item type based on parent relationship (NRF compliance).
        /// </summary>
        /// <param name="parentLineItemId">The parent line item ID.</param>
        /// <returns>The appropriate LineItemType.</returns>
        private static LineItemType DetermineLineItemType(uint? parentLineItemId)
        {
            // NRF COMPLIANCE: Items with parents are modifications/components
            return parentLineItemId.HasValue ? LineItemType.Modification : LineItemType.Sale;
        }

        /// <summary>
        /// Determines the display indent level based on parent relationship (NRF compliance).
        /// </summary>
        /// <param name="parentLineItemId">The parent line item ID.</param>
        /// <returns>The appropriate indent level for hierarchical display.</returns>
        private static int DetermineIndentLevel(uint? parentLineItemId)
        {
            // NRF COMPLIANCE: Child items get indented for hierarchical display
            // TODO: For recursive hierarchy, this would need to calculate actual depth
            return parentLineItemId.HasValue ? 1 : 0;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (!_disposed)
            {
                _logger.LogDebug("Disposing RustKernelClient");

                try
                {
                    DisconnectAsync().Wait();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error during dispose");
                }

                _httpClient?.Dispose();
                _disposed = true;
            }
        }

        /// <inheritdoc/>
        public Task<object> GetStoreConfigAsync(CancellationToken cancellationToken = default)
        {
            EnsureConnected();

            // ARCHITECTURAL FIX: FAIL FAST - No hardcoded store configuration
            throw new InvalidOperationException(
                "DESIGN DEFICIENCY: Store configuration must come from proper configuration service. " +
                "Rust kernel client cannot decide store configuration defaults. " +
                "Implement proper store configuration service that provides StoreId, Currency, and DecimalPlaces. " +
                "Current implementation hardcoded StoreId='RUST_STORE_01' and Currency='SGD' - this violates fail-fast principles.");
        }

        /// <summary>
        /// Adds a modification to a specific line item by its ID (for NRF-compliant hierarchical modifications)
        /// </summary>
        public async Task<TransactionClientResult> AddModificationByLineItemIdAsync(
            string sessionId,
            string transactionId,
            string lineItemId,
            string modificationSku,
            int quantity,
            decimal unitPrice,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var requestBody = new
                {
                    session_id = sessionId,
                    modification_sku = modificationSku,
                    quantity = quantity,
                    unit_price = unitPrice
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"/api/sessions/{sessionId}/transactions/{transactionId}/line-items/{lineItemId}/modifications",
                    content,
                    cancellationToken);

                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogInformation("RUST_SERVICE_JSON: Raw response length: {Length} characters", responseContent.Length);
                _logger.LogInformation("RUST_SERVICE_JSON: Content: {Content}", responseContent);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to add modification to line item {LineItemId}: {StatusCode} - {Content}",
                        lineItemId, response.StatusCode, responseContent);

                    return new TransactionClientResult
                    {
                        Success = false,
                        Error = $"HTTP {response.StatusCode}: {responseContent}"
                    };
                }

                // Parse the response using existing method
                var result = ParseTransactionResponse(responseContent);

                return new TransactionClientResult
                {
                    Success = result.Success,
                    TransactionId = result.TransactionId,
                    Total = (decimal)result.Total,
                    State = result.State,
                    LineItems = result.LineItems?.Select(item => new TransactionLineItem
                    {
                        LineItemId = item.LineItemId,
                        LineNumber = item.LineNumber,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        UnitPrice = (decimal)item.UnitPrice,
                        ExtendedPrice = (decimal)item.ExtendedPrice,
                        ParentLineNumber = (int)(item.ParentLineNumber ?? 0),
                        ItemType = DetermineLineItemType(item.ParentLineNumber),
                        DisplayIndentLevel = DetermineIndentLevel(item.ParentLineNumber),
                        IsVoided = false,
                        VoidReason = null,
                        Metadata = new Dictionary<string, string>()
                    }).ToList() ?? new List<TransactionLineItem>(),
                    Error = result.Error
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding modification to line item by ID");
                throw;
            }
        }

        /// <summary>
        /// Modifies a line item by its ID (for NRF-compliant hierarchical modifications)
        /// </summary>
        public async Task<TransactionClientResult> ModifyLineItemByIdAsync(
            string sessionId,
            string transactionId,
            string lineItemId,
            string modificationType,
            string newValue,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var requestBody = new
                {
                    session_id = sessionId,
                    transaction_id = transactionId,
                    line_item_id = lineItemId,
                    modification_type = modificationType,
                    new_value = newValue
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync(
                    $"/api/sessions/{sessionId}/transactions/{transactionId}/line-items/{lineItemId}/modify",
                    content,
                    cancellationToken);

                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    return new TransactionClientResult
                    {
                        Success = false,
                        Error = $"HTTP {response.StatusCode}: {responseContent}"
                    };
                }

                // Parse the response using existing method
                var result = ParseTransactionResponse(responseContent);

                return new TransactionClientResult
                {
                    Success = result.Success,
                    TransactionId = result.TransactionId,
                    Total = (decimal)result.Total,
                    State = result.State,
                    LineItems = result.LineItems?.Select(item => new TransactionLineItem
                    {
                        LineItemId = item.LineItemId,
                        LineNumber = item.LineNumber,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        UnitPrice = (decimal)item.UnitPrice,
                        ExtendedPrice = (decimal)item.ExtendedPrice,
                        ParentLineNumber = (int)(item.ParentLineNumber ?? 0),
                        ItemType = DetermineLineItemType(item.ParentLineNumber),
                        DisplayIndentLevel = DetermineIndentLevel(item.ParentLineNumber),
                        IsVoided = false,
                        VoidReason = null,
                        Metadata = new Dictionary<string, string>()
                    }).ToList() ?? new List<TransactionLineItem>(),
                    Error = result.Error
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error modifying line item by ID");
                throw;
            }
        }

        /// <summary>
        /// Voids a line item by its ID (for NRF-compliant hierarchical modifications)
        /// </summary>
        public async Task<TransactionClientResult> VoidLineItemByIdAsync(
            string sessionId,
            string transactionId,
            string lineItemId,
            string reason,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _httpClient.PostAsync(
                    $"/api/sessions/{sessionId}/transactions/{transactionId}/line-items/{lineItemId}/void",
                    null,
                    cancellationToken);

                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    return new TransactionClientResult
                    {
                        Success = false,
                        Error = $"HTTP {response.StatusCode}: {responseContent}"
                    };
                }

                // Parse the response using existing method
                var result = ParseTransactionResponse(responseContent);

                return new TransactionClientResult
                {
                    Success = result.Success,
                    TransactionId = result.TransactionId,
                    Total = (decimal)result.Total,
                    State = result.State,
                    LineItems = result.LineItems?.Select(item => new TransactionLineItem
                    {
                        LineItemId = item.LineItemId,
                        LineNumber = item.LineNumber,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        UnitPrice = (decimal)item.UnitPrice,
                        ExtendedPrice = (decimal)item.ExtendedPrice,
                        ParentLineNumber = (int)(item.ParentLineNumber ?? 0),
                        ItemType = DetermineLineItemType(item.ParentLineNumber),
                        DisplayIndentLevel = DetermineIndentLevel(item.ParentLineNumber),
                        IsVoided = false,
                        VoidReason = null,
                        Metadata = new Dictionary<string, string>()
                    }).ToList() ?? new List<TransactionLineItem>(),
                    Error = result.Error
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error voiding line item by ID");
                throw;
            }
        }
    }

    /// <summary>
    /// Configuration options for the Rust kernel HTTP client.
    /// </summary>
    public class RustKernelClientOptions
    {
        /// <summary>
        /// Gets or sets the HTTP service address.
        /// </summary>
        public string Address { get; set; } = "http://localhost:8080";

        /// <summary>
        /// Gets or sets the connection timeout in milliseconds.
        /// </summary>
        public int ConnectionTimeoutMs { get; set; } = 5000;

        /// <summary>
        /// Gets or sets the request timeout in milliseconds.
        /// </summary>
        public int RequestTimeoutMs { get; set; } = 30000;
    }
}
