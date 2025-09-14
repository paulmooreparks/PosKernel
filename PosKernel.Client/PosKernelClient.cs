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

        public PosKernelClient(ILogger<PosKernelClient> logger, PosKernelClientOptions? options = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? new PosKernelClientOptions();
        }

        public bool IsConnected => _pipeClient?.IsConnected == true;

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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during disconnect");
            }
        }

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
            if (typeof(T) == typeof(object) || typeof(T) == typeof(dynamic))
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

    public class JsonRpcClientResponse
    {
        public string JsonRpc { get; set; } = "";
        public object? Result { get; set; }
        public JsonRpcClientError? Error { get; set; }
        public object? Id { get; set; }
    }

    public class JsonRpcClientError
    {
        public string Code { get; set; } = "";
        public string Message { get; set; } = "";
        public object? Data { get; set; }
    }
}
