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
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace PosKernel.Service.Services {
    /// <summary>
    /// Named pipe server implementation for high-performance local IPC communication.
    /// Handles multiple concurrent client connections with JSON-RPC protocol.
    /// </summary>
    public class NamedPipeServer : INamedPipeServer, IDisposable {
        private readonly ILogger<NamedPipeServer> _logger;
        private readonly PosKernelServiceOptions _options;
        private readonly IPosKernelEngine _kernelEngine;
        private readonly IMetricsCollector _metricsCollector;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly List<Task> _serverTasks;
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of the NamedPipeServer class.
        /// </summary>
        /// <param name="logger">Logger for debugging and diagnostics.</param>
        /// <param name="options">Service configuration options.</param>
        /// <param name="kernelEngine">The POS kernel engine for processing requests.</param>
        /// <param name="metricsCollector">Metrics collector for performance monitoring.</param>
        public NamedPipeServer(
            ILogger<NamedPipeServer> logger,
            IOptions<PosKernelServiceOptions> options,
            IPosKernelEngine kernelEngine,
            IMetricsCollector metricsCollector) {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _kernelEngine = kernelEngine ?? throw new ArgumentNullException(nameof(kernelEngine));
            _metricsCollector = metricsCollector ?? throw new ArgumentNullException(nameof(metricsCollector));
            _cancellationTokenSource = new CancellationTokenSource();
            _serverTasks = new List<Task>();
        }

        /// <summary>
        /// Starts the named pipe server for client connections.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous start operation.</returns>
        public async Task StartAsync(CancellationToken cancellationToken = default) {
            _logger.LogInformation("Starting Named Pipe Server on pipe: {PipeName}", _options.NamedPipe.PipeName);

            // Start background task to handle clients
            _ = Task.Run(() => HandleClientsAsync(cancellationToken), cancellationToken);

            // Add await to fix CS1998
            await Task.CompletedTask;
        }

        /// <summary>
        /// Stops the named pipe server and closes all active connections.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous stop operation.</returns>
        public async Task StopAsync(CancellationToken cancellationToken = default) {
            try {
                _logger.LogInformation("Stopping named pipe server...");

                _cancellationTokenSource.Cancel();

                // Wait for all server tasks to complete
                await Task.WhenAll(_serverTasks);

                _logger.LogInformation("Named pipe server stopped successfully");
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error stopping named pipe server");
                throw;
            }
        }

        private async Task HandleClientsAsync(CancellationToken cancellationToken) {
            try {
                _logger.LogInformation("Named Pipe Server is running. Waiting for client connections...");

                while (!cancellationToken.IsCancellationRequested) {
                    try {
                        using var pipeServer = new NamedPipeServerStream(
                            _options.NamedPipe.PipeName,
                            PipeDirection.InOut,
                            _options.NamedPipe.MaxServerInstances,
#if WINDOWS
                            PipeTransmissionMode.Message,
#else
                            PipeTransmissionMode.Byte,
#endif
                            PipeOptions.Asynchronous,
                            _options.NamedPipe.BufferSize,
                            _options.NamedPipe.BufferSize);

                        _logger.LogDebug("Waiting for client connection...");

                        // Wait for client connection
                        await pipeServer.WaitForConnectionAsync(cancellationToken);

                        _logger.LogDebug("Client connected");

                        // Handle the client connection
                        await HandleClientConnectionAsync(pipeServer, cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                        // Expected during shutdown
                        break;
                    }
                    catch (Exception ex) {
                        _logger.LogError(ex, "Error in named pipe server instance");

                        // Brief delay before retrying to avoid tight loop on persistent errors
                        await Task.Delay(1000, cancellationToken);
                    }
                }
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error in HandleClientsAsync");
            }
        }

        private async Task HandleClientConnectionAsync(NamedPipeServerStream pipeServer, CancellationToken cancellationToken) {
            try {
                using var reader = new StreamReader(pipeServer, Encoding.UTF8, false, _options.NamedPipe.BufferSize, true);
                using var writer = new StreamWriter(pipeServer, Encoding.UTF8, _options.NamedPipe.BufferSize, true) { AutoFlush = true };

                while (pipeServer.IsConnected && !cancellationToken.IsCancellationRequested) {
                    try {
                        // Read request
                        var requestJson = await reader.ReadLineAsync(cancellationToken);
                        if (requestJson == null) {
                            _logger.LogDebug("Client disconnected");
                            break;
                        }

                        _logger.LogDebug("Received request: {Request}", requestJson);

                        // Process request
                        var responseJson = await ProcessRequestAsync(requestJson, cancellationToken);

                        // Send response
                        await writer.WriteLineAsync(responseJson);

                        _logger.LogDebug("Sent response: {Response}", responseJson);
                    }
                    catch (IOException ex) {
                        _logger.LogDebug(ex, "Client connection closed");
                        break;
                    }
                    catch (Exception ex) {
                        _logger.LogError(ex, "Error processing client request");

                        // Send error response to client
                        try {
                            var errorResponse = CreateErrorResponse("internal_error", $"Internal server error: {ex.Message}");
                            await writer.WriteLineAsync(errorResponse);
                        }
                        catch (Exception writeEx) {
                            _logger.LogError(writeEx, "Failed to send error response");
                            break;
                        }
                    }
                }
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error handling client connection");
            }
        }

        /// <summary>
        /// Processes a JSON-RPC request from a client.
        /// </summary>
        /// <param name="requestJson">The JSON-RPC request string.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The JSON-RPC response as a string.</returns>
        public async Task<string> ProcessRequestAsync(string requestJson, CancellationToken cancellationToken = default) {
            var startTime = DateTime.UtcNow;
            string operation = "unknown";
            bool success = false;

            try {
                // Parse JSON-RPC request
                var request = JsonSerializer.Deserialize<JsonRpcRequest>(requestJson);
                if (request == null) {
                    return CreateErrorResponse("parse_error", "Invalid JSON-RPC request");
                }

                operation = request.Method;
                _logger.LogDebug("Processing request: {Method} with ID: {Id}", request.Method, request.Id);

                // Route request to appropriate kernel method
                var result = await RouteRequestAsync(request, cancellationToken);
                success = true;

                var response = new JsonRpcResponse {
                    Id = request.Id,
                    Result = result
                };

                return JsonSerializer.Serialize(response);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error processing request: {RequestJson}", requestJson);
                return CreateErrorResponse("internal_error", $"Internal server error: {ex.Message}");
            }
            finally {
                var duration = DateTime.UtcNow - startTime;
                _metricsCollector.RecordTransaction(operation, duration, success);
            }
        }

        private async Task<object?> RouteRequestAsync(JsonRpcRequest request, CancellationToken cancellationToken) {
            return request.Method switch {
                "create_session" => await HandleCreateSessionAsync(request.Params, cancellationToken),
                "start_transaction" => await HandleStartTransactionAsync(request.Params, cancellationToken),
                "add_line_item" => await HandleAddLineItemAsync(request.Params, cancellationToken),
                "process_payment" => await HandleProcessPaymentAsync(request.Params, cancellationToken),
                "get_transaction" => await HandleGetTransactionAsync(request.Params, cancellationToken),
                "close_session" => await HandleCloseSessionAsync(request.Params, cancellationToken),
                _ => throw new ArgumentException($"Unknown method: {request.Method}")
            };
        }

        private async Task<object?> HandleCreateSessionAsync(JsonElement? parameters, CancellationToken cancellationToken) {
            var terminalId = parameters?.GetProperty("terminal_id").GetString() ?? "unknown";
            var operatorId = parameters?.GetProperty("operator_id").GetString() ?? "system";

            var sessionId = await _kernelEngine.CreateSessionAsync(terminalId, operatorId, cancellationToken);
            return new { session_id = sessionId };
        }

        private async Task<object?> HandleStartTransactionAsync(JsonElement? parameters, CancellationToken cancellationToken) {
            var sessionId = parameters?.GetProperty("session_id").GetString() ?? throw new ArgumentException("session_id required");

            // ARCHITECTURAL PRINCIPLE: Client must NOT decide currency defaults - system must provide currency configuration
            if (!parameters.HasValue || !parameters.Value.TryGetProperty("currency", out var currencyProperty) || currencyProperty.ValueKind == JsonValueKind.Null)
            {
                throw new InvalidOperationException(
                    "DESIGN DEFICIENCY: StartTransaction requires currency parameter. " +
                    "Client cannot decide currency defaults. " +
                    "System must provide currency configuration via store settings or session context.");
            }

            var currency = currencyProperty.GetString();
            if (string.IsNullOrWhiteSpace(currency))
            {
                throw new ArgumentException("Currency parameter cannot be empty", nameof(parameters));
            }

            var result = await _kernelEngine.StartTransactionAsync(sessionId, currency, cancellationToken);
            return result;
        }

        private async Task<object?> HandleAddLineItemAsync(JsonElement? parameters, CancellationToken cancellationToken) {
            var sessionId = parameters?.GetProperty("session_id").GetString() ?? throw new ArgumentException("session_id required");
            var transactionId = parameters?.GetProperty("transaction_id").GetString() ?? throw new ArgumentException("transaction_id required");
            var productId = parameters?.GetProperty("product_id").GetString() ?? throw new ArgumentException("product_id required");
            var quantity = parameters?.GetProperty("quantity").GetInt32() ?? 1;
            var unitPrice = parameters?.GetProperty("unit_price").GetDecimal() ?? throw new ArgumentException("unit_price required");

            var result = await _kernelEngine.AddLineItemAsync(sessionId, transactionId, productId, quantity, unitPrice, cancellationToken);
            return result;
        }

        private async Task<object?> HandleProcessPaymentAsync(JsonElement? parameters, CancellationToken cancellationToken) {
            var sessionId = parameters?.GetProperty("session_id").GetString() ?? throw new ArgumentException("session_id required");
            var transactionId = parameters?.GetProperty("transaction_id").GetString() ?? throw new ArgumentException("transaction_id required");
            var amount = parameters?.GetProperty("amount").GetDecimal() ?? throw new ArgumentException("amount required");
            var paymentType = parameters?.GetProperty("payment_type").GetString() ?? "cash";

            var result = await _kernelEngine.ProcessPaymentAsync(sessionId, transactionId, amount, paymentType, cancellationToken);
            return result;
        }

        private async Task<object?> HandleGetTransactionAsync(JsonElement? parameters, CancellationToken cancellationToken) {
            var sessionId = parameters?.GetProperty("session_id").GetString() ?? throw new ArgumentException("session_id required");
            var transactionId = parameters?.GetProperty("transaction_id").GetString() ?? throw new ArgumentException("transaction_id required");

            var result = await _kernelEngine.GetTransactionAsync(sessionId, transactionId, cancellationToken);
            return result;
        }

        private async Task<object?> HandleCloseSessionAsync(JsonElement? parameters, CancellationToken cancellationToken) {
            var sessionId = parameters?.GetProperty("session_id").GetString() ?? throw new ArgumentException("session_id required");

            await _kernelEngine.CloseSessionAsync(sessionId, cancellationToken);
            return new { success = true };
        }

        private static string CreateErrorResponse(string code, string message, object? id = null) {
            var error = new JsonRpcError {
                Code = code,
                Message = message
            };

            var response = new JsonRpcResponse {
                Id = id,
                Error = error
            };

            return JsonSerializer.Serialize(response);
        }

        /// <summary>
        /// Releases all resources used by the NamedPipeServer.
        /// </summary>
        public void Dispose() {
            if (!_disposed) {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _disposed = true;
            }
        }
    }

    // JSON-RPC Data Transfer Objects

    /// <summary>
    /// Represents a JSON-RPC request message.
    /// </summary>
    public class JsonRpcRequest {
        /// <summary>
        /// Gets or sets the JSON-RPC protocol version.
        /// </summary>
        public string JsonRpc { get; set; } = "2.0";

        /// <summary>
        /// Gets or sets the method name to invoke.
        /// </summary>
        public string Method { get; set; } = "";

        /// <summary>
        /// Gets or sets the method parameters.
        /// </summary>
        public JsonElement? Params { get; set; }

        /// <summary>
        /// Gets or sets the request identifier.
        /// </summary>
        public object? Id { get; set; }
    }

    /// <summary>
    /// Represents a JSON-RPC response message.
    /// </summary>
    public class JsonRpcResponse {
        /// <summary>
        /// Gets or sets the JSON-RPC protocol version.
        /// </summary>
        public string JsonRpc { get; set; } = "2.0";

        /// <summary>
        /// Gets or sets the result data for successful responses.
        /// </summary>
        public object? Result { get; set; }

        /// <summary>
        /// Gets or sets the error information for failed responses.
        /// </summary>
        public JsonRpcError? Error { get; set; }

        /// <summary>
        /// Gets or sets the request identifier that this response corresponds to.
        /// </summary>
        public object? Id { get; set; }
    }

    /// <summary>
    /// Represents error information in a JSON-RPC response.
    /// </summary>
    public class JsonRpcError {
        /// <summary>
        /// Gets or sets the error code.
        /// </summary>
        public string Code { get; set; } = "";

        /// <summary>
        /// Gets or sets the human-readable error message.
        /// </summary>
        public string Message { get; set; } = "";

        /// <summary>
        /// Gets or sets additional error data.
        /// </summary>
        public object? Data { get; set; }
    }
}
