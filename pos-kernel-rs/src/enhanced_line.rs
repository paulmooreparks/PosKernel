// Enhanced Line Structure for POS Accounting Compliance
// Add this to pos-kernel-rs/src/lib.rs around line 5 (after existing use statements)
use std::time::SystemTime;

// Replace the existing Line struct (around line 201) with this enhanced version:
#[derive(Debug, Clone)]
struct Line { 
    sku: String,
    qty: i32,  // Negative quantities represent voids/reversals
    unit_minor: i64,
    line_number: u32,  // 1-based line number for customer reference
    entry_type: EntryType,
    void_reason: Option<String>,
    references_line: Option<u32>,  // Links void entries back to original
    timestamp: SystemTime,
    operator_id: Option<String>,
}

#[derive(Debug, Clone, PartialEq)]
enum EntryType {
    Sale,        // Original sale entry
    Void,        // Reversing entry for void (maintains audit trail)
    Adjustment,  // Quantity/price adjustments
}

impl Line {
    fn new(sku: String, qty: i32, unit_minor: i64) -> Self {
        // Temporary compatibility with existing code
        Self::new_sale(sku, qty, unit_minor, 1, None)
    }
    
    fn new_sale(sku: String, qty: i32, unit_minor: i64, line_number: u32, operator_id: Option<String>) -> Self {
        Self {
            sku,
            qty,
            unit_minor,
            line_number,
            entry_type: EntryType::Sale,
            void_reason: None,
            references_line: None,
            timestamp: SystemTime::now(),
            operator_id,
        }
    }
    
    fn new_void(original: &Line, line_number: u32, reason: String, operator_id: Option<String>) -> Self {
        Self {
            sku: original.sku.clone(),
            qty: -original.qty,  // Negative for reversal
            unit_minor: original.unit_minor,
            line_number,
            entry_type: EntryType::Void,
            void_reason: Some(reason),
            references_line: Some(original.line_number),
            timestamp: SystemTime::now(),
            operator_id,
        }
    }
}
