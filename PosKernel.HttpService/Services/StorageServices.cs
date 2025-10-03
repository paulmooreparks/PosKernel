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

using PosKernel.HttpService.Models;
using System.Collections.Concurrent;

namespace PosKernel.HttpService.Services;

// ===== STORAGE INTERFACES =====

public interface ISessionStorage
{
    Task<SessionResponse> CreateSessionAsync(SessionRequest request);
    Task<SessionResponse?> GetSessionAsync(string sessionId);
    Task<bool> DeleteSessionAsync(string sessionId);
}

public interface ITransactionStorage
{
    Task<TransactionResponse> CreateTransactionAsync(TransactionRequest request);
    Task<TransactionResponse?> GetTransactionAsync(string transactionId);
    Task<bool> DeleteTransactionAsync(string transactionId);
    Task<TransactionResponse?> FinalizeTransactionAsync(string transactionId);
    Task<TransactionResponse?> VoidTransactionAsync(string transactionId);
    Task UpdateTransactionTotalAsync(string transactionId, decimal total);
}

public interface ILineItemStorage
{
    Task<LineItemResponse> AddLineItemAsync(string transactionId, LineItemRequest request, string currency);
    Task<List<LineItemResponse>> GetLineItemsAsync(string transactionId);
    Task<bool> RemoveLineItemAsync(string transactionId, string itemId);
    Task<decimal> GetTransactionTotalAsync(string transactionId, string currency);
}

// ===== IN-MEMORY IMPLEMENTATIONS =====

public class InMemorySessionStorage : ISessionStorage
{
    private readonly ConcurrentDictionary<string, SessionResponse> _sessions = new();
    private readonly ILogger<InMemorySessionStorage> _logger;

    public InMemorySessionStorage(ILogger<InMemorySessionStorage> logger)
    {
        _logger = logger;
    }

    public Task<SessionResponse> CreateSessionAsync(SessionRequest request)
    {
        var sessionId = Guid.NewGuid().ToString();
        var session = new SessionResponse
        {
            SessionId = sessionId,
            TerminalId = request.TerminalId,
            OperatorId = request.OperatorId,
            Status = "active"
        };

        _sessions[sessionId] = session;
        _logger.LogInformation("HTTP POST /api/sessions - Created session {SessionId} for terminal {TerminalId}, operator {OperatorId}",
            sessionId, request.TerminalId, request.OperatorId);

        return Task.FromResult(session);
    }

    public Task<SessionResponse?> GetSessionAsync(string sessionId)
    {
        _logger.LogInformation("HTTP GET /api/sessions/{SessionId}", sessionId);
        _sessions.TryGetValue(sessionId, out var session);
        return Task.FromResult(session);
    }

    public Task<bool> DeleteSessionAsync(string sessionId)
    {
        _logger.LogInformation("HTTP DELETE /api/sessions/{SessionId}", sessionId);
        return Task.FromResult(_sessions.TryRemove(sessionId, out _));
    }
}

public class InMemoryTransactionStorage : ITransactionStorage
{
    private readonly ConcurrentDictionary<string, TransactionResponse> _transactions = new();
    private readonly ISessionStorage _sessionStorage;
    private readonly ILogger<InMemoryTransactionStorage> _logger;

    public InMemoryTransactionStorage(ISessionStorage sessionStorage, ILogger<InMemoryTransactionStorage> logger)
    {
        _sessionStorage = sessionStorage;
        _logger = logger;
    }

    public async Task<TransactionResponse> CreateTransactionAsync(TransactionRequest request)
    {
        // Verify session exists (matching Rust behavior)
        var session = await _sessionStorage.GetSessionAsync(request.SessionId);
        if (session == null)
        {
            throw new InvalidOperationException($"Session {request.SessionId} not found");
        }

        var transactionId = Guid.NewGuid().ToString();
        var transaction = new TransactionResponse
        {
            TransactionId = transactionId,
            SessionId = request.SessionId,
            Store = request.Store,
            Currency = request.Currency,
            Language = request.Language,
            Status = "open", // Match Rust service behavior
            Total = 0m // Culture-neutral: no assumptions about decimal places
        };

        _transactions[transactionId] = transaction;
        _logger.LogInformation("HTTP POST /api/transactions - Created transaction {TransactionId} for session {SessionId}",
            transactionId, request.SessionId);

        return transaction;
    }

    public Task<TransactionResponse?> GetTransactionAsync(string transactionId)
    {
        _logger.LogInformation("HTTP GET /api/transactions/{TransactionId}", transactionId);
        _transactions.TryGetValue(transactionId, out var transaction);
        return Task.FromResult(transaction);
    }

    public Task<bool> DeleteTransactionAsync(string transactionId)
    {
        _logger.LogInformation("HTTP DELETE /api/transactions/{TransactionId}", transactionId);
        return Task.FromResult(_transactions.TryRemove(transactionId, out _));
    }

