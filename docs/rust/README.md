# Rust Implementation Documentation

This document describes the internal Rust implementation of the POS Kernel, including architecture decisions, data structures, and implementation details.

## Architecture Overview

The POS Kernel is implemented as a Rust library (`cdylib`) that exposes a C-compatible FFI interface. The design emphasizes safety, performance, and maintainability while providing a clean separation between internal Rust idioms and the external C API.

### Project Structure
```
PosKernel/
├── pos-kernel-rs/          # Core Rust implementation (cdylib)
│   ├── src/lib.rs          # FFI exports and business logic
│   └── Cargo.toml          # Rust dependencies and build config
├── PosKernel.Host/         # .NET wrapper and demo application
│   ├── RustNative.cs       # P/Invoke declarations
│   ├── Pos.cs              # High-level .NET API
│   └── Program.cs          # Demo application
└── docs/                   # Comprehensive documentation
```

**Design Philosophy:**
- **Rust-First**: Core business logic implemented in Rust for performance and safety
- **Thin Wrapper**: .NET layer provides only convenience and object-oriented patterns
- **Direct FFI**: No intermediate .NET assemblies between wrapper and Rust core
- **Documentation-Driven**: Parallel documentation maintenance with implementation

## Core Data Structures

### Transaction
```rust
struct Transaction {
    id: u64,
    store: String,
    currency: String,
    lines: Vec<Line>,
    tendered_minor: i64,
    state: TxState,
}
```

**Design Rationale:**
- `id`: Unique identifier for handle-based lookups
- `store` & `currency`: String storage for metadata (UTF-8 safe)
- `lines`: Vector for efficient line item management
- `tendered_minor`: Integer arithmetic for precise currency handling
- `state`: Enum for transaction lifecycle management

### Line Item
```rust
struct Line { 
    sku: String, 
    qty: i32, 
    unit_minor: i64 
}
```

**Design Rationale:**
- SKU as String for flexible product identification
- Signed quantities (negative for returns/voids)
- Minor units (cents) for precise decimal arithmetic

### Transaction State
```rust
enum TxState { 
    Building,    // Adding items, accepting tenders
    Completed    // Sufficient payment received
}
```

## Memory Management

### Global Store
```rust
static STORE: OnceLock<Mutex<Store>> = OnceLock::new();
```

**Thread Safety:**
- `OnceLock` ensures single initialization
- `Mutex` provides exclusive access for modifications
- Read-heavy operations can be optimized with `RwLock` if needed

### Handle Management
- Handles are sequential `u64` values starting from 1
- Handle 0 (`PK_INVALID_HANDLE`) represents invalid/closed transactions
- Explicit cleanup via `pk_close_transaction` prevents resource leaks

## Error Handling

### Result Codes
```rust
#[repr(i32)]
pub enum ResultCode { 
    Ok = 0,                    // Success
    NotFound = 1,             // Handle not found
    InvalidState = 2,         // Operation not valid in current state
    ValidationFailed = 3,     // Input validation failed
    InsufficientBuffer = 4,   // String buffer too small
    InternalError = 255       // Mutex poison or similar
}
```

**Design Philosophy:**
- Structured error codes rather than magic numbers
- Consistent with Win32 `HRESULT` patterns
- Discriminated errors enable appropriate client handling

## String Handling

### Input Strings (C → Rust)
```rust
unsafe fn read_str(ptr: *const u8, len: usize) -> String {
    if ptr.is_null() || len == 0 { return String::new(); }
    let slice = unsafe { std::slice::from_raw_parts(ptr, len) };
    String::from_utf8_lossy(slice).into_owned()
}
```

**Safety Guarantees:**
- Null pointer checks prevent crashes
- Length validation prevents buffer overruns
- `from_utf8_lossy` handles invalid UTF-8 gracefully
- Owned string prevents dangling pointer issues

### Output Strings (Rust → C)
```rust
unsafe fn write_str(s: &str, buffer: *mut u8, buffer_size: usize, out_required: *mut usize) -> PkResult
```

**Win32 Pattern:**
- Two-phase retrieval: size query, then data copy
- Buffer size validation prevents overruns
- Required size output enables proper allocation
- Consistent with `GetWindowText`, `GetModuleFileName`, etc.

## Performance Characteristics

### Computational Complexity
- **Transaction Creation**: O(1) - HashMap insertion
- **Line Addition**: O(1) - Vector append
- **Total Calculation**: O(n) - Sum over line items
- **Property Access**: O(1) - Direct field access

### Memory Usage
- **Per Transaction**: ~200 bytes + string content
- **Per Line Item**: ~50 bytes + SKU string
- **Global Overhead**: Minimal (single HashMap + Mutex)

### Lock Contention
- **Read Operations**: Shared lock (if using RwLock)
- **Write Operations**: Exclusive lock required
- **Optimization Opportunity**: Fine-grained locking per transaction

## Safety Analysis

### Unsafe Code Audit
All `unsafe` code is limited to:
1. **FFI Boundary**: Pointer dereferencing with explicit validation
2. **String Conversion**: UTF-8 handling with fallback to lossy conversion
3. **Output Parameter Writing**: Bounds-checked buffer operations

### Memory Safety
- No raw pointer arithmetic
- All allocations managed by Rust's ownership system
- Explicit resource cleanup prevents leaks
- Bounds checking on all array/buffer operations

## Testing Strategy

### Unit Tests
- Transaction lifecycle (create, modify, close)
- Error conditions (invalid handles, validation failures)
- String handling (null pointers, empty strings, UTF-8 edge cases)
- Concurrent access patterns

### Integration Tests
- C ABI compatibility
- Memory leak detection
- Performance benchmarks
- Stress testing under load

## Build Integration

### MSBuild Integration
The Rust library is automatically built and copied as part of the .NET build process:

```xml
<Target Name="BuildRustLibrary" BeforeTargets="Build">
  <Exec Command="cargo build --manifest-path &quot;$(RustProjectPath)\Cargo.toml&quot;" />
</Target>

<Target Name="CopyRustLibrary" AfterTargets="Build">
  <Copy SourceFiles="$(RustTargetDir)\$(RustLibName).dll" 
        DestinationFolder="$(OutputPath)" />
</Target>
```

**Benefits:**
- Single `dotnet build` command builds both Rust and .NET components
- Automatic debug symbol copying for mixed-mode debugging
- Cross-platform build support (Windows/Linux/macOS)

## Future Enhancements

### Performance Optimizations
- Replace `Mutex<HashMap>` with `DashMap` for better concurrency
- Implement copy-on-write for string fields
- Add memory pooling for frequent allocations

### Feature Extensions
- Line item modification/deletion
- Multiple tender types (credit, check, etc.)
- Transaction serialization/persistence
- Audit trail and logging

### API Extensions
- Batch operations for bulk line additions
- Query interfaces for transaction enumeration
- Event callbacks for state changes
- Configuration management

### Architecture Improvements
- **Optional .NET Abstractions Layer**: Consider adding `PosKernel.Abstractions` back if you need to expose interfaces for dependency injection or testing
- **NuGet Package**: Package the .NET wrapper as a NuGet package for distribution
- **Language Bindings**: Create additional language wrappers (Python, JavaScript, etc.) following the same patterns
