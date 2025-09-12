# Examples

This directory contains practical examples demonstrating POS Kernel usage across different languages and scenarios.

## Structure

- **[basic/](basic/)** - Simple transaction examples
- **[advanced/](advanced/)** - Complex scenarios and patterns
- **[integration/](integration/)** - Real-world integration examples
- **[performance/](performance/)** - Performance testing and benchmarks

## Quick Examples

### C# - Basic Transaction
```csharp
using PosKernel.Host;

// Object-oriented approach with automatic cleanup
using var tx = Pos.CreateTransaction("Store-001", "USD");
tx.AddItem("COFFEE", 3.99m);
tx.AddItem("MUFFIN", 2.49m);
tx.AddCashTender(10.00m);

Console.WriteLine($"Total: {tx.Totals.Total:C}, Change: {tx.Totals.Change:C}");
```

### C - Direct API Usage
```c
#include <stdio.h>
#include "pos_kernel.h"

int main() {
    PkTransactionHandle handle;
    PkResult result;
    
    // Begin transaction
    result = pk_begin_transaction("Store-001", 9, "USD", 3, &handle);
    if (!pk_result_is_ok(result)) return -1;
    
    // Add items
    pk_add_line(handle, "COFFEE", 6, 1, 399);  // $3.99
    pk_add_line(handle, "MUFFIN", 6, 1, 249);  // $2.49
    
    // Add payment
    pk_add_cash_tender(handle, 1000); // $10.00
    
    // Get totals
    int64_t total, tendered, change;
    int32_t state;
    pk_get_totals(handle, &total, &tendered, &change, &state);
    
    printf("Total: $%.2f, Change: $%.2f\n", 
           total / 100.0, change / 100.0);
    
    // Cleanup
    pk_close_transaction(handle);
    return 0;
}
```

### Python - FFI Wrapper (conceptual)
```python
import ctypes
from contextlib import contextmanager

class PosKernel:
    def __init__(self, lib_path="pos_kernel.dll"):
        self.lib = ctypes.CDLL(lib_path)
        self._setup_functions()
    
    @contextmanager
    def transaction(self, store, currency):
        handle = ctypes.c_uint64()
        result = self.lib.pk_begin_transaction(
            store.encode('utf-8'), len(store),
            currency.encode('utf-8'), len(currency),
            ctypes.byref(handle)
        )
        
        if result != 0:
            raise Exception(f"Failed to begin transaction: {result}")
        
        try:
            yield Transaction(self.lib, handle.value)
        finally:
            self.lib.pk_close_transaction(handle.value)

# Usage
pos = PosKernel()
with pos.transaction("Store-001", "USD") as tx:
    tx.add_item("COFFEE", 1, 399)  # $3.99
    tx.add_cash_tender(500)        # $5.00
    totals = tx.get_totals()
    print(f"Change: ${totals.change / 100:.2f}")
```

## Example Categories

### Basic Examples
- Single transaction processing
- Error handling patterns
- Resource management
- Currency conversion

### Advanced Examples  
- Multi-threaded usage
- Batch processing
- Custom wrapper development
- Integration with existing systems

### Performance Examples
- High-throughput scenarios
- Memory usage optimization
- Concurrent transaction processing
- Benchmarking utilities

## Language-Specific Notes

### C#/.NET
- Use `using` statements for automatic resource management
- Leverage async/await for UI responsiveness
- Implement logging and error handling consistently
- Consider dependency injection for testability

### C/C++
- Always check result codes before using output parameters
- Implement RAII wrappers in C++ for automatic cleanup
- Use appropriate string encoding (UTF-8)
- Handle memory allocation failures gracefully

### Python
- Use context managers (`with` statement) for resource management
- ctypes for FFI, cffi for more complex scenarios
- Handle encoding/decoding between Python strings and UTF-8
- Consider asyncio integration for concurrent processing

### JavaScript/Node.js
- Use node-ffi-napi for native library binding
- Promise-based APIs for asynchronous operations
- Buffer management for string parameters
- TypeScript definitions for better development experience

### Other Languages
- Java: JNI or JNA for native integration
- Go: CGo for C library binding
- Rust: Direct FFI with minimal overhead
- Ruby: FFI gem for native library access