    public Task<TransactionResponse?> FinalizeTransactionAsync(string transactionId)
    {
        _logger.LogInformation("HTTP POST /api/transactions/{TransactionId}/finalize", transactionId);
        if (_transactions.TryGetValue(transactionId, out var transaction))
        {
            transaction.Status = "finalized";
            return Task.FromResult<TransactionResponse?>(transaction);
        }
        return Task.FromResult<TransactionResponse?>(null);
    }

    public Task<TransactionResponse?> VoidTransactionAsync(string transactionId)
    {
        _logger.LogInformation("HTTP POST /api/transactions/{TransactionId}/void", transactionId);
        if (_transactions.TryGetValue(transactionId, out var transaction))
        {
            transaction.Status = "voided";
            return Task.FromResult<TransactionResponse?>(transaction);
        }
        return Task.FromResult<TransactionResponse?>(null);
    }

    public Task UpdateTransactionTotalAsync(string transactionId, decimal total)
    {
        if (_transactions.TryGetValue(transactionId, out var transaction))
        {
            // ARCHITECTURAL PRINCIPLE: Round total according to transaction currency
            transaction.Total = CurrencyHelper.RoundToCurrency(total, transaction.Currency);
        }
        return Task.CompletedTask;
    }
}

public class InMemoryLineItemStorage : ILineItemStorage
{
    private readonly ConcurrentDictionary<string, List<LineItemResponse>> _lineItems = new();
    private readonly ILogger<InMemoryLineItemStorage> _logger;

    public InMemoryLineItemStorage(ILogger<InMemoryLineItemStorage> logger)
    {
        _logger = logger;
    }

    public Task<LineItemResponse> AddLineItemAsync(string transactionId, LineItemRequest request, string currency)
    {
        var lineItemId = Guid.NewGuid().ToString();

        // ARCHITECTURAL PRINCIPLE: Support line-item hierarchy for NRF compliance
        // Validate parent reference if provided
        if (!string.IsNullOrEmpty(request.ParentLineItemId))
        {
            if (!_lineItems.TryGetValue(transactionId, out var existingItems) ||
                !existingItems.Any(item => item.LineItemId == request.ParentLineItemId))
            {
                throw new InvalidOperationException($"Parent line item {request.ParentLineItemId} not found in transaction {transactionId}");
            }
        }

        // ARCHITECTURAL PRINCIPLE: Use transaction currency for proper decimal precision
        var unitPrice = CurrencyHelper.RoundToCurrency(request.UnitPrice, currency);
        var lineTotal = CurrencyHelper.RoundToCurrency(request.Quantity * unitPrice, currency);

        var lineItem = new LineItemResponse
        {
            LineItemId = lineItemId,
            ProductId = request.ProductId,
            Quantity = request.Quantity,
            UnitPrice = unitPrice,
            LineTotal = lineTotal,
            ParentLineItemId = request.ParentLineItemId
        };

        _lineItems.AddOrUpdate(transactionId,
            new List<LineItemResponse> { lineItem },
            (key, existing) => { existing.Add(lineItem); return existing; });

        _logger.LogInformation("HTTP POST /api/transactions/{TransactionId}/items - Added line item {LineItemId} for product {ProductId} with parent {ParentLineItemId}",
            transactionId, lineItemId, request.ProductId, request.ParentLineItemId ?? "none");

        return Task.FromResult(lineItem);
    }

    public Task<List<LineItemResponse>> GetLineItemsAsync(string transactionId)
    {
        _logger.LogInformation("HTTP GET /api/transactions/{TransactionId}/items", transactionId);
        _lineItems.TryGetValue(transactionId, out var items);
        return Task.FromResult(items ?? new List<LineItemResponse>());
    }

    public Task<bool> RemoveLineItemAsync(string transactionId, string itemId)
    {
        _logger.LogInformation("HTTP DELETE /api/transactions/{TransactionId}/items/{ItemId}", transactionId, itemId);
        if (_lineItems.TryGetValue(transactionId, out var items))
        {
            var itemToRemove = items.FirstOrDefault(i => i.LineItemId == itemId);
            if (itemToRemove != null)
            {
                items.Remove(itemToRemove);
                return Task.FromResult(true);
            }
        }
        return Task.FromResult(false);
    }

    public Task<decimal> GetTransactionTotalAsync(string transactionId, string currency)
    {
        if (_lineItems.TryGetValue(transactionId, out var items))
        {
            var total = items.Sum(item => item.LineTotal);
            // ARCHITECTURAL PRINCIPLE: Round total to currency precision
            var roundedTotal = CurrencyHelper.RoundToCurrency(total, currency);
            return Task.FromResult(roundedTotal);
        }
        return Task.FromResult(0m);
    }
}
