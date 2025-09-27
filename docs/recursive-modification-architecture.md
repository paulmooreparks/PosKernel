# Recursive Modification Architecture (NRF-Compliant)

## Core Principle: NRF Linked Items / Parent-Child Relationships

The POS Kernel supports **NRF-compliant linked items** architecture where modifications can themselves be modified. This follows National Retail Federation standards for parent-child line item relationships.

**CRITICAL**: The kernel maintains pure hierarchical transaction structure with NO cultural concepts. All hierarchy comes from `parent_line_item_id` relationships between actual line items.

## NRF Schema Implementation

Based on `modification_availability` table structure:
```sql
parent_sku VARCHAR(50) NOT NULL,    -- Product SKU or modification_id
parent_type VARCHAR(20) NOT NULL,   -- 'PRODUCT' or 'MODIFICATION'
modification_id VARCHAR(50) NOT NULL
```

This enables true recursive modification hierarchy where any modification can be the parent of another modification.

## Example: Set Drink Customization

When a customer orders "Traditional Kaya Toast Set" with "kopi siew dai":

**Kernel Transaction Structure:**
```
Line 2: TSET001 (Traditional Kaya Toast Set)  S$7.40  parent_line_item_id: null
Line 3: KOPI001 (Kopi)                        S$0.00  parent_line_item_id: 2
Line 4: MOD_LESS_SUGAR (Less Sugar)           S$0.00  parent_line_item_id: 3
```

**Receipt Display:**
```
Traditional Kaya Toast Set  S$7.40
  Kopi                      S$0.00
    less sugar              S$0.00
```

## Kernel Data Structure Requirements

The Rust kernel MUST implement `parent_line_item_id` field:

```rust
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct LineItem {
    pub line_number: u32,
    pub product_id: String,
    pub quantity: i32,
    pub unit_price: Decimal,
    pub extended_price: Decimal,
    pub parent_line_item_id: Option<u32>, // NRF linked item support
    // NO preparation_notes field - this is NOT a kernel concept
}
```

## ARCHITECTURAL VIOLATION: "preparation_notes"

**❌ WRONG - Current Violation:**
```json
{
  "line_number": 2,
  "product_id": "TSET001",
  "preparation_notes": "with Kopi less sugar"  // WRONG - Cultural concept in kernel
}
```

**✅ CORRECT - NRF-Compliant Structure:**
```json
// Line 2: Set (parent)
{
  "line_number": 2,
  "product_id": "TSET001",
  "unit_price": 7.40,
  "extended_price": 7.40,
  "parent_line_item_id": null
}

// Line 3: Kopi component (child of set)
{
  "line_number": 3,
  "product_id": "KOPI001",
  "unit_price": 0.00,
  "extended_price": 0.00,
  "parent_line_item_id": 2
}

// Line 4: Less sugar mod (child of kopi)
{
  "line_number": 4,
  "product_id": "MOD_LESS_SUGAR",
  "unit_price": 0.00,
  "extended_price": 0.00,
  "parent_line_item_id": 3
}
```

## Void Cascade Implementation

**Critical NRF Requirement**: When parent items are voided, all linked child items must be automatically voided to maintain transaction integrity.

```rust
pub fn void_line_item_with_children(&mut self, line_number: u32, reason: &str) -> Result<(), Error> {
    // 1. Find all child items recursively
    let children = self.find_all_children(line_number);
    
    // 2. Void children first (reverse hierarchy order)
    for child_line in children.iter().rev() {
        self.void_single_line_item(child_line.line_number, &format!("Parent voided: {}", reason))?;
    }
    
    // 3. Void parent item
    self.void_single_line_item(line_number, reason)?;
    
    Ok(())
}
```

## AI Tool Implementation

The AI must make separate tool calls for each level of modification:

```json
// Call 1: Add drink to set
{
    "product_sku": "TSET001",
    "customization_type": "drink", 
    "customization_value": "Kopi"
}

// Call 2: Add sugar modification to drink  
{
    "product_sku": "KOPI001",
    "customization_type": "preparation",
    "customization_value": "less sugar"
}
```

**Result**: Kernel creates separate line items with proper parent_line_item_id relationships, not text modifications.

## Key Architectural Points

1. **NRF Compliance**: Follows National Retail Federation linked item standards
2. **Culture-Neutral Kernel**: NO cultural concepts like "preparation_notes" - only structured data relationships
3. **Same Logic, Different Accounting**: Set components work exactly like standalone items, just with different pricing
   - Standalone: KOPI001 (S$3.40) as separate transaction line
   - Set component: KOPI001 (S$0.00) with parent_line_item_id pointing to set

4. **Full Inventory Tracking**: The kernel tracks actual drinks consumed (KOPI001) regardless of pricing context

5. **AI Translation Layer**: The AI handles cultural/linguistic translation ("kopi siew dai" → structured modification hierarchy)

6. **Kernel Receives Structured Data**: The kernel never parses kopitiam terminology or stores cultural strings

7. **Void Cascade**: Voiding parent items automatically voids all child modifications (NRF requirement)

## Receipt Formatting

The receipt formatter reads the kernel's hierarchical structure directly by traversing parent_line_item_id relationships:

```csharp
// CORRECT - Read kernel hierarchy
private void RenderLineItemWithChildren(LineItem item, int indentLevel) 
{
    WriteLine($"{new string(' ', indentLevel * 2)}{item.ProductName} {FormatCurrency(item.ExtendedPrice)}");
    
    var children = transaction.LineItems.Where(li => li.ParentLineItemId == item.LineNumber);
    foreach (var child in children) {
        RenderLineItemWithChildren(child, indentLevel + 1);
    }
}
```

**This is NOT parsing presentation notes** - it's reading the actual NRF-compliant kernel transaction structure.

## Implementation Requirements

- **Rust Kernel**: Remove `preparation_notes` field, implement proper `parent_line_item_id` linking
- **AI Tool**: Support separate tool calls that create hierarchical line items
- **C# Client**: Handle parent relationships when adding modifications via kernel APIs
- **Receipt Formatter**: Traverse kernel hierarchy using parent_line_item_id relationships
- **Void Logic**: Implement cascading void for parent-child relationships
- **Inventory/Reporting**: Get complete component tracking from actual line items, not text parsing

## DESIGN DEFICIENCY: Current State

The current implementation stores cultural interpretation ("with Kopi less sugar") instead of structured data relationships. This violates the culture-neutral kernel principle and prevents proper NRF compliance.

**Fix Required**: Remove preparation_notes concept entirely and implement proper hierarchical line item structure with parent_line_item_id relationships.
