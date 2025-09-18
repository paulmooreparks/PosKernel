# Enhanced Multi-Process POS Kernel Architecture

## Architecture Overview

This document describes the enhanced multi-process architecture implemented in POS Kernel v0.4.0, which provides:

- **Process-per-terminal isolation** with fault tolerance
- **Cross-process coordination** through file-based messaging
- **ACID-compliant transaction logging** per terminal
- **Graceful resource management** and cleanup

## Process Architecture

### Terminal Process Structure
```
┌─────────────────────────────────────────┐
│           Terminal Process              │
│                                         │
│  ┌─────────────────────────────────┐   │
│  │        .NET Host App            │   │
│  │                                 │   │
│  │  - UI/UX Layer                  │   │
│  │  - Business Logic               │   │
│  │  - P/Invoke to Rust            │   │
│  └─────────────────────────────────┘   │
│                  ↓                      │
│  ┌─────────────────────────────────┐   │
│  │        Rust POS Kernel          │   │
│  │                                 │   │
│  │  - ACID Transaction Processing  │   │
│  │  - WAL (Write-Ahead Logging)   │   │
│  │  - Terminal Coordination       │   │
│  │  - Process Locking             │   │
│  └─────────────────────────────────┘   │
│                  ↓                      │
│  ┌─────────────────────────────────┐   │
│  │      File System Layer          │   │
│  │                                 │   │
│  │  - Terminal-specific WAL        │   │
│  │  - Process lock file           │   │
│  │  - Local configuration         │   │
│  └─────────────────────────────────┘   │
└─────────────────────────────────────────┘
```

### File System Layout
```
pos_kernel_data/
├── terminals/
│   ├── REGISTER_01/
│   │   ├── transaction.wal          # ACID-compliant transaction log
│   │   ├── terminal.lock           # Process exclusion lock
│   │   ├── local_config.json       # Terminal-specific settings
│   │   └── session_data.json       # Current session information
│   ├── REGISTER_02/
│   │   ├── transaction.wal
│   │   ├── terminal.lock
│   │   ├── local_config.json
│   │   └── session_data.json
│   └── MANAGER/
│       ├── transaction.wal         # Manager operations
│       ├── terminal.lock
│       └── local_config.json
├── shared/
│   ├── coordination/
│   │   ├── active_terminals.json   # Registry of active terminals
│   │   ├── system_config.json      # Global system configuration
│   │   └── shutdown_signal.flag   # Graceful shutdown coordination
│   ├── cross_terminal_messages/
│   │   ├── broadcast/              # System-wide announcements
│   │   │   ├── config_change_*.json
│   │   │   └── shutdown_*.json
│   │   └── terminal_inbox/         # Terminal-specific messages
│   │       ├── REGISTER_01_*.json
│   │       └── REGISTER_02_*.json
│   └── aggregated_audit.log        # Combined audit trail
└── temp/
    ├── coordination_locks/          # Fine-grained operation locks
    └── cleanup/                     # Temporary files for cleanup
```

## Multi-Threading Model

### Internal Kernel Threading
```rust
// The kernel itself is SINGLE-THREADED per process
// All synchronization is external via RwLock

pub struct LegalKernelStore {
    // Thread-safe atomic counters
    next_tx_id: AtomicU64,
    next_session_id: AtomicU64,
    
    // RwLock provides thread-safe access
    active_transactions: HashMap<u64, LegalTransaction>,
    active_sessions: HashMap<u64, TerminalSession>,
    
    // WAL has internal synchronization
    wal: Arc<WriteAheadLog>,  // Arc<Mutex<...>> internally
}

// Global instance per process
static LEGAL_KERNEL_STORE: OnceLock<RwLock<LegalKernelStore>>;
```

### External Client Threading
```csharp
// .NET clients can use multiple threads safely
public class PosTerminalClient {
    // Thread-safe operations due to Rust RwLock
    public async Task<TransactionHandle> BeginTransactionAsync(string store, string currency) {
        // Multiple threads can call this concurrently
        return await Task.Run(() => {
            var handle = RustNative.pk_begin_transaction_legal(
                Encoding.UTF8.GetBytes(store), store.Length,
                Encoding.UTF8.GetBytes(currency), currency.Length,
                out var transactionHandle);
            
            if (handle.code != 0) {
                throw new PosKernelException($"Transaction begin failed: {handle.code}");
            }
            
            return transactionHandle;
        });
    }
}
```

### Cross-Process Coordination
```csharp
// Example: Manager coordinating multiple terminals
public class PosManagerConsole {
    public async Task InitiateSystemShutdown() {
        // 1. Send shutdown message to all terminals
        var terminals = GetActiveTerminals();
        
        foreach (var terminal in terminals) {
            await SendTerminalMessage(terminal, new ShutdownMessage {
                GracePeriodSeconds = 300,
                Reason = "Scheduled maintenance"
            });
        }
        
        // 2. Wait for terminals to acknowledge
        await WaitForTerminalShutdown(terminals, TimeSpan.FromMinutes(5));
        
        // 3. Perform final cleanup
        await PerformSystemCleanup();
    }
}
```

## Process Lifecycle

