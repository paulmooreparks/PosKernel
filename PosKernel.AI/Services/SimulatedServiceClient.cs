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
using System.Text;
using System.Text.Json;

namespace PosKernel.AI.Services
{
    /// <summary>
    /// Simple service client interface for the AI demo.
    /// Simulates communication with PosKernel.Service.
    /// </summary>
    public interface ISimpleServiceClient : IDisposable
    {
        Task<bool> ConnectAsync(CancellationToken cancellationToken = default);
        Task DisconnectAsync(CancellationToken cancellationToken = default);
        bool IsConnected { get; }

        Task<string> CreateSessionAsync(string terminalId, string operatorId, CancellationToken cancellationToken = default);
        Task<ServiceTransactionResult> StartTransactionAsync(string sessionId, string currency = "USD", CancellationToken cancellationToken = default);
        Task<ServiceTransactionResult> AddLineItemAsync(string sessionId, string transactionId, string productId, int quantity, decimal unitPrice, CancellationToken cancellationToken = default);
        Task<ServiceTransactionResult> ProcessPaymentAsync(string sessionId, string transactionId, decimal amount, string paymentType = "cash", CancellationToken cancellationToken = default);
        Task<ServiceTransactionResult> GetTransactionAsync(string sessionId, string transactionId, CancellationToken cancellationToken = default);
        Task<ReceiptResult> PrintReceiptAsync(string sessionId, string transactionId, CancellationToken cancellationToken = default);
        Task CloseSessionAsync(string sessionId, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Result of a service transaction operation.
    /// </summary>
    public class ServiceTransactionResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public string? TransactionId { get; set; }
        public string? SessionId { get; set; }
        public decimal Total { get; set; }
        public string State { get; set; } = "";
        public object? Data { get; set; }
    }

    /// <summary>
    /// Result of a receipt printing operation.
    /// </summary>
    public class ReceiptResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public string? ReceiptContent { get; set; }
        public string? PrinterStatus { get; set; }
        public DateTime PrintedAt { get; set; }
    }

    /// <summary>
    /// Simple service client that simulates communication with PosKernel.Service.
    /// In a real implementation, this would use Named Pipes or HTTP.
    /// For demo purposes, this simulates the service architecture behavior.
    /// </summary>
    public class SimulatedServiceClient : ISimpleServiceClient
    {
        private readonly ILogger<SimulatedServiceClient> _logger;
        private bool _isConnected = false;
        private bool _disposed = false;

        // Simulate service state (in real implementation, this would be in the service)
        private readonly Dictionary<string, string> _sessions = new();
        private readonly Dictionary<string, ServiceTransactionState> _transactions = new();

        public SimulatedServiceClient(ILogger<SimulatedServiceClient> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool IsConnected => _isConnected;

        public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("🔗 Simulating connection to POS Kernel Service...");
                
                // Simulate connection delay
                await Task.Delay(100, cancellationToken);
                
                _isConnected = true;
                _logger.LogInformation("✅ Connected to POS Kernel Service (Simulated)");
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to service");
                return false;
            }
        }

