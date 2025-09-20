# Rust Kernel Void Implementation Plan

## Overview
This document outlines the implementation plan for adding POS-compliant void functionality to the Rust POS Kernel. The implementation follows accounting standards that require audit trail preservation through reversing entries rather than destructive deletions.

## Files to Modify

### 1. pos-kernel-rs/src/lib.rs
**Required Changes:**

#### A. Enhanced Line Structure (around line 201)
Replace current Line struct with:
```rust
use std::time::SystemTime;

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
```

#### B. Enhanced Transaction Methods
Add these methods to `LegalTransaction` impl block (around line 603):
```rust
impl LegalTransaction {
    // Update existing add_line method to use new Line constructor
    fn add_line(&mut self, sku: String, qty: i32, unit_minor: i64) {
        let line_number = self.next_line_number();
        self.lines.push(Line::new_sale(sku, qty, unit_minor, line_number, None));
        self._recovery_point += 1;
    }
    
    // NEW: Void line item by line number
    fn void_line_item(&mut self, line_number: u32, reason: String, operator_id: Option<String>) -> Result<(), String> {
        // Find original line item
        let original_line = self.lines.iter()
            .find(|line| line.line_number == line_number && line.entry_type == EntryType::Sale)
            .ok_or("Line item not found or already voided")?
            .clone();
        
        // Check if already voided
        let already_voided = self.lines.iter()
            .any(|line| line.entry_type == EntryType::Void && line.references_line == Some(line_number));
        
        if already_voided {
            return Err("Line item already voided".to_string());
        }
        
        // Create reversing entry
        let void_line_number = self.next_line_number();
        let void_entry = Line::new_void(&original_line, void_line_number, reason, operator_id);
        
        self.lines.push(void_entry);
        self._recovery_point += 1;
        Ok(())
    }
    
    // NEW: Update line item quantity
    fn update_line_quantity(&mut self, line_number: u32, new_quantity: i32, operator_id: Option<String>) -> Result<(), String> {
        // Find original line item
        let original_line = self.lines.iter()
            .find(|line| line.line_number == line_number && line.entry_type == EntryType::Sale)
            .ok_or("Line item not found")?;
        
        if new_quantity <= 0 {
            return Err("Use void_line_item for removing items completely".to_string());
        }
        
        // Calculate effective quantity including any previous adjustments
        let effective_qty = self.calculate_effective_quantity_for_line(line_number);
        let qty_diff = new_quantity - effective_qty;
        
        if qty_diff != 0 {
            let adjustment_line_number = self.next_line_number();
            let adjustment_entry = Line {
                sku: original_line.sku.clone(),
                qty: qty_diff,
                unit_minor: original_line.unit_minor,
                line_number: adjustment_line_number,
                entry_type: EntryType::Adjustment,
                void_reason: Some(format!("Quantity changed from {} to {}", effective_qty, new_quantity)),
                references_line: Some(line_number),
                timestamp: SystemTime::now(),
                operator_id,
            };
            
            self.lines.push(adjustment_entry);
            self._recovery_point += 1;
        }
        
        Ok(())
    }
    
    // Helper: Get next line number
    fn next_line_number(&self) -> u32 {
        self.lines.len() as u32 + 1
    }
    
    // Helper: Calculate effective quantity for a specific line (considering adjustments)
    fn calculate_effective_quantity_for_line(&self, line_number: u32) -> i32 {
        self.lines.iter()
            .filter(|line| line.line_number == line_number || line.references_line == Some(line_number))
            .map(|line| line.qty)
            .sum()
    }
    
    // Helper: Calculate total considering all entries
    fn calculate_effective_total(&self) -> i64 {
        self.lines.iter()
            .map(|line| (line.qty as i64) * line.unit_minor)
            .sum()
    }
}
```

#### C. Store-Level Operations
Add these methods to `LegalKernelStore` impl block (around line 680):
```rust
impl LegalKernelStore {
    // NEW: Void line item operation
    fn void_line_item_legal(&mut self, handle: u64, line_number: u32, reason: String, operator_id: Option<String>) -> Result<(), Box<dyn std::error::Error>> {
        let tx = self.active_transactions.get_mut(&handle)
            .ok_or("Transaction not found")?;
        
        match tx.state {
            LegalTxState::Building => {
                if tx.is_expired(self.system_config.transaction_timeout_seconds) {
                    return Err("Transaction expired".into());
                }
                
                tx.void_line_item(line_number, reason, operator_id)?;
                tx.last_activity = SystemTime::now();
                
                // WAL logging for audit compliance
                self.log_wal_entry("VOID_LINE", handle, &format!("line:{}, reason:{}", line_number, reason))?;
                
                Ok(())
            },
            _ => Err("Transaction not in building state".into())
        }
    }
    
    // NEW: Update line item quantity operation
    fn update_line_quantity_legal(&mut self, handle: u64, line_number: u32, new_quantity: i32, operator_id: Option<String>) -> Result<(), Box<dyn std::error::Error>> {
        let tx = self.active_transactions.get_mut(&handle)
            .ok_or("Transaction not found")?;
        
        match tx.state {
            LegalTxState::Building => {
                if tx.is_expired(self.system_config.transaction_timeout_seconds) {
                    return Err("Transaction expired".into());
                }
                
                tx.update_line_quantity(line_number, new_quantity, operator_id)?;
                tx.last_activity = SystemTime::now();
                
                // WAL logging for audit compliance  
                self.log_wal_entry("UPDATE_QTY", handle, &format!("line:{}, qty:{}", line_number, new_quantity))?;
                
                Ok(())
            },
            _ => Err("Transaction not in building state".into())
        }
    }
}
```

