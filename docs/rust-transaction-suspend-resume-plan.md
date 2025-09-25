# Rust Kernel Transaction Suspend/Resume Implementation Plan

## Overview
Implement transaction suspend/resume functionality in the POS Kernel following NRF/industry standards and integrating with the existing ACID-compliant architecture.

## Requirements Analysis

### Transaction Void (Already Complete)
The kernel already implements transaction void through `pk_abort_transaction_legal()`. This provides:
- ✅ Complete transaction cancellation
- ✅ WAL logging with reason codes
- ✅ State management (Aborting → Aborted)
- ✅ Operator ID tracking

**Recommendation**: Add alias `pk_void_transaction()` for API clarity, but functionality is complete.

### Transaction Suspend/Resume (New Implementation Required)

#### New Data Structures Needed

```rust
#[derive(Debug, Clone)]
pub enum LegalTxState {
    Building,
    Committing,
    Committed,
    Aborting,
    Aborted,
    TimedOut,
    Suspended,     // NEW - Transaction suspended for later resume
    Resuming,      // NEW - Transaction being restored from suspension
}

#[derive(Debug, Clone)]
pub struct SuspendedTransaction {
    suspend_id: String,           // Unique suspend identifier (barcode-friendly)
    original_handle: u64,         // Original transaction handle
    suspend_timestamp: SystemTime,
    suspend_operator_id: String,  // Who suspended it
    suspend_reason: Option<String>,
    expires_at: SystemTime,       // Auto-void timeout
    terminal_id: String,          // Where it was suspended
    transaction_data: String,     // Serialized transaction state (JSON)
    barcode_data: String,         // Barcode content for receipt
}

#[derive(Debug, Clone)]
pub enum WalOperationType {
    // ... existing operations ...
    TransactionSuspend { 
        suspend_id: String, 
        operator_id: String, 
        reason: Option<String>,
        expires_at: SystemTime 
    },
    TransactionResume { 
        suspend_id: String, 
        new_handle: u64, 
        operator_id: String 
    },
    SuspendedTransactionExpired { 
        suspend_id: String 
    },
}
```

#### Suspend Process Implementation

```rust
impl LegalKernelStore {
    fn suspend_transaction_legal(
        &mut self, 
        handle: u64, 
        operator_id: String,
        reason: Option<String>
    ) -> Result<String, Box<dyn std::error::Error>> {
        // 1. Validate transaction can be suspended
        let tx = self.active_transactions.get_mut(&handle)
            .ok_or("Transaction not found")?;
        
        // Business rule validation
        if tx.state != LegalTxState::Building {
            return Err("Can only suspend building transactions".into());
        }
        
        if tx.has_active_payments() {
            return Err("Cannot suspend transactions with active payment lines".into());
        }
        
        if tx.has_gift_card_operations() {
            return Err("Cannot suspend transactions with gift card operations".into());
        }
        
        // 2. Generate unique suspend ID (format: SUSP-{timestamp}-{random})
        let suspend_id = generate_suspend_id();
        
        // 3. Serialize transaction data
        let transaction_data = serde_json::to_string(tx)?;
        
        // 4. Create suspended transaction record
        let expires_at = SystemTime::now() + Duration::from_secs(24 * 3600); // 24 hour default
        
        let suspended_tx = SuspendedTransaction {
            suspend_id: suspend_id.clone(),
            original_handle: handle,
            suspend_timestamp: SystemTime::now(),
            suspend_operator_id: operator_id.clone(),
            suspend_reason: reason.clone(),
            expires_at,
            terminal_id: get_current_terminal_id(),
            transaction_data,
            barcode_data: generate_barcode_data(&suspend_id),
        };
        
        // 5. Log to WAL
        self.wal.log_operation(
            handle,
            WalOperationType::TransactionSuspend {
                suspend_id: suspend_id.clone(),
                operator_id,
                reason,
                expires_at
            },
            &transaction_data
        )?;
        
        // 6. Persist suspended transaction
        self.suspended_transactions.insert(suspend_id.clone(), suspended_tx);
        self.persist_suspended_transaction(&suspend_id)?;
        
        // 7. Remove from active transactions
        tx.state = LegalTxState::Suspended;
        self.active_transactions.remove(&handle);
        self.transaction_timeouts.remove(&handle);
        
        Ok(suspend_id)
    }
}
```

#### Resume Process Implementation

```rust
impl LegalKernelStore {
    fn resume_transaction_legal(
        &mut self, 
        suspend_id: String, 
        operator_id: String
    ) -> Result<u64, Box<dyn std::error::Error>> {
        // 1. Find suspended transaction
        let suspended_tx = self.suspended_transactions.get(&suspend_id)
            .ok_or("Suspended transaction not found")?
            .clone();
        
        // 2. Check if expired
        if SystemTime::now() > suspended_tx.expires_at {
            return Err("Suspended transaction has expired".into());
        }
        
        // 3. Deserialize transaction data
        let mut tx: LegalTransaction = serde_json::from_str(&suspended_tx.transaction_data)?;
        
        // 4. Assign new handle and update state
        let new_handle = self.next_tx_id.fetch_add(1, Ordering::SeqCst);
        tx.id = new_handle;
        tx.state = LegalTxState::Building;
        tx.last_activity = SystemTime::now();
        
        // 5. Log resume operation
        self.wal.log_operation(
            new_handle,
            WalOperationType::TransactionResume {
                suspend_id: suspend_id.clone(),
                new_handle,
                operator_id
            },
            &format!("resumed_from:{}", suspended_tx.original_handle)
        )?;
        
        // 6. Restore to active transactions
        let timeout = SystemTime::now() + Duration::from_secs(self.system_config.transaction_timeout_seconds);
        self.transaction_timeouts.insert(new_handle, timeout);
        self.active_transactions.insert(new_handle, tx);
        
        // 7. Remove from suspended transactions
        self.suspended_transactions.remove(&suspend_id);
        self.remove_persisted_suspended_transaction(&suspend_id)?;
        
        Ok(new_handle)
    }
}
```