        public async Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("🔌 Disconnecting from POS Kernel Service...");
            _isConnected = false;
            _sessions.Clear();
            _transactions.Clear();
            _logger.LogInformation("✅ Disconnected successfully");
        }

        public async Task<string> CreateSessionAsync(string terminalId, string operatorId, CancellationToken cancellationToken = default)
        {
            ThrowIfNotConnected();
            
            var sessionId = $"session_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}_{Guid.NewGuid().ToString("N")[..8]}";
            _sessions[sessionId] = $"{terminalId}:{operatorId}";
            
            _logger.LogInformation("✅ Session created: {SessionId} for terminal {TerminalId}", sessionId, terminalId);
            
            await Task.Delay(10, cancellationToken); // Simulate service delay
            return sessionId;
        }

        public async Task<ServiceTransactionResult> StartTransactionAsync(string sessionId, string currency = "USD", CancellationToken cancellationToken = default)
        {
            ThrowIfNotConnected();
            
            if (!_sessions.ContainsKey(sessionId))
            {
                return new ServiceTransactionResult 
                { 
                    Success = false, 
                    Error = $"Invalid session: {sessionId}" 
                };
            }

            var transactionId = $"txn_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}_{Guid.NewGuid().ToString("N")[..8]}";
            _transactions[transactionId] = new ServiceTransactionState
            {
                SessionId = sessionId,
                Currency = currency,
                Items = new List<ServiceLineItem>(),
                State = "Building"
            };

            _logger.LogInformation("✅ Transaction started: {TransactionId} in session {SessionId}", transactionId, sessionId);
            
            await Task.Delay(10, cancellationToken); // Simulate service delay
            
            return new ServiceTransactionResult
            {
                Success = true,
                TransactionId = transactionId,
                SessionId = sessionId,
                Total = 0m,
                State = "Building"
            };
        }

        public async Task<ServiceTransactionResult> AddLineItemAsync(string sessionId, string transactionId, string productId, int quantity, decimal unitPrice, CancellationToken cancellationToken = default)
        {
            ThrowIfNotConnected();
            
            if (!_sessions.ContainsKey(sessionId))
            {
                return new ServiceTransactionResult 
                { 
                    Success = false, 
                    Error = $"Invalid session: {sessionId}" 
                };
            }

            if (!_transactions.TryGetValue(transactionId, out var transaction))
            {
                return new ServiceTransactionResult 
                { 
                    Success = false, 
                    Error = $"Transaction not found: {transactionId}" 
                };
            }

            // Add line item
            transaction.Items.Add(new ServiceLineItem
            {
                ProductId = productId,
                Quantity = quantity,
                UnitPrice = unitPrice,
                Extended = quantity * unitPrice
            });

            var total = transaction.Items.Sum(i => i.Extended);

            _logger.LogInformation("✅ Line item added: {ProductId} x{Quantity} @ ${UnitPrice} to transaction {TransactionId}", 
                productId, quantity, unitPrice, transactionId);
            
            await Task.Delay(10, cancellationToken); // Simulate service delay

            return new ServiceTransactionResult
            {
                Success = true,
                TransactionId = transactionId,
                SessionId = sessionId,
                Total = total,
                State = transaction.State,
                Data = new
                {
                    LineCount = transaction.Items.Count,
                    LastProduct = productId,
                    LastQuantity = quantity,
                    LastUnitPrice = unitPrice
                }
            };
        }

        public async Task<ServiceTransactionResult> ProcessPaymentAsync(string sessionId, string transactionId, decimal amount, string paymentType = "cash", CancellationToken cancellationToken = default)
        {
            ThrowIfNotConnected();
            
            if (!_sessions.ContainsKey(sessionId))
            {
                return new ServiceTransactionResult 
                { 
                    Success = false, 
                    Error = $"Invalid session: {sessionId}" 
                };
            }

            if (!_transactions.TryGetValue(transactionId, out var transaction))
            {
                return new ServiceTransactionResult 
                { 
                    Success = false, 
                    Error = $"Transaction not found: {transactionId}" 
                };
            }

            var total = transaction.Items.Sum(i => i.Extended);
            transaction.State = "Completed";
            transaction.PaymentAmount = amount;
            transaction.PaymentType = paymentType;

            _logger.LogInformation("✅ Payment processed: ${Amount} via {PaymentType} for transaction {TransactionId}", 
                amount, paymentType, transactionId);
            
            await Task.Delay(50, cancellationToken); // Simulate payment processing delay

            return new ServiceTransactionResult
            {
                Success = true,
                TransactionId = transactionId,
                SessionId = sessionId,
                Total = total,
                State = "Completed",
                Data = new
                {
                    PaymentAmount = amount,
                    PaymentType = paymentType,
                    TenderedAmount = amount,
                    ChangeAmount = Math.Max(0, amount - total)
                }
            };
        }

        public async Task<ServiceTransactionResult> GetTransactionAsync(string sessionId, string transactionId, CancellationToken cancellationToken = default)
        {
            ThrowIfNotConnected();
            
            if (!_sessions.ContainsKey(sessionId))
            {
                return new ServiceTransactionResult 
                { 
                    Success = false, 
                    Error = $"Invalid session: {sessionId}" 
                };
            }

            if (!_transactions.TryGetValue(transactionId, out var transaction))
            {
                return new ServiceTransactionResult 
                { 
                    Success = false, 
                    Error = $"Transaction not found: {transactionId}" 
                };
            }

            var total = transaction.Items.Sum(i => i.Extended);
            
            await Task.Delay(5, cancellationToken); // Simulate service delay

            return new ServiceTransactionResult
            {
                Success = true,
                TransactionId = transactionId,
                SessionId = sessionId,
                Total = total,
                State = transaction.State,
                Data = new
                {
                    LineItems = transaction.Items.Select(i => new
                    {
                        ProductId = i.ProductId,
                        Quantity = i.Quantity,
                        UnitPrice = i.UnitPrice,
                        Extended = i.Extended
                    }).ToArray(),
                    Currency = transaction.Currency,
                    PaymentAmount = transaction.PaymentAmount,
                    PaymentType = transaction.PaymentType
                }
            };
        }

        public async Task<ReceiptResult> PrintReceiptAsync(string sessionId, string transactionId, CancellationToken cancellationToken = default)
        {
            ThrowIfNotConnected();
            
            if (!_sessions.ContainsKey(sessionId))
            {
                return new ReceiptResult 
                { 
                    Success = false, 
                    Error = $"Invalid session: {sessionId}" 
                };
            }

            if (!_transactions.TryGetValue(transactionId, out var transaction))
            {
                return new ReceiptResult 
                { 
                    Success = false, 
                    Error = $"Transaction not found: {transactionId}" 
                };
            }

            // Generate receipt content
            var receiptContent = GenerateReceipt(transaction, sessionId, transactionId);
            
            _logger.LogInformation("🖨️ Receipt printed for transaction {TransactionId} in session {SessionId}", 
                transactionId, sessionId);
            
            await Task.Delay(100, cancellationToken); // Simulate printing delay

            return new ReceiptResult
            {
                Success = true,
                ReceiptContent = receiptContent,
                PrinterStatus = "Ready",
                PrintedAt = DateTime.Now
            };
        }

        public async Task CloseSessionAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            ThrowIfNotConnected();
            
            _sessions.Remove(sessionId);
            
            // Remove transactions for this session
            var transactionsToRemove = _transactions
                .Where(kvp => kvp.Value.SessionId == sessionId)
                .Select(kvp => kvp.Key)
                .ToArray();

            foreach (var transactionId in transactionsToRemove)
            {
                _transactions.Remove(transactionId);
            }

            _logger.LogInformation("✅ Session closed: {SessionId}", sessionId);
            
            await Task.Delay(10, cancellationToken); // Simulate service delay
        }

        private string GenerateReceipt(ServiceTransactionState transaction, string sessionId, string transactionId)
        {
            var receipt = new StringBuilder();
            var now = DateTime.Now;
            
            // Header
            receipt.AppendLine("════════════════════════════════");
            receipt.AppendLine("       SERVICE-BASED COFFEE");
            receipt.AppendLine("    Powered by POS Kernel Service");
            receipt.AppendLine("════════════════════════════════");
            receipt.AppendLine();
            receipt.AppendLine($"Date: {now:yyyy-MM-dd}");
            receipt.AppendLine($"Time: {now:HH:mm:ss}");
            receipt.AppendLine($"Transaction: {transactionId}");
            receipt.AppendLine($"Session: {sessionId}");
            receipt.AppendLine();
            receipt.AppendLine("────────────────────────────────");
            
            // Line items
            var total = 0m;
            foreach (var item in transaction.Items)
            {
                var itemName = GetProductDisplayName(item.ProductId);
                receipt.AppendLine($"{itemName}");
                receipt.AppendLine($"  {item.Quantity} x ${item.UnitPrice:F2} = ${item.Extended:F2}");
                total += item.Extended;
            }
            
            receipt.AppendLine("────────────────────────────────");
            receipt.AppendLine($"SUBTOTAL:        ${total:F2}");
            receipt.AppendLine($"TAX:             ${0:F2}");
            receipt.AppendLine($"TOTAL:           ${total:F2}");
            receipt.AppendLine();
            receipt.AppendLine($"PAYMENT ({transaction.PaymentType.ToUpper()}): ${transaction.PaymentAmount:F2}");
            
            var change = transaction.PaymentAmount - total;
            if (change > 0)
            {
                receipt.AppendLine($"CHANGE:          ${change:F2}");
            }
            
            receipt.AppendLine();
            receipt.AppendLine("════════════════════════════════");
            receipt.AppendLine("       THANK YOU!");
            receipt.AppendLine("   🏗️ Enterprise Architecture");
            receipt.AppendLine("   🔗 Session-Based Management");
            receipt.AppendLine("   ☕ AI-Powered Service");
            receipt.AppendLine("════════════════════════════════");
            receipt.AppendLine($"Printed: {now:yyyy-MM-dd HH:mm:ss}");
            
            return receipt.ToString();
        }

        private string GetProductDisplayName(string productId)
        {
            return productId switch
            {
                "COFFEE_SM" => "Small Coffee",
                "COFFEE_MD" => "Medium Coffee", 
                "COFFEE_LG" => "Large Coffee",
                "LATTE" => "Caffe Latte",
                "CAPPUCCINO" => "Cappuccino",
                "MUFFIN_BLUEBERRY" => "Blueberry Muffin",
                "BREAKFAST_SANDWICH" => "Breakfast Sandwich",
                _ => productId
            };
        }

        private void ThrowIfNotConnected()
        {
            if (!_isConnected)
            {
                throw new InvalidOperationException("Not connected to service. Call ConnectAsync() first.");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                DisconnectAsync(CancellationToken.None).Wait(1000);
                _disposed = true;
            }
        }
    }

    // Supporting classes for the simulated service client

    internal class ServiceTransactionState
    {
        public string SessionId { get; set; } = "";
        public string Currency { get; set; } = "USD";
        public string State { get; set; } = "Building";
        public List<ServiceLineItem> Items { get; set; } = new();
        public decimal PaymentAmount { get; set; }
        public string PaymentType { get; set; } = "";
    }

    internal class ServiceLineItem
    {
        public string ProductId { get; set; } = "";
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Extended { get; set; }
    }
}
