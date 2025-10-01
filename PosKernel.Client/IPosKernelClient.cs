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

namespace PosKernel.Client
{
    /// <summary>
    /// NRF ARTS-compliant line item type enumeration.
    /// Defines the standard types of line items in POS transactions.
    /// </summary>
    public enum LineItemType
    {
        /// <summary>
        /// Standard sale item.
        /// </summary>
        Sale = 0,

        /// <summary>
        /// Return/refund item.
        /// </summary>
        Return = 1,

        /// <summary>
        /// Void/cancel item.
        /// </summary>
        Void = 2,

        /// <summary>
        /// Modification to another item (e.g., "extra cheese").
        /// </summary>
        Modification = 3,

        /// <summary>
        /// Discount applied to item or transaction.
        /// </summary>
        Discount = 4,

        /// <summary>
        /// Tax line item.
        /// </summary>
        Tax = 5,

        /// <summary>
        /// Service charge or fee.
        /// </summary>
        ServiceCharge = 6,

        /// <summary>
        /// Tip or gratuity.
        /// </summary>
        Tip = 7
    }

    /// <summary>
    /// Client interface for communicating with POS Kernel Service.
    /// Provides the same API as the in-process kernel but over IPC.
    /// </summary>
    public interface IPosKernelClient : IDisposable
    {
        /// <summary>
        /// Connects to the POS Kernel Service.
        /// </summary>
        Task ConnectAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Disconnects from the POS Kernel Service.
        /// </summary>
        Task DisconnectAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Disconnects from the POS Kernel Service (synchronous version).
        /// </summary>
        void Disconnect();

