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

using Microsoft.AspNetCore.Mvc;
using PosKernel.HttpService.Models;
using PosKernel.HttpService.Services;

namespace PosKernel.HttpService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TransactionsController : ControllerBase
{
    private readonly ITransactionStorage _transactionStorage;
    private readonly ILineItemStorage _lineItemStorage;
    private readonly ILogger<TransactionsController> _logger;

    public TransactionsController(
        ITransactionStorage transactionStorage,
        ILineItemStorage lineItemStorage,
        ILogger<TransactionsController> logger)
    {
        _transactionStorage = transactionStorage;
        _lineItemStorage = lineItemStorage;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<TransactionResponse>> CreateTransaction([FromBody] TransactionRequest request)
    {
        _logger.LogInformation("Creating new transaction for session {SessionId} with currency {Currency} and language {Language}",
            request.SessionId, request.Currency, request.Language);

        var transaction = await _transactionStorage.CreateTransactionAsync(request);

        _logger.LogInformation("Transaction {TransactionId} created successfully with initial total {Total}",
            transaction.TransactionId, transaction.Total);

        return CreatedAtAction(nameof(GetTransaction), new { transactionId = transaction.TransactionId }, transaction);
    }

    [HttpGet("{transactionId}")]
    public async Task<ActionResult<TransactionResponse>> GetTransaction(string transactionId)
    {
        _logger.LogInformation("HTTP GET /api/transactions/{TransactionId}", transactionId);
        var transaction = await _transactionStorage.GetTransactionAsync(transactionId);
        if (transaction == null)
        {
            return NotFound();
        }
        return Ok(transaction);
    }

    [HttpDelete("{transactionId}")]
    public async Task<IActionResult> DeleteTransaction(string transactionId)
    {
        _logger.LogInformation("HTTP DELETE /api/transactions/{TransactionId}", transactionId);
        var deleted = await _transactionStorage.DeleteTransactionAsync(transactionId);
        if (!deleted)
        {
            return NotFound();
        }
        return NoContent();
    }

    [HttpPost("{transactionId}/finalize")]
    public async Task<ActionResult<TransactionResponse>> FinalizeTransaction(string transactionId)
    {
        _logger.LogInformation("HTTP POST /api/transactions/{TransactionId}/finalize", transactionId);
        var transaction = await _transactionStorage.FinalizeTransactionAsync(transactionId);
        if (transaction == null)
        {
            return NotFound();
        }
        return Ok(transaction);
    }

    [HttpPost("{transactionId}/void")]
    public async Task<ActionResult<TransactionResponse>> VoidTransaction(string transactionId)
    {
        _logger.LogInformation("HTTP POST /api/transactions/{TransactionId}/void", transactionId);
        var transaction = await _transactionStorage.VoidTransactionAsync(transactionId);
        if (transaction == null)
        {
            return NotFound();
        }
        return Ok(transaction);
    }

    // Line item endpoints
    [HttpPost("{transactionId}/items")]
    public async Task<ActionResult<LineItemResponse>> AddLineItem(string transactionId, [FromBody] LineItemRequest request)
    {
        _logger.LogInformation("Adding line item to transaction {TransactionId}: {ProductId} x{Quantity} at {UnitPrice}",
            transactionId, request.ProductId, request.Quantity, request.UnitPrice);

        // Verify transaction exists (matching Rust behavior)
        var transaction = await _transactionStorage.GetTransactionAsync(transactionId);
        if (transaction == null)
        {
            return NotFound();
        }

        // ARCHITECTURAL PRINCIPLE: Use transaction currency for proper decimal precision
        var lineItem = await _lineItemStorage.AddLineItemAsync(transactionId, request, transaction.Currency);

        // Update transaction total with currency-aware calculation
        var newTotal = await _lineItemStorage.GetTransactionTotalAsync(transactionId, transaction.Currency);

        _logger.LogInformation("Line item {LineItemId} added successfully. Transaction total updated from {OldTotal} to {NewTotal}",
            lineItem.LineItemId, transaction.Total, newTotal);
        await _transactionStorage.UpdateTransactionTotalAsync(transactionId, newTotal);

        return CreatedAtAction(nameof(GetLineItems), new { transactionId }, lineItem);
    }

    [HttpGet("{transactionId}/items")]
    public async Task<ActionResult<List<LineItemResponse>>> GetLineItems(string transactionId)
    {
        _logger.LogInformation("HTTP GET /api/transactions/{TransactionId}/items", transactionId);
        var items = await _lineItemStorage.GetLineItemsAsync(transactionId);
        return Ok(items);
    }

    [HttpDelete("{transactionId}/items/{itemId}")]
    public async Task<IActionResult> RemoveLineItem(string transactionId, string itemId)
    {
        _logger.LogInformation("HTTP DELETE /api/transactions/{TransactionId}/items/{ItemId}", transactionId, itemId);

        // Get transaction to access currency
        var transaction = await _transactionStorage.GetTransactionAsync(transactionId);
        if (transaction == null)
        {
            return NotFound();
        }

        var deleted = await _lineItemStorage.RemoveLineItemAsync(transactionId, itemId);
        if (!deleted)
        {
            return NotFound();
        }

        // Update transaction total with currency-aware calculation
        var newTotal = await _lineItemStorage.GetTransactionTotalAsync(transactionId, transaction.Currency);
        await _transactionStorage.UpdateTransactionTotalAsync(transactionId, newTotal);

        return NoContent();
    }
}