### Terminal Startup Sequence
```
1. Process Start
   ↓
2. Initialize Terminal Coordinator
   - Validate terminal ID
   - Create terminal directory
   ↓
3. Acquire Process Lock
   - Create terminal.lock file
   - Check for stale locks
   - Fail if another process active
   ↓
4. Register Terminal
   - Add to active_terminals.json
   - Include PID, start time
   ↓
5. Initialize Kernel Store
   - Create terminal-specific WAL
   - Load local configuration
   ↓
6. Ready for Operations
```

### Terminal Shutdown Sequence
```
1. Shutdown Signal Received
   ↓
2. Complete Active Transactions
   - Finish in-progress operations
   - Commit or abort pending transactions
   ↓
3. Force WAL Sync
   - Ensure all data written to disk
   ↓
4. Unregister Terminal
   - Remove from active_terminals.json
   ↓
5. Release Process Lock
   - Delete terminal.lock file
   ↓
6. Process Exit
```

## Error Handling and Fault Tolerance

### Process Isolation Benefits
```rust
// Terminal 1 crash doesn't affect Terminal 2
// Each has independent:
// - Memory space
// - File handles  
// - WAL files
// - Process locks
```

### Stale Lock Detection
```rust
fn is_lock_stale(&self, lock_path: &str) -> Result<bool, Error> {
    // Read lock file to get PID
    let lock_content = std::fs::read_to_string(lock_path)?;
    let pid = extract_pid(&lock_content)?;
    
    // Platform-specific process existence check
    #[cfg(windows)]
    {
        // Try to open process handle
        let handle = unsafe { OpenProcess(PROCESS_QUERY_INFORMATION, 0, pid) };
        Ok(handle.is_null()) // If null, process doesn't exist
    }
    
    #[cfg(unix)]
    {
        // Send signal 0 to check process existence
        use std::process::Command;
        let result = Command::new("kill").args(["-0", &pid.to_string()]).output()?;
        Ok(!result.status.success())
    }
}
```

### Recovery Procedures
```rust
// Automatic recovery from stale locks
if lock_exists && is_lock_stale(lock_path)? {
    // Remove stale lock
    std::fs::remove_file(lock_path)?;
    
    // Check WAL integrity
    verify_wal_consistency(terminal_id)?;
    
    // Recover partial transactions
    recover_incomplete_transactions(terminal_id)?;
    
    // Proceed with normal startup
    acquire_exclusive_lock()?;
}
```

## Performance Characteristics

### Resource Usage per Terminal
- **Memory**: 20-50MB (including .NET runtime)
- **File Handles**: 5-10 handles per terminal
- **Disk I/O**: ~1KB per transaction (WAL)
- **CPU**: Minimal when idle, scales with transaction volume

### Scalability Limits
- **Max Terminals**: 50-100 per machine (practical limit)
- **Transaction Throughput**: 100-1000 TPS per terminal
- **File System**: Scales well with terminal count
- **Cross-Process Overhead**: Minimal (file-based coordination)

### Optimization Strategies
```rust
// WAL batching for high-volume scenarios
struct WalBatcher {
    pending_entries: Vec<WalEntry>,
    batch_size: usize,
    flush_interval: Duration,
}

impl WalBatcher {
    fn add_entry(&mut self, entry: WalEntry) -> Result<(), Error> {
        self.pending_entries.push(entry);
        
        if self.pending_entries.len() >= self.batch_size {
            self.flush_batch()?;
        }
        
        Ok(())
    }
    
    fn flush_batch(&mut self) -> Result<(), Error> {
        // Write all entries in single fsync operation
        // Significantly improves high-volume performance
        Ok(())
    }
}
```

## Usage Examples

### Basic Terminal Operations
```csharp
// Initialize terminal
var result = RustNative.pk_initialize_terminal(
    Encoding.UTF8.GetBytes("REGISTER_01"), 
    "REGISTER_01".Length);

if (!RustNative.pk_result_is_ok(result)) {
    throw new Exception($"Terminal initialization failed: {result.code}");
}

// Normal transaction processing
var txHandle = BeginTransaction("STORE_001", "USD");
AddLine(txHandle, "COFFEE_001", 1, 399); // $3.99
AddCashTender(txHandle, 500); // $5.00
// Transaction automatically committed, WAL updated

CloseTransaction(txHandle);
```

### Cross-Terminal Coordination
```csharp
// Manager terminal coordinating system
public class SystemManager {
    public async Task BroadcastConfigurationChange(SystemConfig newConfig) {
        // Write configuration to shared location
        await WriteSharedConfig(newConfig);
        
        // Notify all terminals
        var message = new ConfigChangeMessage {
            ConfigVersion = newConfig.Version,
            EffectiveTime = DateTime.UtcNow.AddSeconds(30)
        };
        
        await BroadcastMessage(message);
        
        // Wait for acknowledgments
        await WaitForConfigurationApplied();
    }
}
```

## Future Enhancements

### Phase 2: Enhanced Coordination
- **Real-time messaging** via named pipes or shared memory
- **Distributed transaction coordination** across terminals
- **Load balancing** for high-volume scenarios

### Phase 3: High Availability
- **Automatic failover** between terminal processes
- **Transaction replication** across terminals
- **Hot standby** terminal processes

### Phase 4: Scale-Out Architecture
- **Network-aware coordination** for multi-machine deployments
- **Centralized transaction coordination** service
- **Database backend integration** for enterprise scenarios

This architecture provides a solid foundation for enterprise-grade POS systems while maintaining the simplicity and reliability principles established in the current coding standards.