#### D. C FFI Functions
Add these extern "C" functions (around line 1552):
```rust
#[no_mangle]
pub extern "C" fn pk_void_line_item(
    handle: PkTransactionHandle,
    line_number: u32,
    reason_ptr: *const u8,
    reason_len: usize,
    operator_ptr: *const u8,
    operator_len: usize,
) -> PkResult {
    if handle == PK_INVALID_HANDLE {
        return PkResult::err(ResultCode::ValidationFailed);
    }
    
    let reason = unsafe { read_str(reason_ptr, reason_len) };
    let operator_id = if operator_ptr.is_null() { 
        None 
    } else { 
        Some(unsafe { read_str(operator_ptr, operator_len) })
    };
    
    let mut store = match legal_kernel_store().write() {
        Ok(s) => s,
        Err(e) => {
            eprintln!("CRITICAL: Failed to acquire kernel store write lock: {}", e);
            return PkResult::err(ResultCode::InternalError);
        }
    };
    
    match store.void_line_item_legal(handle, line_number, reason, operator_id) {
        Ok(()) => {
            eprintln!("INFO: Voided line {} in transaction {} with reason: {}", line_number, handle, reason);
            PkResult::ok()
        },
        Err(e) => {
            eprintln!("ERROR: Void line item failed: {}", e);
            PkResult::err(ResultCode::ValidationFailed)
        }
    }
}

#[no_mangle]
pub extern "C" fn pk_update_line_quantity(
    handle: PkTransactionHandle,
    line_number: u32,
    new_quantity: i32,
    operator_ptr: *const u8,
    operator_len: usize,
) -> PkResult {
    if handle == PK_INVALID_HANDLE || new_quantity <= 0 {
        return PkResult::err(ResultCode::ValidationFailed);
    }
    
    let operator_id = if operator_ptr.is_null() { 
        None 
    } else { 
        Some(unsafe { read_str(operator_ptr, operator_len) })
    };
    
    let mut store = match legal_kernel_store().write() {
        Ok(s) => s,
        Err(e) => {
            eprintln!("CRITICAL: Failed to acquire kernel store write lock: {}", e);
            return PkResult::err(ResultCode::InternalError);
        }
    };
    
    match store.update_line_quantity_legal(handle, line_number, new_quantity, operator_id) {
        Ok(()) => {
            eprintln!("INFO: Updated line {} quantity to {} in transaction {}", line_number, new_quantity, handle);
            PkResult::ok()
        },
        Err(e) => {
            eprintln!("ERROR: Update line quantity failed: {}", e);
            PkResult::err(ResultCode::ValidationFailed)
        }
    }
}
```

### 2. pos-kernel-rs/src/bin/service.rs
**Required Changes:**

#### A. Add Request/Response Structures
Add these near the other request structures:
```rust
#[derive(Deserialize)]
struct VoidLineRequest {
    reason: String,
    operator_id: Option<String>,
}

#[derive(Deserialize)]  
struct UpdateQuantityRequest {
    new_quantity: i32,
    operator_id: Option<String>,
}
```

