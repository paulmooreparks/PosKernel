    /// <summary>
    /// Represents a line item on a receipt for display purposes.
    /// Each line item should have a unique identifier for modification tracking.
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
        /// ARCHITECTURAL COMPONENT: Parent line item ID for linked item support.
        /// Used to associate line items that are modifications or extras of a base item.
        /// </summary>
        public uint? ParentLineItemId { get; set; }
    }
