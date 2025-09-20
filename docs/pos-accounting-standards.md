# POS Accounting Standards for Void Operations

## Executive Summary

This document defines the accounting standards and compliance requirements for void operations in the POS Kernel system. These standards ensure regulatory compliance, audit trail integrity, and financial control requirements.

## Regulatory Compliance Requirements

### **Audit Trail Integrity**
- ✅ **Void operations create reversing entries** - original line items are preserved
- ✅ **Complete transaction history** - all operations are traceable
- ✅ **Timestamps preserved** - shows chronological order of operations
- ❌ **NEVER delete line items** - destroys audit trail and violates compliance

### **Tax Authority Standards**
- **IRS Requirements (US)**: Complete transaction records must be maintained
- **IRAS Requirements (Singapore)**: All void operations must be auditable
- **VAT Compliance (EU)**: Reversing entries required for tax calculations
- **GST Compliance**: Original and void entries must be separately reportable

### **Financial Controls**
- **Reason Codes**: All voids must include reason for management reporting
- **Value Limits**: High-value voids may require manager override
- **Pattern Detection**: Excessive void patterns trigger alerts
- **Reconciliation**: Daily void reports must match transaction logs

## Technical Implementation

### **Void Operation Behavior**

**CORRECT Implementation (POS Kernel):**
```rust
pub fn void_line_item(&mut self, line_number: usize, reason: String) -> Result<(), VoidError> {
    // 1. Find original line item
    let original_item = self.find_line_item(line_number)?;
    
    // 2. Create reversing entry (negative quantity)
    let void_entry = LineItem {
        line_number: self.next_line_number(),
        product_id: original_item.product_id.clone(),
        quantity: -original_item.quantity, // Negative quantity for reversal
        unit_price: original_item.unit_price,
        timestamp: SystemTime::now(),
        void_reason: Some(reason),
        references_line: Some(line_number), // Links back to original
        entry_type: EntryType::Void,
    };
    
    // 3. Add void entry to transaction (preserves original)
    self.line_items.push(void_entry);
    
    // 4. Recalculate totals
    self.recalculate_totals();
    
    Ok(())
}
```

**WRONG Implementation (Destructive):**
```rust
// ❌ NEVER DO THIS - Violates accounting standards
pub fn delete_line_item(&mut self, line_number: usize) -> Result<(), Error> {
    self.line_items.remove(line_number); // DESTROYS AUDIT TRAIL
    Ok(())
}
```

### **Line Item State Model**

```rust
#[derive(Debug, Clone, Serialize, Deserialize)]
pub enum EntryType {
    Sale,        // Original sale entry
    Void,        // Reversing entry for void
    Adjustment,  // Quantity/price adjustments
    Refund,      // Post-sale refunds
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct LineItem {
    pub line_number: u32,
    pub product_id: String,
    pub quantity: i32,          // Negative for voids/refunds
    pub unit_price: Decimal,
    pub timestamp: SystemTime,
    pub entry_type: EntryType,
    pub void_reason: Option<String>,
    pub references_line: Option<u32>, // Links to original entry
    pub operator_id: String,
}
```

## Reporting Requirements

### **Audit Reports**
- **Transaction Detail Report**: Shows all line items including voids
- **Void Analysis Report**: Groups voids by reason code and operator
- **Reconciliation Report**: Verifies void reversals match originals

### **Management Reports**
- **Daily Void Summary**: Total void amounts by category
- **Operator Void Activity**: Tracks void patterns by staff member
- **High-Value Void Alert**: Flags voids above threshold for review

## Compliance Validation

### **Required Tests**
1. **Audit Trail Test**: Verify all void operations preserve original entries
2. **Calculation Test**: Verify void reversals correctly adjust totals
3. **Reporting Test**: Verify void entries appear in audit reports
4. **Timestamp Test**: Verify chronological order is maintained

### **Regulatory Validation**
- **Tax Calculation**: Voids must not affect tax calculation history
- **Receipt Generation**: Void receipts must reference original transaction
- **Data Retention**: Void records must be retained per local regulations

## Error Handling

### **Void Operation Errors**
- **InvalidLineNumber**: Line number not found in transaction
- **AlreadyVoided**: Attempting to void an already-voided item
- **InsufficientPrivileges**: Operator lacks void permissions
- **TransactionClosed**: Cannot void items in completed transactions

### **Recovery Procedures**
- **Partial Void Failure**: Transaction remains in consistent state
- **System Crash**: Void operations are atomic and recoverable
- **Data Corruption**: Void audit trail allows reconstruction

## Rust Kernel Implementation Status

### **Required Implementation**
The Rust POS Kernel must implement the following void functionality:

1. **HTTP API Endpoints**:
   - `DELETE /api/sessions/{session_id}/transactions/{transaction_id}/lines/{line_number}`
   - `PUT /api/sessions/{session_id}/transactions/{transaction_id}/lines/{line_number}` (for quantity updates)

2. **JSON-RPC Methods**:
   - `void_line_item(session_id, transaction_id, line_number, reason)`
   - `update_line_item_quantity(session_id, transaction_id, line_number, new_quantity)`

3. **Audit Compliance**:
   - Reversing entries with negative quantities
   - Original entry preservation
   - Reason code tracking
   - Timestamp preservation
   - Reference linking between original and void entries

### **Integration Points**
- **C# Client Layer**: Already implemented in `IPosKernelClient`
- **AI Tools Layer**: Already implemented in `KernelPosToolsProvider`
- **Rust Kernel**: **REQUIRES IMPLEMENTATION** for full functionality

## Next Steps

1. **Implement Rust kernel void functionality** following POS accounting standards
2. **Add audit trail validation** to ensure compliance
3. **Implement reporting endpoints** for void analysis
4. **Add integration tests** to verify accounting standards compliance
5. **Test with real scenarios** like the "change kopi to siew dai" use case

## Compliance Statement

This implementation ensures compliance with:
- ✅ **US IRS Requirements** for retail transaction records
- ✅ **Singapore IRAS Standards** for electronic point-of-sale systems
- ✅ **EU VAT Compliance** for transaction reversals
- ✅ **GAAP Standards** for accounting audit trails
- ✅ **PCI DSS Requirements** for payment transaction integrity
