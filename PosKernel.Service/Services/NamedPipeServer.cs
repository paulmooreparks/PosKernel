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

namespace PosKernel.Service.Services
{
    /// <summary>
    /// Named pipe server implementation for high-performance local IPC communication.
    /// Handles multiple concurrent client connections with JSON-RPC protocol.
    /// </summary>
    public class NamedPipeServer : INamedPipeServer, IDisposable
    {
        private readonly ILogger<NamedPipeServer> _logger;
        private readonly PosKernelServiceOptions _options;
        private readonly IPosKernelEngine _kernelEngine;
        private readonly IMetricsCollector _metricsCollector;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly List<Task> _serverTasks;
        private bool _disposed = false;

        public NamedPipeServer(
            ILogger<NamedPipeServer> logger,
            IOptions<PosKernelServiceOptions> options,
            IPosKernelEngine kernelEngine,
            IMetricsCollector metricsCollector)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _kernelEngine = kernelEngine ?? throw new ArgumentNullException(nameof(kernelEngine));
            _metricsCollector = metricsCollector ?? throw new ArgumentNullException(nameof(metricsCollector));
            _cancellationTokenSource = new CancellationTokenSource();
            _serverTasks = new List<Task>();
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting named pipe server on pipe: {PipeName}", _options.NamedPipe.PipeName);

                // Create multiple server instances for concurrent connections
                for (int i = 0; i < _options.NamedPipe.MaxServerInstances; i++)
                {
                    var serverTask = Task.Run(() => RunServerInstanceAsync(i, _cancellationTokenSource.Token));
                    _serverTasks.Add(serverTask);
                }

                _logger.LogInformation("Named pipe server started with {InstanceCount} server instances", 
                    _options.NamedPipe.MaxServerInstances);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting named pipe server");
                throw;
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Stopping named pipe server...");

                _cancellationTokenSource.Cancel();

                // Wait for all server tasks to complete
                await Task.WhenAll(_serverTasks);

