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

using PosKernel.Abstractions;

namespace PosKernel.AI.Models
{
    /// <summary>
    /// Represents a line item in a receipt.
    /// </summary>
    public class ReceiptLineItem
    {
        /// <summary>
        /// ARCHITECTURAL COMPONENT: Line item ID FROM KERNEL - not client-generated.
        /// Stable identifier assigned by kernel for precise modification tracking.
        /// </summary>
        public string LineItemId { get; set; } = "";
        
        /// <summary>
        /// ARCHITECTURAL COMPONENT: Line number from kernel transaction (1-based).
        /// May change due to voids/insertions - use LineItemId for stable references.
        /// </summary>
        public int LineNumber { get; set; }
        
        /// <summary>
        /// Number of items for this line item.
        /// </summary>
        public int Quantity { get; set; }
        
        /// <summary>
        /// Display name of the product.
        /// </summary>
        public string ProductName { get; set; } = "";
        
        /// <summary>
        /// Price per unit for this product.
        /// </summary>
        public decimal UnitPrice { get; set; }
        
        /// <summary>
        /// ARCHITECTURAL COMPONENT: Product SKU for set customization tracking.
        /// Multiple line items may share same SKU (multiple instances of same product).
        /// </summary>
        public string ProductSku { get; set; } = "";
        
        /// <summary>
        /// Total price for this line item (quantity × unit price).
        /// </summary>
        public decimal ExtendedPrice => UnitPrice * Quantity;

        /// <summary>
        /// Gets or sets the calculated total for compatibility.
        /// </summary>
        public decimal CalculatedTotal => ExtendedPrice;

        /// <summary>
        /// Gets or sets the line ID for compatibility.
        /// </summary>
        public string? LineId { get; set; }
        
        /// <summary>
        /// Gets or sets the parent line item ID for linked item support.
        /// Used to correlate items that are part of a configurable product or bundle.
        /// </summary>
        public uint? ParentLineItemId { get; set; } // NRF linked item support
    }
    
    /// <summary>
    /// Represents a complete receipt/transaction.
    /// </summary>
    public class Receipt
    {
        /// <summary>
        /// Gets or sets the store information.
        /// </summary>
        public StoreInfo Store { get; set; } = new();
        
        /// <summary>
        /// Gets or sets the transaction ID.
        /// </summary>
        public string TransactionId { get; set; } = "";
        
        /// <summary>
        /// Gets or sets the line items.
        /// </summary>
        public List<ReceiptLineItem> Items { get; set; } = new();
        
        /// <summary>
        /// Gets the subtotal of all items.
        /// </summary>
        public decimal Subtotal => Items.Sum(i => i.CalculatedTotal);
        
        /// <summary>
        /// Gets or sets the tax amount.
        /// </summary>
        public decimal Tax { get; set; } = 0;
        
        /// <summary>
        /// Gets the total amount.
        /// </summary>
        public decimal Total => Subtotal + Tax;
        
        /// <summary>
        /// Gets or sets the payment status.
        /// </summary>
        public PaymentStatus Status { get; set; } = PaymentStatus.Building;
        
        /// <summary>
        /// Gets or sets the timestamp.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
    
    /// <summary>
    /// Store information for receipt display.
    /// </summary>
    public class StoreInfo
    {
        /// <summary>
        /// Gets or sets the store name.
        /// </summary>
        public string Name { get; set; } = "";
        
        /// <summary>
        /// Gets or sets the currency code.
        /// </summary>
        public string Currency { get; set; } = "";
        
        /// <summary>
        /// Gets or sets the store type.
        /// </summary>
        public string StoreType { get; set; } = "";
    }
    
    /// <summary>
    /// Payment status enumeration.
    /// </summary>
    public enum PaymentStatus
    {
        /// <summary>
        /// Order is being built.
        /// </summary>
        Building,
        
        /// <summary>
        /// Order is ready for payment.
        /// </summary>
        ReadyForPayment,
        
        /// <summary>
        /// Payment is being processed.
        /// </summary>
        Processing,
        
        /// <summary>
        /// Payment completed successfully.
        /// </summary>
        Completed,
        
        /// <summary>
        /// Order was cancelled.
        /// </summary>
        Cancelled
    }
}
