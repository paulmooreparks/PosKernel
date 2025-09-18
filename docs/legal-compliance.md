# POS Kernel ACID Compliance Architecture

## Design Goals

Transaction integrity is a core design goal. This requires:

- **ACID Compliance**: Atomicity, Consistency, Isolation, Durability
- **Write-Ahead Logging (WAL)**: Operations logged before being applied
- **Tamper Evidence**: Append-only logs with checksums and sequence numbers
- **Durability Guarantees**: fsync to physical storage before acknowledging success

## ACID-Compliant Implementation

### Write-Ahead Logging (WAL)

```rust
pub struct WalEntry {
    sequence_number: u64,           // Monotonic ordering
    timestamp: SystemTime,          // Wall clock time
    transaction_handle: u64,        // Which transaction
    operation_type: WalOperationType,
    data: String,                   // Serialized operation data
    checksum: u32,                  // Data integrity verification
}
```

### Operation Types Logged
- `TransactionBegin` - Transaction started
- `LineAdd` - Item added to transaction
- `TenderAdd` - Payment applied
- `TransactionCommit` - Transaction completed
- `TransactionAbort` - Transaction cancelled
- `TransactionTimeout` - Transaction expired
- `SystemConfigChange` - System configuration modified

### Durability Guarantees

#### Before Operation Acknowledgment
1. **WAL Entry Written** - Operation details logged
2. **fsync() Called** - Data forced to physical storage
3. **Checksum Verified** - Data integrity confirmed
4. **Sequence Number Assigned** - Ordering guaranteed

#### Only Then
5. **Memory State Updated** - In-memory transaction modified
6. **Success Returned** - Client receives confirmation

## Data Flow

### Transaction Begin
```
Client Request → WAL Write → fsync() → Memory Update → Success Response
     ↓              ↓           ↓           ↓              ↓
   pk_begin_    WalEntry    Physical    LegalTx       Handle
   transaction    #001      Storage     Created       Returned
```

### Line Add
```
Client Request → WAL Write → fsync() → Memory Update → Success Response
     ↓              ↓           ↓           ↓              ↓
   pk_add_line   WalEntry    Physical    Line Added     OK Status
                  #002       Storage     to Memory      Returned
```

### Failure Scenario
```
Client Request → WAL Write → DISK FAILS → Error Response
     ↓              ↓           ↓              ↓
   pk_add_line   WalEntry    Physical      InternalError
                  #003       FAILURE       NO MEMORY UPDATE
```

**Result**: Transaction remains in consistent state, no partial updates

## File Structure

```
./pos_kernel_data/
├── transaction.wal          # Write-Ahead Log (CRITICAL)
│   ├── Sequence numbers     # Monotonic ordering
│   ├── Checksums           # Data integrity
│   ├── Timestamps          # Audit trail
│   └── Operation details   # Complete transaction log
├── audit.log               # Non-transactional events
└── config.txt              # System configuration
```

### WAL Format
```
sequence,timestamp_nanos,tx_handle,operation_type,data,checksum
1,1703721600000000000,1,TransactionBegin,store:Store-001;currency:USD,12345
2,1703721600100000000,1,LineAdd,sku:COFFEE;qty:1;unit_minor:399,23456
3,1703721600200000000,1,TenderAdd,amount_minor:500,34567
4,1703721600300000000,1,TransactionCommit,committed_at:2023-12-27T...,45678
```

## Integrity Features

### 1. Tamper Evidence
- **Append-only logs** - No modification of historical records
- **Sequence numbers** - Detect missing or reordered entries
- **Checksums** - Verify data integrity
- **Timestamps** - Establish chronological order

### 2. Durability Guarantees
- **fsync() enforcement** - Data physically written to storage
- **Write-before-acknowledge** - Success only after durable storage
- **Atomic operations** - Either complete success or complete failure

### 3. Audit Trail
- **Complete operation history** - Every action logged
- **Immutable records** - Cannot be altered after written
- **Structured format** - Consistent, parseable format
- **Recovery capability** - Reconstruct transactions from logs

### 4. Consistency Maintenance
- **ACID transactions** - All operations atomic
- **State validation** - Consistent state at all times
- **Error handling** - Graceful failure without corruption

## API Usage

### ACID-Compliant Functions
```c
// ACID-compliant transaction operations
PkResult pk_begin_transaction_legal(...);
PkResult pk_add_line_legal(...);
PkResult pk_add_cash_tender_legal(...);
PkResult pk_commit_transaction_legal(...);
PkResult pk_abort_transaction_legal(...);

// Integrity verification
PkResult pk_get_transaction_wal_info(...);
PkResult pk_verify_wal_integrity();
PkResult pk_force_wal_sync();
```

### Backward Compatibility
Original functions delegate to ACID versions:
```c
// These now use ACID-compliant implementation
PkResult pk_begin_transaction(...);  // → pk_begin_transaction_legal(...)
PkResult pk_add_line(...);           // → pk_add_line_legal(...)
PkResult pk_add_cash_tender(...);    // → pk_add_cash_tender_legal(...)
```

## Design Benefits

### 1. Data Integrity
- **Complete audit trail** - Every action documented
- **Tamper evidence** - Detect unauthorized modifications
- **Chronological ordering** - Establish sequence of events
- **Data integrity** - Checksums verify accuracy

### 2. System Reliability
- **Dispute resolution** - Clear transaction history
- **Anomaly detection** - Pattern recognition in logs
- **System audits** - Demonstrate operational integrity
- **Recovery capability** - Reconstruct system state from logs

### 3. Business Value
- **Regulatory readiness** - Architecture supports compliance requirements
- **Audit support** - Comprehensive transaction documentation
- **Fraud detection** - Complete transaction visibility
- **Data protection** - Immutable transaction records

## Performance Considerations

### Write Performance
- **Sequential writes** - WAL uses append-only pattern
- **Batch fsync** - Group multiple operations (future optimization)
- **Memory buffering** - BufWriter reduces syscall overhead

### Read Performance
- **Hot data in memory** - Active transactions cached
- **Read-write locks** - Multiple concurrent readers
- **Indexed access** - HashMap for O(1) transaction lookup

### Storage Requirements
- **Compressed logs** - Future: LZ4 compression
- **Log rotation** - Archive old WAL files
- **Retention policies** - Configurable retention periods

This architecture ensures the POS Kernel maintains transaction integrity and provides comprehensive audit capabilities while maintaining excellent performance characteristics.
