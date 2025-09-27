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
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace PosKernel.Client
{
    /// <summary>
    /// Named pipe client implementation for communicating with POS Kernel Service.
    /// Provides automatic reconnection, retry logic, and connection pooling.
    /// </summary>
    public class PosKernelClient : IPosKernelClient
    {
        private readonly ILogger<PosKernelClient> _logger;
        private readonly PosKernelClientOptions _options;
        private NamedPipeClientStream? _pipeClient;
        private StreamWriter? _writer;
        private StreamReader? _reader;
        private readonly object _lockObject = new();
        private int _requestId = 0;
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of the PosKernelClient class.
        /// </summary>
        /// <param name="logger">Logger for debugging and diagnostics.</param>
        /// <param name="options">Optional client configuration settings.</param>
        public PosKernelClient(ILogger<PosKernelClient> logger, PosKernelClientOptions? options = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? new PosKernelClientOptions();
        }

        /// <summary>
        /// Gets a value indicating whether the client is connected to the POS Kernel Service.
        /// </summary>
        public bool IsConnected => _pipeClient?.IsConnected == true;

        /// <summary>
        /// Connects to the POS Kernel Service asynchronously.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the connection operation.</param>
        /// <returns>A task representing the asynchronous connection operation.</returns>
        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (IsConnected)
            {
                _logger.LogDebug("Already connected to POS Kernel Service");
                return;
            }

            try
            {
                _logger.LogInformation("Connecting to POS Kernel Service on pipe: {PipeName}", _options.PipeName);

                _pipeClient = new NamedPipeClientStream(".", _options.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                
                using var timeoutCts = new CancellationTokenSource(_options.ConnectionTimeoutMs);
                using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                await _pipeClient.ConnectAsync(combinedCts.Token);

                _writer = new StreamWriter(_pipeClient, Encoding.UTF8) { AutoFlush = true };
                _reader = new StreamReader(_pipeClient, Encoding.UTF8);

                _logger.LogInformation("Connected to POS Kernel Service successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to POS Kernel Service");
                await DisconnectAsync(cancellationToken);
                throw;
            }
        }

        /// <summary>
        /// Disconnects from the POS Kernel Service and releases resources.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the disconnect operation.</param>
        /// <returns>A task representing the asynchronous disconnect operation.</returns>
        public async Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Disconnecting from POS Kernel Service");

                _writer?.Dispose();
                _reader?.Dispose();
                _pipeClient?.Dispose();

                _writer = null;
                _reader = null;
                _pipeClient = null;

                _logger.LogInformation("Disconnected from POS Kernel Service");
                
                // Add await to fix CS1998 warning
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during disconnect");
            }
        }

        /// <summary>
        /// Synchronous version of DisconnectAsync for interface compatibility.
        /// </summary>
        public void Disconnect()
        {
            try
            {
                DisconnectAsync(CancellationToken.None).Wait(5000);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during synchronous disconnect");
            }
        }

        /// <summary>
        /// Creates a new transaction session on the POS Kernel Service.
        /// </summary>
        /// <param name="terminalId">The terminal identifier.</param>
        /// <param name="operatorId">The operator identifier.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The session identifier for the new session.</returns>
        public async Task<string> CreateSessionAsync(string terminalId, string operatorId, CancellationToken cancellationToken = default)
        {
            var request = new
            {
                jsonrpc = "2.0",
                method = "create_session",
                @params = new
                {
                    terminal_id = terminalId,
                    operator_id = operatorId
                },
                id = GetNextRequestId()
            };

            var response = await SendRequestAsync<dynamic>(request, cancellationToken);
            
            if (response?.session_id is string sessionId)
            {
                return sessionId;
            }

            throw new InvalidOperationException("Failed to create session - invalid response");
        }

        /// <summary>
        /// Starts a new transaction within the specified session.
        /// </summary>
        /// <param name="sessionId">The session identifier.</param>
        /// <param name="currency">The transaction currency (default: USD).</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The result of the start transaction operation.</returns>
        public async Task<TransactionClientResult> StartTransactionAsync(string sessionId, string currency = "USD", CancellationToken cancellationToken = default)
        {
            var request = new
            {
                jsonrpc = "2.0",
                method = "start_transaction",
                @params = new
                {
                    session_id = sessionId,
                    currency = currency
                },
                id = GetNextRequestId()
            };

            var response = await SendRequestAsync<TransactionClientResult>(request, cancellationToken);
            return response ?? new TransactionClientResult { Success = false, Error = "Invalid response" };
        }

        /// <summary>
        /// Adds a line item to the current transaction.
        /// </summary>
        /// <param name="sessionId">The session identifier.</param>
        /// <param name="transactionId">The transaction identifier.</param>
        /// <param name="productId">The product identifier or SKU.</param>
        /// <param name="quantity">The quantity of items to add.</param>
        /// <param name="unitPrice">The unit price of the item.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The result of the add line item operation.</returns>
        public async Task<TransactionClientResult> AddLineItemAsync(string sessionId, string transactionId, string productId, int quantity, decimal unitPrice, CancellationToken cancellationToken = default)
        {
            var request = new
            {
                jsonrpc = "2.0",
                method = "add_line_item",
                @params = new
                {
                    session_id = sessionId,
                    transaction_id = transactionId,
                    product_id = productId,
                    quantity = quantity,
                    unit_price = unitPrice
                },
                id = GetNextRequestId()
            };

            var response = await SendRequestAsync<TransactionClientResult>(request, cancellationToken);
            return response ?? new TransactionClientResult { Success = false, Error = "Invalid response" };
        }

        /// <summary>
        /// Adds a modification to an existing line item (NRF-compliant hierarchical).
        /// </summary>
        /// <param name="sessionId">The session identifier.</param>
        /// <param name="transactionId">The transaction identifier.</param>
        /// <param name="parentLineNumber">The parent line number to add the modification to.</param>
        /// <param name="modificationId">The modification identifier.</param>
        /// <param name="quantity">The quantity of modifications to add.</param>
        /// <param name="unitPrice">The unit price of the modification.</param>
        /// <param name="itemType">The type of line item (default: Modification).</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The result of the add modification operation.</returns>
        public async Task<TransactionClientResult> AddModificationAsync(string sessionId, string transactionId, int parentLineNumber, string modificationId, int quantity, decimal unitPrice, LineItemType itemType = LineItemType.Modification, CancellationToken cancellationToken = default)
        {
            var request = new
            {
                jsonrpc = "2.0",
                method = "add_modification",
                @params = new
                {
                    session_id = sessionId,
                    transaction_id = transactionId,
                    parent_line_number = parentLineNumber,
                    modification_id = modificationId,
                    quantity = quantity,
                    unit_price = unitPrice,
                    item_type = itemType.ToString()
                },
                id = GetNextRequestId()
            };

            var response = await SendRequestAsync<TransactionClientResult>(request, cancellationToken);
            return response ?? new TransactionClientResult { Success = false, Error = "Invalid response" };
        }

        /// <summary>
        /// Voids a line item from the current transaction by line number.
        /// Creates a reversing entry to maintain audit trail compliance.
        /// </summary>
        /// <param name="sessionId">The session identifier.</param>
        /// <param name="transactionId">The transaction identifier.</param>
        /// <param name="lineNumber">The line number to void (1-based).</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The result of the void line item operation.</returns>
        public async Task<TransactionClientResult> VoidLineItemAsync(string sessionId, string transactionId, int lineNumber, CancellationToken cancellationToken = default)
        {
            var request = new
            {
                jsonrpc = "2.0",
                method = "void_line_item",
                @params = new
                {
                    session_id = sessionId,
                    transaction_id = transactionId,
                    line_number = lineNumber
                },
                id = GetNextRequestId()
            };

            var response = await SendRequestAsync<TransactionClientResult>(request, cancellationToken);
            return response ?? new TransactionClientResult { Success = false, Error = "Invalid response" };
        }

        /// <summary>
        /// Updates the quantity of a line item in the current transaction.
        /// </summary>
        /// <param name="sessionId">The session identifier.</param>
        /// <param name="transactionId">The transaction identifier.</param>
        /// <param name="lineNumber">The line number to update (1-based).</param>
        /// <param name="newQuantity">The new quantity for the line item.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The result of the update line item operation.</returns>
        public async Task<TransactionClientResult> UpdateLineItemQuantityAsync(string sessionId, string transactionId, int lineNumber, int newQuantity, CancellationToken cancellationToken = default)
        {
            var request = new
            {
                jsonrpc = "2.0",
                method = "update_line_item_quantity",
                @params = new
                {
                    session_id = sessionId,
                    transaction_id = transactionId,
                    line_number = lineNumber,
                    new_quantity = newQuantity
                },
                id = GetNextRequestId()
            };

            var response = await SendRequestAsync<TransactionClientResult>(request, cancellationToken);
            return response ?? new TransactionClientResult { Success = false, Error = "Invalid response" };
        }

        /// <summary>
        /// Processes payment for the specified transaction.
        /// </summary>
        /// <param name="sessionId">The session identifier.</param>
        /// <param name="transactionId">The transaction identifier.</param>
        /// <param name="amount">The payment amount.</param>
        /// <param name="paymentType">The type of payment (default: cash).</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The result of the payment processing operation.</returns>
        public async Task<TransactionClientResult> ProcessPaymentAsync(string sessionId, string transactionId, decimal amount, string paymentType = "cash", CancellationToken cancellationToken = default)
        {
            var request = new
            {
                jsonrpc = "2.0",
                method = "process_payment",
                @params = new
                {
                    session_id = sessionId,
                    transaction_id = transactionId,
                    amount = amount,
                    payment_type = paymentType
                },
                id = GetNextRequestId()
            };

            var response = await SendRequestAsync<TransactionClientResult>(request, cancellationToken);
            return response ?? new TransactionClientResult { Success = false, Error = "Invalid response" };
        }

        /// <summary>
        /// Gets the current state and details of the specified transaction.
        /// </summary>
        /// <param name="sessionId">The session identifier.</param>
        /// <param name="transactionId">The transaction identifier.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The current transaction state and details.</returns>
        public async Task<TransactionClientResult> GetTransactionAsync(string sessionId, string transactionId, CancellationToken cancellationToken = default)
        {
            var request = new
            {
                jsonrpc = "2.0",
                method = "get_transaction",
                @params = new
                {
                    session_id = sessionId,
                    transaction_id = transactionId
                },
                id = GetNextRequestId()
            };

            var response = await SendRequestAsync<TransactionClientResult>(request, cancellationToken);
            return response ?? new TransactionClientResult { Success = false, Error = "Invalid response" };
        }

        /// <summary>
        /// Closes the specified session and releases associated resources.
        /// </summary>
        /// <param name="sessionId">The session identifier to close.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous close session operation.</returns>
        public async Task CloseSessionAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            var request = new
            {
                jsonrpc = "2.0",
                method = "close_session",
                @params = new
                {
                    session_id = sessionId
                },
                id = GetNextRequestId()
            };

            await SendRequestAsync<dynamic>(request, cancellationToken);
        }

        /// <summary>
        /// Gets store configuration from the POS Kernel Service.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The store configuration object.</returns>
        public async Task<object> GetStoreConfigAsync(CancellationToken cancellationToken = default)
        {
            var request = new
            {
                jsonrpc = "2.0",
                method = "get_store_config",
                @params = new { },
                id = GetNextRequestId()
            };

            var response = await SendRequestAsync<object>(request, cancellationToken);
            return response ?? new { };
        }

        /// <summary>
        /// Adds a child line item to the current transaction (NRF hierarchical line items).
        /// </summary>
        /// <param name="sessionId">The session identifier.</param>
        /// <param name="transactionId">The transaction identifier.</param>
        /// <param name="productId">The product identifier or SKU.</param>
        /// <param name="quantity">The quantity of items to add.</param>
        /// <param name="unitPrice">The unit price of the item.</param>
        /// <param name="parentLineNumber">The parent line number this item modifies.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The result of the add child line item operation.</returns>
        public async Task<TransactionClientResult> AddChildLineItemAsync(string sessionId, string transactionId, string productId, int quantity, decimal unitPrice, int parentLineNumber, CancellationToken cancellationToken = default)
        {
            var request = new
            {
                jsonrpc = "2.0",
                method = "add_child_line_item",
                @params = new
                {
                    session_id = sessionId,
                    transaction_id = transactionId,
                    product_id = productId,
                    quantity = quantity,
                    unit_price = unitPrice,
                    parent_line_number = parentLineNumber
                },
                id = GetNextRequestId()
            };

            var response = await SendRequestAsync<TransactionClientResult>(request, cancellationToken);
            return response ?? new TransactionClientResult { Success = false, Error = "Invalid response" };
        }

        private async Task<T?> SendRequestAsync<T>(object request, CancellationToken cancellationToken = default)
        {
            for (int attempt = 0; attempt <= _options.MaxRetryAttempts; attempt++)
            {
                try
                {
                    if (!IsConnected && _options.AutoReconnect)
                    {
                        await ConnectAsync(cancellationToken);
                    }

                    if (!IsConnected)
                    {
                        throw new InvalidOperationException("Not connected to POS Kernel Service");
                    }

                    return await SendRequestInternalAsync<T>(request, cancellationToken);
                }
                catch (Exception ex) when (attempt < _options.MaxRetryAttempts)
                {
                    _logger.LogWarning(ex, "Request failed, attempt {Attempt}/{MaxAttempts}, retrying...", 
                        attempt + 1, _options.MaxRetryAttempts + 1);

                    await DisconnectAsync(cancellationToken);
                    await Task.Delay(_options.RetryDelayMs, cancellationToken);
                }
            }

            // Final attempt without catching exceptions
            return await SendRequestInternalAsync<T>(request, cancellationToken);
        }

        private async Task<T?> SendRequestInternalAsync<T>(object request, CancellationToken cancellationToken = default)
        {
            lock (_lockObject)
            {
                if (_writer == null || _reader == null)
                {
                    throw new InvalidOperationException("Client not properly connected");
                }
            }

            using var timeoutCts = new CancellationTokenSource(_options.RequestTimeoutMs);
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            // Serialize and send request
            var requestJson = JsonSerializer.Serialize(request);
            _logger.LogDebug("Sending request: {Request}", requestJson);

            await _writer!.WriteLineAsync(requestJson.AsMemory(), combinedCts.Token);

            // Read response
            var responseJson = await _reader!.ReadLineAsync(combinedCts.Token);
            if (responseJson == null)
            {
                throw new InvalidOperationException("Connection closed by server");
            }

            _logger.LogDebug("Received response: {Response}", responseJson);

            // Parse JSON-RPC response
            var jsonResponse = JsonSerializer.Deserialize<JsonRpcClientResponse>(responseJson);
            if (jsonResponse == null)
            {
                throw new InvalidOperationException("Invalid JSON-RPC response");
            }

            if (jsonResponse.Error != null)
            {
                throw new InvalidOperationException($"Server error: {jsonResponse.Error.Code} - {jsonResponse.Error.Message}");
            }

            if (jsonResponse.Result == null)
            {
                return default(T);
            }

            // Convert result to requested type
            if (typeof(T) == typeof(object))
            {
                return (T)(object)jsonResponse.Result;
            }

            var resultJson = JsonSerializer.Serialize(jsonResponse.Result);
            return JsonSerializer.Deserialize<T>(resultJson);
        }

        private int GetNextRequestId()
        {
            return Interlocked.Increment(ref _requestId);
        }

        /// <summary>
        /// Releases all resources used by the PosKernelClient.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                try
                {
                    DisconnectAsync(CancellationToken.None).Wait(5000);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during dispose");
                }
                finally
                {
                    _disposed = true;
                }
            }
        }
    }

    // JSON-RPC Client Response DTOs

    /// <summary>
    /// Represents a JSON-RPC response from the POS Kernel Service.
    /// </summary>
    public class JsonRpcClientResponse
    {
        /// <summary>
        /// Gets or sets the JSON-RPC version.
        /// </summary>
        public string JsonRpc { get; set; } = "";
        
        /// <summary>
        /// Gets or sets the response result data.
        /// </summary>
        public object? Result { get; set; }
        
        /// <summary>
        /// Gets or sets the error information if the request failed.
        /// </summary>
        public JsonRpcClientError? Error { get; set; }
        
        /// <summary>
        /// Gets or sets the request identifier that this response corresponds to.
        /// </summary>
        public object? Id { get; set; }
    }

    /// <summary>
    /// Represents error information in a JSON-RPC response.
    /// </summary>
    public class JsonRpcClientError
    {
        /// <summary>
        /// Gets or sets the error code.
        /// </summary>
        public string Code { get; set; } = "";
        
        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        public string Message { get; set; } = "";
        
        /// <summary>
        /// Gets or sets additional error data.
        /// </summary>
        public object? Data { get; set; }
    }
}