                _logger.LogInformation("Named pipe server stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping named pipe server");
                throw;
            }
        }

        private async Task RunServerInstanceAsync(int instanceId, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Starting named pipe server instance {InstanceId}", instanceId);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using var pipeServer = new NamedPipeServerStream(
                        _options.NamedPipe.PipeName,
                        PipeDirection.InOut,
                        _options.NamedPipe.MaxServerInstances,
                        PipeTransmissionMode.Message,
                        PipeOptions.Asynchronous,
                        _options.NamedPipe.BufferSize,
                        _options.NamedPipe.BufferSize);

                    _logger.LogDebug("Instance {InstanceId}: Waiting for client connection...", instanceId);

                    // Wait for client connection
                    await pipeServer.WaitForConnectionAsync(cancellationToken);

                    _logger.LogDebug("Instance {InstanceId}: Client connected", instanceId);

                    // Handle the client connection
                    await HandleClientConnectionAsync(pipeServer, instanceId, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Expected during shutdown
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in named pipe server instance {InstanceId}", instanceId);
                    
                    // Brief delay before retrying to avoid tight loop on persistent errors
                    await Task.Delay(1000, cancellationToken);
                }
            }

            _logger.LogDebug("Named pipe server instance {InstanceId} stopped", instanceId);
        }

        private async Task HandleClientConnectionAsync(NamedPipeServerStream pipeServer, int instanceId, CancellationToken cancellationToken)
        {
            try
            {
                using var reader = new StreamReader(pipeServer, Encoding.UTF8, false, _options.NamedPipe.BufferSize, true);
                using var writer = new StreamWriter(pipeServer, Encoding.UTF8, _options.NamedPipe.BufferSize, true) { AutoFlush = true };

                while (pipeServer.IsConnected && !cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        // Read request
                        var requestJson = await reader.ReadLineAsync(cancellationToken);
                        if (requestJson == null)
                        {
                            _logger.LogDebug("Instance {InstanceId}: Client disconnected", instanceId);
                            break;
                        }

                        _logger.LogDebug("Instance {InstanceId}: Received request: {Request}", instanceId, requestJson);

                        // Process request
                        var responseJson = await ProcessRequestAsync(requestJson, cancellationToken);

                        // Send response
                        await writer.WriteLineAsync(responseJson);

                        _logger.LogDebug("Instance {InstanceId}: Sent response: {Response}", instanceId, responseJson);
                    }
                    catch (IOException ex)
                    {
                        _logger.LogDebug(ex, "Instance {InstanceId}: Client connection closed", instanceId);
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Instance {InstanceId}: Error processing client request", instanceId);
                        
                        // Send error response to client
                        try
                        {
                            var errorResponse = CreateErrorResponse("internal_error", $"Internal server error: {ex.Message}");
                            await writer.WriteLineAsync(errorResponse);
                        }
                        catch (Exception writeEx)
                        {
                            _logger.LogError(writeEx, "Instance {InstanceId}: Failed to send error response", instanceId);
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Instance {InstanceId}: Error handling client connection", instanceId);
            }
        }

        public async Task<string> ProcessRequestAsync(string requestJson, CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;
            string operation = "unknown";
            bool success = false;

            try
            {
                // Parse JSON-RPC request
                var request = JsonSerializer.Deserialize<JsonRpcRequest>(requestJson);
                if (request == null)
                {
                    return CreateErrorResponse("parse_error", "Invalid JSON-RPC request");
                }

                operation = request.Method;
                _logger.LogDebug("Processing request: {Method} with ID: {Id}", request.Method, request.Id);

                // Route request to appropriate kernel method
                var result = await RouteRequestAsync(request, cancellationToken);
                success = true;

                var response = new JsonRpcResponse
                {
                    Id = request.Id,
                    Result = result
                };

                return JsonSerializer.Serialize(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing request: {RequestJson}", requestJson);
                return CreateErrorResponse("internal_error", $"Internal server error: {ex.Message}");
            }
            finally
            {
                var duration = DateTime.UtcNow - startTime;
                _metricsCollector.RecordTransaction(operation, duration, success);
            }
        }

        private async Task<object?> RouteRequestAsync(JsonRpcRequest request, CancellationToken cancellationToken)
        {
            return request.Method switch
            {
                "create_session" => await HandleCreateSessionAsync(request.Params, cancellationToken),
                "start_transaction" => await HandleStartTransactionAsync(request.Params, cancellationToken),
                "add_line_item" => await HandleAddLineItemAsync(request.Params, cancellationToken),
                "process_payment" => await HandleProcessPaymentAsync(request.Params, cancellationToken),
                "get_transaction" => await HandleGetTransactionAsync(request.Params, cancellationToken),
                "close_session" => await HandleCloseSessionAsync(request.Params, cancellationToken),
                _ => throw new ArgumentException($"Unknown method: {request.Method}")
            };
        }

        private async Task<object?> HandleCreateSessionAsync(JsonElement? parameters, CancellationToken cancellationToken)
        {
            var terminalId = parameters?.GetProperty("terminal_id").GetString() ?? "unknown";
            var operatorId = parameters?.GetProperty("operator_id").GetString() ?? "system";

            var sessionId = await _kernelEngine.CreateSessionAsync(terminalId, operatorId, cancellationToken);
            return new { session_id = sessionId };
        }

        private async Task<object?> HandleStartTransactionAsync(JsonElement? parameters, CancellationToken cancellationToken)
        {
            var sessionId = parameters?.GetProperty("session_id").GetString() ?? throw new ArgumentException("session_id required");
            var currency = parameters?.GetProperty("currency").GetString() ?? "USD";

            var result = await _kernelEngine.StartTransactionAsync(sessionId, currency, cancellationToken);
            return result;
        }

        private async Task<object?> HandleAddLineItemAsync(JsonElement? parameters, CancellationToken cancellationToken)
        {
            var sessionId = parameters?.GetProperty("session_id").GetString() ?? throw new ArgumentException("session_id required");
            var transactionId = parameters?.GetProperty("transaction_id").GetString() ?? throw new ArgumentException("transaction_id required");
            var productId = parameters?.GetProperty("product_id").GetString() ?? throw new ArgumentException("product_id required");
            var quantity = parameters?.GetProperty("quantity").GetInt32() ?? 1;
            var unitPrice = parameters?.GetProperty("unit_price").GetDecimal() ?? throw new ArgumentException("unit_price required");

            var result = await _kernelEngine.AddLineItemAsync(sessionId, transactionId, productId, quantity, unitPrice, cancellationToken);
            return result;
        }

        private async Task<object?> HandleProcessPaymentAsync(JsonElement? parameters, CancellationToken cancellationToken)
        {
            var sessionId = parameters?.GetProperty("session_id").GetString() ?? throw new ArgumentException("session_id required");
            var transactionId = parameters?.GetProperty("transaction_id").GetString() ?? throw new ArgumentException("transaction_id required");
            var amount = parameters?.GetProperty("amount").GetDecimal() ?? throw new ArgumentException("amount required");
            var paymentType = parameters?.GetProperty("payment_type").GetString() ?? "cash";

            var result = await _kernelEngine.ProcessPaymentAsync(sessionId, transactionId, amount, paymentType, cancellationToken);
            return result;
        }

        private async Task<object?> HandleGetTransactionAsync(JsonElement? parameters, CancellationToken cancellationToken)
        {
            var sessionId = parameters?.GetProperty("session_id").GetString() ?? throw new ArgumentException("session_id required");
            var transactionId = parameters?.GetProperty("transaction_id").GetString() ?? throw new ArgumentException("transaction_id required");

            var result = await _kernelEngine.GetTransactionAsync(sessionId, transactionId, cancellationToken);
            return result;
        }

        private async Task<object?> HandleCloseSessionAsync(JsonElement? parameters, CancellationToken cancellationToken)
        {
            var sessionId = parameters?.GetProperty("session_id").GetString() ?? throw new ArgumentException("session_id required");

            await _kernelEngine.CloseSessionAsync(sessionId, cancellationToken);
            return new { success = true };
        }

        private static string CreateErrorResponse(string code, string message, object? id = null)
        {
            var error = new JsonRpcError
            {
                Code = code,
                Message = message
            };

            var response = new JsonRpcResponse
            {
                Id = id,
                Error = error
            };

            return JsonSerializer.Serialize(response);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _disposed = true;
            }
        }
    }

    // JSON-RPC Data Transfer Objects

    public class JsonRpcRequest
    {
        public string JsonRpc { get; set; } = "2.0";
        public string Method { get; set; } = "";
        public JsonElement? Params { get; set; }
        public object? Id { get; set; }
    }

    public class JsonRpcResponse
    {
        public string JsonRpc { get; set; } = "2.0";
        public object? Result { get; set; }
        public JsonRpcError? Error { get; set; }
        public object? Id { get; set; }
    }

    public class JsonRpcError
    {
        public string Code { get; set; } = "";
        public string Message { get; set; } = "";
        public object? Data { get; set; }
    }
}
