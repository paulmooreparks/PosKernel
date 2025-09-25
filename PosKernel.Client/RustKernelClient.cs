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
using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PosKernel.Client.Rust
{
    /// <summary>
    /// HTTP client adapter for the Rust POS Kernel Service.
    /// Implements IPosKernelClient to provide a seamless interface to the AI demo.
    /// </summary>
    public class RustKernelClient : IPosKernelClient
    {
        private readonly ILogger<RustKernelClient> _logger;
        private readonly RustKernelClientOptions _options;
        private readonly HttpClient _httpClient;
        private bool _disposed = false;
        private bool _connected = false;

        /// <summary>
        /// Initializes a new instance of the RustKernelClient.
        /// </summary>
        /// <param name="logger">Logger for diagnostics.</param>
        /// <param name="options">Client configuration options.</param>
        public RustKernelClient(ILogger<RustKernelClient> logger, RustKernelClientOptions? options = null)
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

            _logger.LogInformation("Connecting to Rust POS Kernel Service at {Address}", _options.Address);

            try
            {
                // Test connection with health check
                var response = await _httpClient.GetAsync("/health", cancellationToken);
                response.EnsureSuccessStatusCode();
                
                var healthJson = await response.Content.ReadAsStringAsync(cancellationToken);
                var healthData = JsonSerializer.Deserialize<JsonElement>(healthJson);
                
                if (healthData.TryGetProperty("healthy", out var healthyProp) && healthyProp.GetBoolean())
                {
                    _connected = true;
                    _logger.LogInformation("✅ Connected to Rust POS Kernel Service successfully");
                }
                else
                {
                    throw new InvalidOperationException("Rust kernel service reported unhealthy status");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to Rust POS Kernel Service");
                throw new InvalidOperationException("Unable to connect to Rust POS Kernel Service", ex);
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
        public async Task<TransactionClientResult> StartTransactionAsync(string sessionId, string currency = "USD", CancellationToken cancellationToken = default)
        {
            EnsureConnected();

            _logger.LogDebug("Starting transaction in session {SessionId} with currency {Currency}", sessionId, currency);

            try
            {
                var request = new
                {
                    session_id = sessionId,
                    store = "DEFAULT_STORE",
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
        public async Task<TransactionClientResult> AddLineItemAsync(string sessionId, string transactionId, string productId, int quantity, decimal unitPrice, CancellationToken cancellationToken = default)
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
                    unit_price = (double)unitPrice
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
        public async Task<TransactionClientResult> AddModificationAsync(string sessionId, string transactionId, int parentLineNumber, string modificationId, int quantity, decimal unitPrice, LineItemType itemType = LineItemType.Modification, CancellationToken cancellationToken = default)
        {
            EnsureConnected();

            _logger.LogDebug("Adding modification {ModificationId} x{Quantity} @ {UnitPrice:C} to line {ParentLineNumber} in transaction {TransactionId}", 
                modificationId, quantity, unitPrice, parentLineNumber, transactionId);

            try
            {
                var request = new
                {
                    session_id = sessionId,
                    transaction_id = transactionId,
                    parent_line_number = parentLineNumber,
                    modification_id = modificationId,
                    quantity = quantity,
                    unit_price = (double)unitPrice,
                    item_type = itemType.ToString().ToUpperInvariant()
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"/api/sessions/{sessionId}/transactions/{transactionId}/modifications", content, cancellationToken);
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
        public async Task<TransactionClientResult> UpdateLineItemPreparationNotesAsync(string sessionId, string transactionId, int lineNumber, string preparationNotes, CancellationToken cancellationToken = default)
        {
            EnsureConnected();

            _logger.LogDebug("Updating preparation notes for line {LineNumber} in transaction {TransactionId}: {PreparationNotes}", 
                lineNumber, transactionId, preparationNotes);

            try
            {
                // ARCHITECTURAL PRINCIPLE: This is critical for set meal customization - update existing line item
                var request = new
                {
                    preparation_notes = preparationNotes
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync($"/api/sessions/{sessionId}/transactions/{transactionId}/lines/{lineNumber}/notes", content, cancellationToken);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                var responseData = JsonSerializer.Deserialize<JsonElement>(responseJson);

                var result = ParseTransactionResponse(responseData, sessionId, transactionId);

                if (result.Success)
                {
                    _logger.LogInformation("Preparation notes updated for line {LineNumber} in transaction {TransactionId}: {PreparationNotes}", 
                        lineNumber, result.TransactionId, preparationNotes);
                }
                else
                {
                    _logger.LogWarning("Failed to update preparation notes: {Error}", result.Error);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating preparation notes");
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

                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                var responseData = JsonSerializer.Deserialize<JsonElement>(responseJson);

                return new TransactionClientResult
                {
                    Success = responseData.GetProperty("success").GetBoolean(),
                    Error = responseData.TryGetProperty("error", out var errorProp) ? errorProp.GetString() : null,
                    SessionId = responseData.TryGetProperty("session_id", out var sidProp) ? sidProp.GetString() : sessionId,
                    TransactionId = responseData.TryGetProperty("transaction_id", out var tidProp) ? tidProp.GetString() : transactionId,
                    Total = responseData.TryGetProperty("total", out var totalProp) ? (decimal)totalProp.GetDouble() : 0,
                    State = responseData.TryGetProperty("state", out var stateProp) ? stateProp.GetString() ?? "" : "",
                    Data = responseData
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting transaction");
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
        public Task CloseSessionAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            if (_disposed)
            {
                return Task.CompletedTask;
            }

            _logger.LogDebug("Closing session {SessionId}", sessionId);
            // For now, just log the session close - the Rust service doesn't need explicit session cleanup
            _logger.LogInformation("Session {SessionId} closed", sessionId);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Parses a transaction response from the Rust kernel service.
        /// ARCHITECTURAL PRINCIPLE: Centralized response parsing with NRF-compliant line item support.
        /// </summary>
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

            // Parse line items with NRF hierarchical support
            if (responseData.TryGetProperty("line_items", out var lineItemsProp) && lineItemsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var itemElement in lineItemsProp.EnumerateArray())
                {
                    var lineItem = new TransactionLineItem
                    {
                        LineNumber = itemElement.TryGetProperty("line_number", out var lnProp) ? lnProp.GetInt32() : 0,
                        ParentLineNumber = itemElement.TryGetProperty("parent_line_number", out var plnProp) ? plnProp.GetInt32() : 0,
                        ProductId = itemElement.TryGetProperty("product_id", out var pidProp) ? pidProp.GetString() ?? "" : "",
                        ItemType = itemElement.TryGetProperty("item_type", out var itProp) ? ParseLineItemType(itProp.GetString()) : LineItemType.BaseProduct,
                        Quantity = itemElement.TryGetProperty("quantity", out var qtyProp) ? qtyProp.GetInt32() : 0,
                        UnitPrice = itemElement.TryGetProperty("unit_price_minor", out var upProp) ? (decimal)upProp.GetInt64() / 100 : 0,
                        ExtendedPrice = itemElement.TryGetProperty("extended_price_minor", out var epProp) ? (decimal)epProp.GetInt64() / 100 : 0,
                        DisplayIndentLevel = itemElement.TryGetProperty("display_indent_level", out var dilProp) ? dilProp.GetInt32() : 0,
                        IsVoided = itemElement.TryGetProperty("is_voided", out var voidProp) ? voidProp.GetBoolean() : false,
                        VoidReason = itemElement.TryGetProperty("void_reason", out var vrProp) ? vrProp.GetString() : null,
                        PreparationNotes = itemElement.TryGetProperty("preparation_notes", out var pnProp) ? pnProp.GetString() ?? "" : ""
                    };

                    result.LineItems.Add(lineItem);
                }
            }

            return result;
        }

        /// <summary>
        /// Parses LineItemType from string representation.
        /// </summary>
        private LineItemType ParseLineItemType(string? itemTypeString)
        {
            if (string.IsNullOrEmpty(itemTypeString))
            {
                return LineItemType.BaseProduct;
            }

            return itemTypeString.ToUpperInvariant() switch
            {
                "BASE_PRODUCT" => LineItemType.BaseProduct,
                "MODIFICATION" => LineItemType.Modification,
                "AUTOMATIC_INCLUSION" => LineItemType.AutomaticInclusion,
                "DISCOUNT" => LineItemType.Discount,
                "TAX" => LineItemType.Tax,
                "FEE" => LineItemType.Fee,
                _ => LineItemType.BaseProduct
            };
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