#### FFI Functions Required

```rust
#[no_mangle]
pub extern "C" fn pk_suspend_transaction(
    handle: PkTransactionHandle,
    operator_id_ptr: *const u8,
    operator_id_len: usize,
    reason_ptr: *const u8,
    reason_len: usize,
    out_suspend_id: *mut u8,
    suspend_id_buffer_size: usize,
    out_required_size: *mut usize
) -> PkResult;

#[no_mangle]
pub extern "C" fn pk_resume_transaction(
    suspend_id_ptr: *const u8,
    suspend_id_len: usize,
    operator_id_ptr: *const u8,
    operator_id_len: usize,
    out_handle: *mut PkTransactionHandle
) -> PkResult;

#[no_mangle]
pub extern "C" fn pk_list_suspended_transactions(
    buffer: *mut u8,
    buffer_size: usize,
    out_required_size: *mut usize
) -> PkResult;

#[no_mangle]
pub extern "C" fn pk_void_suspended_transaction(
    suspend_id_ptr: *const u8,
    suspend_id_len: usize,
    operator_id_ptr: *const u8,
    operator_id_len: usize,
    reason_ptr: *const u8,
    reason_len: usize
) -> PkResult;

#[no_mangle]
pub extern "C" fn pk_get_suspend_receipt_data(
    suspend_id_ptr: *const u8,
    suspend_id_len: usize,
    buffer: *mut u8,
    buffer_size: usize,
    out_required_size: *mut usize
) -> PkResult;
```

## Integration Points

### HTTP Service API Endpoints
```rust
// POST /api/sessions/{session_id}/transactions/{tx_id}/suspend
// DELETE /api/sessions/{session_id}/suspended/{suspend_id}
// PUT /api/sessions/{session_id}/suspended/{suspend_id}/resume
// GET /api/sessions/{session_id}/suspended
```

### .NET Client Integration
```csharp
public async Task<string> SuspendTransactionAsync(ulong transactionHandle, string operatorId, string reason = null);
public async Task<ulong> ResumeTransactionAsync(string suspendId, string operatorId);
public async Task<List<SuspendedTransactionInfo>> ListSuspendedTransactionsAsync();
public async Task VoidSuspendedTransactionAsync(string suspendId, string operatorId, string reason);
```

### AI Integration
Add new tools for transaction management:
- `suspend_current_transaction` - Suspend current transaction with reason
- `resume_suspended_transaction` - Resume by suspend ID or select from list
- `list_suspended_transactions` - Show pending suspended transactions

## Storage Architecture

### File Structure
```
./pos_kernel_data/terminals/{terminal_id}/
├── transaction.wal              # Write-Ahead Log
├── suspended/                   # Suspended transaction storage
│   ├── SUSP-20250115-001.json   # Individual suspended transactions
│   ├── SUSP-20250115-002.json
│   └── index.json               # Suspended transaction index
```

### Cleanup Process
- Background process to check for expired suspended transactions
- Auto-void expired transactions after timeout
- Configurable retention period (default 24 hours)
- Daily cleanup at terminal startup

## Testing Strategy

### Unit Tests
- Suspend validation rules (active payments, gift cards, states)
- Resume validation (expiry, not found, authorization)
- Serialization/deserialization of transaction data
- Barcode generation and parsing

### Integration Tests
- End-to-end suspend/resume across terminal restarts
- Multi-terminal suspend/resume scenarios
- Error handling and recovery
- Performance under load (many suspended transactions)

### AI Training Scenarios
- "hold this order" → suspend transaction
- "get that order back" → resume transaction  
- "cancel the suspended order" → void suspended transaction

## Implementation Timeline

### Phase 1: Core Functionality (Week 1)
- [ ] Add suspended transaction data structures
- [ ] Implement suspend/resume logic in kernel
- [ ] Add WAL logging support
- [ ] File-based persistence

### Phase 2: FFI and HTTP API (Week 2)  
- [ ] Implement FFI functions
- [ ] Add HTTP service endpoints
- [ ] Update .NET client library
- [ ] Basic testing

### Phase 3: AI Integration (Week 3)
- [ ] Add suspend/resume tools
- [ ] Update AI prompts for transaction management
- [ ] Cultural variations ("hold", "park", "suspend")
- [ ] Receipt generation

### Phase 4: Production Readiness (Week 4)
- [ ] Comprehensive error handling
- [ ] Performance optimization
- [ ] Cleanup processes
- [ ] Documentation and training scenarios

## Success Criteria

1. **Functional**: Transactions can be suspended and resumed across terminal sessions
2. **Performance**: Suspend/resume operations complete within 100ms
3. **Reliability**: ACID compliance maintained, no data loss scenarios
4. **Usability**: AI understands natural language suspend/resume commands
5. **Compliance**: Full audit trail with operator tracking and timestamps

## Risk Mitigation

### Data Loss Prevention
- Multiple persistence layers (WAL + file storage)
- Transaction data validation on suspend/resume
- Automatic recovery on corruption

### State Consistency
- Atomic operations for suspend/resume
- Lock-free concurrent access where possible
- Clear error states and rollback procedures

### Performance Impact
- Lazy loading of suspended transactions
- Efficient indexing for fast lookup
- Background cleanup to prevent storage bloat

This implementation provides enterprise-grade transaction suspend/resume functionality while maintaining the kernel's ACID compliance and culture-neutral architecture principles.
