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
using PosKernel.Abstractions;
using System.Collections.Concurrent;

namespace PosKernel.Service.Services
{
    /// <summary>
    /// Core POS Kernel engine implementation with session management.
    /// Wraps the existing kernel functionality with service-oriented features.
    /// </summary>
    public class PosKernelEngine : IPosKernelEngine, IDisposable
    {
        private readonly ILogger<PosKernelEngine> _logger;
        private readonly ISessionManager _sessionManager;
        private readonly ConcurrentDictionary<string, Transaction> _transactions;
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="PosKernelEngine"/> class.
        /// </summary>
        /// <param name="logger">Logger for diagnostics and debugging.</param>
        /// <param name="sessionManager">The session manager for handling terminal sessions.</param>
        public PosKernelEngine(ILogger<PosKernelEngine> logger, ISessionManager sessionManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _transactions = new ConcurrentDictionary<string, Transaction>();
        }

        /// <summary>
        /// Creates a new session for a terminal and operator.
        /// </summary>
        /// <param name="terminalId">The terminal identifier.</param>
        /// <param name="operatorId">The operator identifier.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The session identifier.</returns>
        public async Task<string> CreateSessionAsync(string terminalId, string operatorId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Creating session for terminal {TerminalId}, operator {OperatorId}", terminalId, operatorId);

            var sessionInfo = await _sessionManager.CreateSessionAsync(terminalId, operatorId, cancellationToken);

            _logger.LogInformation("Session created: {SessionId}", sessionInfo.SessionId);
            return sessionInfo.SessionId;
        }

