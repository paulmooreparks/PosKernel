# C ABI Specification

This document defines the C Application Binary Interface (ABI) for the POS Kernel. This is the stable interface that all language wrappers build upon.

## Design Philosophy

The C ABI follows Win32 conventions for consistency and familiarity:

- **Handle-based resource management** - Opaque handles rather than pointers
- **Structured error reporting** - `HRESULT`-style return codes
- **Explicit resource cleanup** - Manual lifecycle management
- **Property-style access** - Separate functions for each property
- **Two-phase string retrieval** - Size query followed by data copy

## Core Types

### Handles
```c
typedef uint64_t PkTransactionHandle;
#define PK_INVALID_HANDLE 0
```

Handles are opaque 64-bit identifiers that represent active transactions. Handle value 0 is reserved as the invalid handle.

### Result Structure
```c
typedef struct PkResult {
    int32_t code;      // ResultCode enum value
    int32_t reserved;  // For future use, must be 0
} PkResult;
```

### Result Codes
```c
enum ResultCode {
    PK_OK = 0,                    // Operation succeeded
    PK_NOT_FOUND = 1,            // Handle not found
    PK_INVALID_STATE = 2,        // Operation not valid in current state  
    PK_VALIDATION_FAILED = 3,    // Input validation failed
    PK_INSUFFICIENT_BUFFER = 4,  // Output buffer too small
    PK_INTERNAL_ERROR = 255      // Internal system error
};
```

## Function Reference

### Transaction Lifecycle

#### pk_begin_transaction
```c
PkResult pk_begin_transaction(
    const uint8_t* store_ptr,      // UTF-8 store identifier
    size_t store_len,              // Length of store string  
    const uint8_t* currency_ptr,   // UTF-8 currency code (e.g., "USD")
    size_t currency_len,           // Length of currency string
    PkTransactionHandle* out_handle // [out] Transaction handle
);
```

**Purpose:** Create a new transaction and return its handle.

**Parameters:**
- `store_ptr`, `store_len`: UTF-8 encoded store identifier
- `currency_ptr`, `currency_len`: UTF-8 encoded currency code
- `out_handle`: Pointer to receive the new transaction handle

**Returns:**
- `PK_OK`: Success, `*out_handle` contains valid handle
- `PK_VALIDATION_FAILED`: Invalid parameters (null pointers, empty currency)
- `PK_INTERNAL_ERROR`: System error (memory allocation failure)

#### pk_close_transaction
```c
PkResult pk_close_transaction(PkTransactionHandle handle);
```

**Purpose:** Close a transaction and release its resources.

**Parameters:**
- `handle`: Transaction handle to close

**Returns:**
- `PK_OK`: Transaction closed successfully
- `PK_VALIDATION_FAILED`: Invalid handle (PK_INVALID_HANDLE)
- `PK_NOT_FOUND`: Handle not found (already closed or never existed)

### Transaction Operations

#### pk_add_line
```c
PkResult pk_add_line(
    PkTransactionHandle handle,    // Transaction handle
    const uint8_t* sku_ptr,        // UTF-8 product SKU
    size_t sku_len,                // Length of SKU string
    int32_t qty,                   // Quantity (must be > 0)
    int64_t unit_minor             // Unit price in minor units (e.g., cents)
);
```

**Purpose:** Add a line item to the transaction.

**Parameters:**
- `handle`: Transaction handle
- `sku_ptr`, `sku_len`: UTF-8 encoded product SKU
- `qty`: Item quantity (must be positive)
- `unit_minor`: Unit price in currency minor units (e.g., cents for USD)

**Returns:**
- `PK_OK`: Line item added successfully
- `PK_VALIDATION_FAILED`: Invalid parameters (bad handle, qty ≤ 0, unit_minor < 0, empty SKU)
- `PK_NOT_FOUND`: Transaction handle not found
- `PK_INVALID_STATE`: Transaction not in building state

#### pk_add_cash_tender
```c
PkResult pk_add_cash_tender(
    PkTransactionHandle handle,    // Transaction handle  
    int64_t amount_minor           // Cash amount in minor units
);
```

**Purpose:** Add cash payment to the transaction.

**Parameters:**
- `handle`: Transaction handle
- `amount_minor`: Cash amount in currency minor units

**Returns:**
- `PK_OK`: Cash tender added successfully
- `PK_VALIDATION_FAILED`: Invalid parameters (bad handle, amount ≤ 0)
- `PK_NOT_FOUND`: Transaction handle not found
- `PK_INVALID_STATE`: Transaction not in building state

### Query Operations

#### pk_get_totals
```c
PkResult pk_get_totals(
    PkTransactionHandle handle,    // Transaction handle
    int64_t* out_total,           // [out] Total amount in minor units
    int64_t* out_tendered,        // [out] Tendered amount in minor units  
    int64_t* out_change,          // [out] Change amount in minor units
    int32_t* out_state            // [out] Transaction state (0=building, 1=completed)
);
```