#### B. Add HTTP Route Handlers
Add these handlers:
```rust
async fn void_line_item_handler(
    axum::extract::State(state): axum::extract::State<SharedState>,
    Path((session_id, transaction_id, line_number)): Path<(String, String, u32)>,
    Json(request): Json<VoidLineRequest>,
) -> Json<TransactionResponse> {
    // Get transaction handle
    let handle = {
        let state_guard = state.lock().unwrap();
        if let Some(session) = state_guard.sessions.get(&session_id) {
            if let Some(&handle) = session.transactions.get(&transaction_id) {
                handle
            } else {
                return Json(TransactionResponse {
                    success: false,
                    error: "Transaction not found".to_string(),
                    session_id,
                    transaction_id,
                    total: 0.0,
                    tendered: 0.0,
                    change: 0.0,
                    state: "Failed".to_string(),
                    line_count: 0,
                });
            }
        } else {
            return Json(TransactionResponse {
                success: false,
                error: "Session not found".to_string(),
                session_id,
                transaction_id,
                total: 0.0,
                tendered: 0.0,
                change: 0.0,
                state: "Failed".to_string(),
                line_count: 0,
            });
        }
    };
    
    // Convert reason and operator_id to C-compatible format
    let reason_bytes = request.reason.as_bytes();
    let operator_bytes = request.operator_id.as_ref().map(|s| s.as_bytes());
    
    // Call void function
    let result = pk_void_line_item(
        handle,
        line_number,
        reason_bytes.as_ptr(),
        reason_bytes.len(),
        operator_bytes.map(|b| b.as_ptr()).unwrap_or(std::ptr::null()),
        operator_bytes.map(|b| b.len()).unwrap_or(0),
    );
    
    if pk_result_get_code(result) != 0 {
        return Json(TransactionResponse {
            success: false,
            error: format!("Failed to void line item {}: {}", line_number, request.reason),
            session_id,
            transaction_id,
            total: 0.0,
            tendered: 0.0,
            change: 0.0,
            state: "Failed".to_string(),
            line_count: 0,
        });
    }
    
    // Get updated transaction state
    let mut total_minor = 0i64;
    let mut tendered_minor = 0i64;
    let mut change_minor = 0i64;
    let mut state_code = 0i32;
    
    let totals_result = pk_get_totals(handle, &mut total_minor, &mut tendered_minor, &mut change_minor, &mut state_code);
    
    if pk_result_get_code(totals_result) != 0 {
        return Json(TransactionResponse {
            success: false,
            error: "Failed to get updated totals after void".to_string(),
            session_id,
            transaction_id,
            total: 0.0,
            tendered: 0.0,
            change: 0.0,
            state: "Failed".to_string(),
            line_count: 0,
        });
    }
    
    let mut line_count = 0u32;
    let line_count_result = pk_get_line_count(handle, &mut line_count);
    if pk_result_get_code(line_count_result) != 0 {
        line_count = 0;
    }
    
    info!("Voided line item {} in transaction {}, new total: {}", line_number, transaction_id, total_minor);
    
    Json(TransactionResponse {
        success: true,
        error: String::new(),
        session_id,
        transaction_id,
        total: total_minor as f64 / 100.0,
        tendered: tendered_minor as f64 / 100.0,
        change: change_minor as f64 / 100.0,
        state: if state_code == 0 { "Building" } else { "Completed" }.to_string(),
        line_count,
    })
}

async fn update_line_quantity_handler(
    axum::extract::State(state): axum::extract::State<SharedState>,
    Path((session_id, transaction_id, line_number)): Path<(String, String, u32)>,
    Json(request): Json<UpdateQuantityRequest>,
) -> Json<TransactionResponse> {
    // Similar implementation to void_line_item_handler but calling pk_update_line_quantity
    // ... (implementation follows same pattern)
}
```

#### C. Add Routes to Router
Update the router (in main function) to include:
```rust
.route("/api/sessions/:session_id/transactions/:transaction_id/lines/:line_number", 
       axum::routing::delete(void_line_item_handler)
       .put(update_line_quantity_handler))
```

## Implementation Steps

1. **Backup Current Implementation**: Already done (lib.rs.backup)

2. **Implement Enhanced Line Structure**: Replace Line struct and add EntryType enum

3. **Update Transaction Methods**: Add void_line_item, update_line_quantity, and helper methods

4. **Add Store Operations**: Add void_line_item_legal and update_line_quantity_legal methods

5. **Add C FFI Exports**: Add pk_void_line_item and pk_update_line_quantity functions

6. **Update Service HTTP API**: Add void and update endpoints to service.rs

7. **Test Integration**: Verify C# client can call new Rust kernel functions

8. **Validate POS Compliance**: Ensure audit trail preservation and proper error handling

## POS Accounting Standards Compliance

This implementation ensures:
- ✅ **Audit Trail Preservation**: Original entries are never deleted
- ✅ **Reversing Entries**: Voids create negative quantity entries
- ✅ **Reference Tracking**: Void entries reference original line numbers  
- ✅ **Reason Code Tracking**: All voids include reason for management reporting
- ✅ **Timestamp Tracking**: All entries have timestamps for chronological audit
- ✅ **Operator Tracking**: Who performed each operation is recorded
- ✅ **WAL Logging**: Write-ahead logging for transaction recovery

## Testing Scenarios

After implementation, test these scenarios:
1. **Basic Void**: Void a line item and verify total recalculation
2. **Double Void Prevention**: Attempt to void already-voided item
3. **Quantity Update**: Change quantity and verify adjustment entry creation  
4. **HTTP API**: Test DELETE and PUT endpoints via C# client
5. **Audit Trail**: Verify all entries preserved with proper references
6. **Customer Scenario**: Test "change kopi to siew dai" use case

This implementation provides the foundation for the AI system to properly handle item modifications and removals while maintaining compliance with POS accounting standards.