        /// <summary>
        /// Starts a new transaction for the specified session.
        /// </summary>
        /// <param name="sessionId">The session identifier.</param>
        /// <param name="currency">The transaction currency (must be explicitly provided - no cultural defaults).</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The transaction result.</returns>
        public async Task<TransactionResult> StartTransactionAsync(string sessionId, string currency, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Starting transaction for session {SessionId} with currency {Currency}", sessionId, currency);

                // Validate session
                var session = await _sessionManager.GetSessionAsync(sessionId, cancellationToken);
                if (session == null || !session.IsActive)
                {
                    return new TransactionResult
                    {
                        Success = false,
                        Error = $"Invalid or inactive session: {sessionId}"
                    };
                }

                // Create new transaction using existing kernel contracts
                var transaction = new Transaction
                {
                    Id = TransactionId.New(),
                    State = TransactionState.StartTransaction  // AI-first: fresh transaction ready for customer interaction
                };

                // Store transaction
                var transactionId = transaction.Id.ToString();
                _transactions.TryAdd(transactionId, transaction);

                // Update session activity
                await _sessionManager.UpdateSessionActivityAsync(sessionId, cancellationToken);

                _logger.LogInformation("Transaction {TransactionId} started for session {SessionId}", transactionId, sessionId);

                return new TransactionResult
                {
                    Success = true,
                    TransactionId = transactionId,
                    SessionId = sessionId,
                    Total = transaction.Total.ToDecimal(),
                    State = transaction.State.ToString()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting transaction for session {SessionId}", sessionId);
                return new TransactionResult
                {
                    Success = false,
                    Error = $"Internal error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Adds a line item to an existing transaction.
        /// </summary>
        /// <param name="sessionId">The session identifier.</param>
        /// <param name="transactionId">The transaction identifier.</param>
        /// <param name="productId">The product identifier.</param>
        /// <param name="quantity">The quantity of the product.</param>
        /// <param name="unitPrice">The unit price of the product.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The updated transaction result.</returns>
        public async Task<TransactionResult> AddLineItemAsync(string sessionId, string transactionId, string productId, int quantity, decimal unitPrice, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Adding line item to transaction {TransactionId}: {ProductId} x {Quantity} @ {UnitPrice}",
                    transactionId, productId, quantity, unitPrice);

                // Validate session
                var session = await _sessionManager.GetSessionAsync(sessionId, cancellationToken);
                if (session == null || !session.IsActive)
                {
                    return new TransactionResult
                    {
                        Success = false,
                        Error = $"Invalid or inactive session: {sessionId}"
                    };
                }

                // Get transaction
                if (!_transactions.TryGetValue(transactionId, out var transaction))
                {
                    return new TransactionResult
                    {
                        Success = false,
                        Error = $"Transaction not found: {transactionId}"
                    };
                }

                // Add line item using kernel contracts
                var productIdObj = new ProductId(productId);
                var unitPriceMoney = new Money((long)(unitPrice * 100), transaction.Currency);

                transaction.AddLine(productIdObj, quantity, unitPriceMoney);

                // Update session activity
                await _sessionManager.UpdateSessionActivityAsync(sessionId, cancellationToken);

                _logger.LogInformation("Line item added to transaction {TransactionId}: {ProductId} x {Quantity}",
                    transactionId, productId, quantity);

                return new TransactionResult
                {
                    Success = true,
                    TransactionId = transactionId,
                    SessionId = sessionId,
                    Total = transaction.Total.ToDecimal(),
                    State = transaction.State.ToString(),
                    Data = new
                    {
                        LineCount = transaction.Lines.Count,
                        LastProduct = productId,
                        LastQuantity = quantity,
                        LastUnitPrice = unitPrice
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding line item to transaction {TransactionId}", transactionId);
                return new TransactionResult
                {
                    Success = false,
                    Error = $"Internal error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Processes a payment for the specified transaction.
        /// </summary>
        /// <param name="sessionId">The session identifier.</param>
        /// <param name="transactionId">The transaction identifier.</param>
        /// <param name="amount">The payment amount.</param>
        /// <param name="paymentType">The payment type (default is cash).</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The updated transaction result.</returns>
        public async Task<TransactionResult> ProcessPaymentAsync(string sessionId, string transactionId, decimal amount, string paymentType = "cash", CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Processing payment for transaction {TransactionId}: {Amount} via {PaymentType}",
                    transactionId, amount, paymentType);

                // Validate session
                var session = await _sessionManager.GetSessionAsync(sessionId, cancellationToken);
                if (session == null || !session.IsActive)
                {
                    return new TransactionResult
                    {
                        Success = false,
                        Error = $"Invalid or inactive session: {sessionId}"
                    };
                }

                // Get transaction
                if (!_transactions.TryGetValue(transactionId, out var transaction))
                {
                    return new TransactionResult
                    {
                        Success = false,
                        Error = $"Transaction not found: {transactionId}"
                    };
                }

                // Process payment using kernel contracts
                var paymentAmount = new Money((long)(amount * 100), transaction.Currency);

                // For now, we'll use cash tender - in a full implementation,
                // we'd have different tender types
                transaction.AddCashTender(paymentAmount);

                // Update session activity
                await _sessionManager.UpdateSessionActivityAsync(sessionId, cancellationToken);

                _logger.LogInformation("Payment processed for transaction {TransactionId}: {Amount} via {PaymentType}, Final state: {State}",
                    transactionId, amount, paymentType, transaction.State);

                return new TransactionResult
                {
                    Success = true,
                    TransactionId = transactionId,
                    SessionId = sessionId,
                    Total = transaction.Total.ToDecimal(),
                    State = transaction.State.ToString(),
                    Data = new
                    {
                        PaymentAmount = amount,
                        PaymentType = paymentType,
                        TenderedAmount = transaction.Tendered.ToDecimal(),
                        ChangeAmount = Math.Max(0, transaction.Tendered.ToDecimal() - transaction.Total.ToDecimal())
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing payment for transaction {TransactionId}", transactionId);
                return new TransactionResult
                {
                    Success = false,
                    Error = $"Internal error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Gets the details of a specific transaction.
        /// </summary>
        /// <param name="sessionId">The session identifier.</param>
        /// <param name="transactionId">The transaction identifier.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The transaction result with details.</returns>
        public async Task<TransactionResult> GetTransactionAsync(string sessionId, string transactionId, CancellationToken cancellationToken = default)
        {
            try
            {
                // Validate session
                var session = await _sessionManager.GetSessionAsync(sessionId, cancellationToken);
                if (session == null || !session.IsActive)
                {
                    return new TransactionResult
                    {
                        Success = false,
                        Error = $"Invalid or inactive session: {sessionId}"
                    };
                }

                // Get transaction
                if (!_transactions.TryGetValue(transactionId, out var transaction))
                {
                    return new TransactionResult
                    {
                        Success = false,
                        Error = $"Transaction not found: {transactionId}"
                    };
                }

                return new TransactionResult
                {
                    Success = true,
                    TransactionId = transactionId,
                    SessionId = sessionId,
                    Total = transaction.Total.ToDecimal(),
                    State = transaction.State.ToString(),
                    Data = new
                    {
                        LineItems = transaction.Lines.Select(l => new
                        {
                            ProductId = l.ProductId.ToString(),
                            Quantity = l.Quantity,
                            UnitPrice = l.UnitPrice.ToDecimal(),
                            Extended = l.Extended.ToDecimal()
                        }).ToArray(),
                        TenderedAmount = transaction.Tendered.ToDecimal(),
                        Currency = transaction.Currency
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting transaction {TransactionId}", transactionId);
                return new TransactionResult
                {
                    Success = false,
                    Error = $"Internal error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Closes a session and cleans up associated resources.
        /// </summary>
        /// <param name="sessionId">The session identifier to close.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task CloseSessionAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Closing session {SessionId}", sessionId);

                // Clean up any transactions for this session
                var transactionsToRemove = _transactions
                    .Where(kvp => kvp.Value.Id.ToString().StartsWith(sessionId))
                    .Select(kvp => kvp.Key)
                    .ToArray();

                foreach (var transactionId in transactionsToRemove)
                {
                    _transactions.TryRemove(transactionId, out _);
                }

                // Close session
                await _sessionManager.CloseSessionAsync(sessionId, cancellationToken);

                _logger.LogInformation("Session {SessionId} closed successfully", sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing session {SessionId}", sessionId);
                throw;
            }
        }

        /// <summary>
        /// Disposes the POS kernel engine and releases all resources.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _transactions.Clear();
                _disposed = true;
            }
        }
    }
}