**Purpose:** Retrieve current transaction totals and state.

**Parameters:**
- `handle`: Transaction handle
- `out_total`: Pointer to receive total amount
- `out_tendered`: Pointer to receive tendered amount
- `out_change`: Pointer to receive change amount
- `out_state`: Pointer to receive transaction state

**Returns:**
- `PK_OK`: Totals retrieved successfully
- `PK_VALIDATION_FAILED`: Invalid parameters (bad handle, null output pointers)
- `PK_NOT_FOUND`: Transaction handle not found

#### pk_get_line_count
```c
PkResult pk_get_line_count(
    PkTransactionHandle handle,    // Transaction handle
    uint32_t* out_count           // [out] Number of line items
);
```

**Purpose:** Get the number of line items in the transaction.

#### pk_get_store_name
```c
PkResult pk_get_store_name(
    PkTransactionHandle handle,           // Transaction handle
    uint8_t* buffer,                     // [out] Buffer for store name (UTF-8)
    size_t buffer_size,                  // Size of buffer
    size_t* out_required_size            // [out] Required buffer size
);
```

**Purpose:** Retrieve the store name for a transaction.

**Win32 Pattern:**
1. Call with `buffer=NULL, buffer_size=0` to get required size
2. Allocate buffer of required size
3. Call again with proper buffer to get data

#### pk_get_currency  
```c
PkResult pk_get_currency(
    PkTransactionHandle handle,           // Transaction handle
    uint8_t* buffer,                     // [out] Buffer for currency code (UTF-8)
    size_t buffer_size,                  // Size of buffer
    size_t* out_required_size            // [out] Required buffer size
);
```

**Purpose:** Retrieve the currency code for a transaction.

### Utility Functions

#### pk_get_version
```c
const uint8_t* pk_get_version(void);
```

**Purpose:** Get library version string (null-terminated UTF-8).

**Returns:** Pointer to static version string.

#### pk_result_is_ok
```c
bool pk_result_is_ok(PkResult result);
```

**Purpose:** Check if a result indicates success.

#### pk_result_get_code
```c
int32_t pk_result_get_code(PkResult result);
```

**Purpose:** Extract error code from result.

## Usage Patterns

### Basic Transaction Flow
```c
// 1. Begin transaction
PkTransactionHandle handle;
PkResult result = pk_begin_transaction("Store-001", 9, "USD", 3, &handle);
if (!pk_result_is_ok(result)) {
    // Handle error
    return;
}

// 2. Add line items  
result = pk_add_line(handle, "SKU-001", 7, 1, 199); // $1.99
if (!pk_result_is_ok(result)) {
    pk_close_transaction(handle);
    return;
}

// 3. Add payment
result = pk_add_cash_tender(handle, 500); // $5.00
if (!pk_result_is_ok(result)) {
    pk_close_transaction(handle);
    return;
}

// 4. Get totals
int64_t total, tendered, change;
int32_t state;
result = pk_get_totals(handle, &total, &tendered, &change, &state);

// 5. Close transaction
pk_close_transaction(handle);
```

### Win32-Style String Retrieval
```c
// Get required buffer size
size_t required_size;
PkResult result = pk_get_store_name(handle, NULL, 0, &required_size);
if (!pk_result_is_ok(result) && pk_result_get_code(result) != PK_INSUFFICIENT_BUFFER) {
    // Handle error
    return;
}

// Allocate buffer and retrieve data
uint8_t* buffer = malloc(required_size);
result = pk_get_store_name(handle, buffer, required_size, NULL);
if (pk_result_is_ok(result)) {
    // Use store name
    printf("Store: %s\n", buffer);
}
free(buffer);
```

## Error Handling Guidelines

### Defensive Programming
- Always check result codes before using output parameters
- Validate handles before passing to functions
- Initialize output pointers to safe values
- Clean up resources in error paths

### Resource Management
- Every `pk_begin_transaction` must have a matching `pk_close_transaction`
- Failed `pk_begin_transaction` calls do not require cleanup
- Handle cleanup is idempotent (safe to call multiple times)

## Thread Safety

- **Concurrent Access**: Multiple threads may safely call functions with different handles
- **Shared Handles**: Multiple threads using the same handle require external synchronization
- **Global State**: Library manages internal synchronization for global state

## Memory Management

- **Input Strings**: Caller owns all input string memory
- **Output Strings**: Library writes to caller-provided buffers
- **Handle Storage**: Library manages transaction data internally
- **No Allocations**: Library never allocates memory on behalf of caller

## Calling Conventions

- **Platform**: Standard C calling convention (`cdecl` on x86/x64)
- **Alignment**: All structures use standard C alignment rules
- **Endianness**: Native byte order (little-endian on x86/x64)
- **Character Encoding**: All strings are UTF-8