        /// <summary>
        /// Gets store configuration information.
        /// </summary>
        Task<object> GetStoreConfigAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the connection status.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Creates a new session.
        /// </summary>
        Task<string> CreateSessionAsync(string terminalId, string operatorId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts a new transaction.
        /// ARCHITECTURAL PRINCIPLE: Currency must be explicitly provided - no cultural defaults.
        /// </summary>
        Task<TransactionClientResult> StartTransactionAsync(string sessionId, string currency, CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds a line item to the transaction.
        /// ARCHITECTURAL PRINCIPLE: Accepts optional product metadata from store extension.
        /// Kernel stores all provided metadata for display - does not perform lookups.
        /// </summary>
        Task<TransactionClientResult> AddLineItemAsync(string sessionId, string transactionId, string productId, int quantity, decimal unitPrice, string? productName = null, string? productDescription = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds a modification to an existing line item (NRF ARTS-compliant hierarchical).
        /// ARCHITECTURAL PRINCIPLE: Implements NRF standards for product modifications with proper parent-child relationships.
        /// </summary>
        Task<TransactionClientResult> AddModificationAsync(string sessionId, string transactionId, int parentLineNumber, string modificationId, int quantity, decimal unitPrice, LineItemType itemType = LineItemType.Modification, CancellationToken cancellationToken = default);

        /// <summary>
        /// Voids a line item from the transaction by line number.
        /// Creates a reversing entry to maintain audit trail compliance with POS accounting standards.
        /// </summary>
        Task<TransactionClientResult> VoidLineItemAsync(string sessionId, string transactionId, int lineNumber, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates the quantity of a line item in the transaction.
        /// </summary>
        Task<TransactionClientResult> UpdateLineItemQuantityAsync(string sessionId, string transactionId, int lineNumber, int newQuantity, CancellationToken cancellationToken = default);

        /// <summary>
        /// Processes payment for the transaction.
        /// </summary>
        Task<TransactionClientResult> ProcessPaymentAsync(string sessionId, string transactionId, decimal amount, string paymentType = "cash", CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current transaction state.
        /// </summary>
        Task<TransactionClientResult> GetTransactionAsync(string sessionId, string transactionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Closes a session.
        /// </summary>
        Task CloseSessionAsync(string sessionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds a child line item with parent relationship for NRF-compliant hierarchical modifications.
        /// ARCHITECTURAL PRINCIPLE: This replaces preparation notes with proper parent-child line item relationships.
        /// </summary>
        Task<TransactionClientResult> AddChildLineItemAsync(string sessionId, string transactionId, string productId, int quantity, decimal unitPrice, int parentLineNumber, string? productName = null, string? productDescription = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds a modification to a specific line item using stable line item ID for precise targeting.
        /// ARCHITECTURAL PRINCIPLE: Eliminates ambiguity when multiple items have the same SKU.
        /// </summary>
        Task<TransactionClientResult> AddModificationByLineItemIdAsync(string sessionId, string transactionId, string lineItemId, string modificationSku, int quantity, decimal unitPrice, CancellationToken cancellationToken = default);

        /// <summary>
        /// Voids a line item using stable line item ID for precise targeting.
        /// ARCHITECTURAL PRINCIPLE: Stable ID targeting eliminates ambiguity in multi-item scenarios.
        /// </summary>
        Task<TransactionClientResult> VoidLineItemByIdAsync(string sessionId, string transactionId, string lineItemId, string reason = "customer requested", CancellationToken cancellationToken = default);

        /// <summary>
        /// Modifies a line item property using stable line item ID for precise targeting.
        /// ARCHITECTURAL PRINCIPLE: Use stable IDs for unambiguous line item targeting.
        /// </summary>
        Task<TransactionClientResult> ModifyLineItemByIdAsync(string sessionId, string transactionId, string lineItemId, string modificationType, string newValue, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Result from client transaction operations.
    /// </summary>
    public class TransactionClientResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the operation succeeded.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the error message if the operation failed.
        /// </summary>
        public string? Error { get; set; }

        /// <summary>
        /// Gets or sets the transaction identifier.
        /// </summary>
        public string? TransactionId { get; set; }

        /// <summary>
        /// Gets or sets the session identifier.
        /// </summary>
        public string? SessionId { get; set; }

        /// <summary>
        /// Gets or sets the total amount.
        /// </summary>
        public decimal Total { get; set; }

        /// <summary>
        /// Gets or sets the transaction state.
        /// </summary>
        public string State { get; set; } = "";

        /// <summary>
        /// Gets or sets the hierarchical line items in the transaction (NRF ARTS-compliant).
        /// </summary>
        public List<TransactionLineItem> LineItems { get; set; } = new();

        /// <summary>
        /// Gets or sets additional result data.
        /// </summary>
        public object? Data { get; set; }
    }

    /// <summary>
    /// Represents a line item in a transaction with NRF ARTS-compliant hierarchical support.
    /// Based on industry standards for POS transaction line items with parent-child relationships.
    /// </summary>
    public class TransactionLineItem
    {
        /// <summary>
        /// Gets or sets the stable line item identifier assigned by the kernel.
        /// ARCHITECTURAL PRINCIPLE: Stable ID remains constant even when line numbers change due to voids.
        /// Use this for precise targeting of modifications, not the line number.
        /// </summary>
        public string LineItemId { get; set; } = "";

        /// <summary>
        /// Gets or sets the line number (1-based for POS display/void operations).
        /// NOTE: Line numbers may change due to voids/insertions. Use LineItemId for stable references.
        /// </summary>
        public int LineNumber { get; set; }

        /// <summary>
        /// Gets or sets the parent line number (0 for base items, parent line for modifications).
        /// </summary>
        public int ParentLineNumber { get; set; }

        /// <summary>
        /// Gets or sets the product identifier or modification ID.
        /// </summary>
        public string ProductId { get; set; } = "";

        /// <summary>
        /// Gets or sets the product name from store extension.
        /// </summary>
        public string ProductName { get; set; } = "";

        /// <summary>
        /// Gets or sets the product description from store extension.
        /// </summary>
        public string ProductDescription { get; set; } = "";

        /// <summary>
        /// Gets or sets the NRF ARTS-compliant line item type.
        /// </summary>
        public LineItemType ItemType { get; set; }

        /// <summary>
        /// Gets or sets the quantity.
        /// </summary>
        public int Quantity { get; set; }

        /// <summary>
        /// Gets or sets the unit price (with proper currency handling via services).
        /// </summary>
        public decimal UnitPrice { get; set; }

        /// <summary>
        /// Gets or sets the extended price (calculated by kernel, not assumed).
        /// </summary>
        public decimal ExtendedPrice { get; set; }

        /// <summary>
        /// Gets or sets the display indent level for NRF-style receipt formatting.
        /// </summary>
        public int DisplayIndentLevel { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this line item is voided.
        /// </summary>
        public bool IsVoided { get; set; }

        /// <summary>
        /// Gets or sets the void reason.
        /// </summary>
        public string? VoidReason { get; set; }

        /// <summary>
        /// Gets or sets additional metadata.
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Configuration options for POS Kernel client connections.
    /// </summary>
    public class PosKernelClientOptions
    {
        /// <summary>
        /// Gets or sets the named pipe name.
        /// </summary>
        public string PipeName { get; set; } = "poskernel-service";

        /// <summary>
        /// Gets or sets the connection timeout in milliseconds.
        /// </summary>
        public int ConnectionTimeoutMs { get; set; } = 5000;

        /// <summary>
        /// Gets or sets the request timeout in milliseconds.
        /// </summary>
        public int RequestTimeoutMs { get; set; } = 30000;

        /// <summary>
        /// Gets or sets the maximum retry attempts.
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// Gets or sets the retry delay in milliseconds.
        /// </summary>
        public int RetryDelayMs { get; set; } = 1000;

        /// <summary>
        /// Gets or sets a value indicating whether to auto-reconnect on failure.
        /// </summary>
        public bool AutoReconnect { get; set; } = true;

        /// <summary>
        /// Gets or sets the keep-alive interval in milliseconds.
        /// </summary>
        public int KeepAliveIntervalMs { get; set; } = 30000;
    }
}
