#include <stdio.h>
#include <stdint.h>
#include <string.h>
#include <stdlib.h>

// POS Kernel C ABI definitions
typedef uint64_t PkTransactionHandle;
typedef struct { int32_t code; int32_t reserved; } PkResult;

#define PK_INVALID_HANDLE 0
#define PK_OK 0
#define PK_NOT_FOUND 1
#define PK_INVALID_STATE 2  
#define PK_VALIDATION_FAILED 3
#define PK_INSUFFICIENT_BUFFER 4
#define PK_INTERNAL_ERROR 255

// Function declarations (normally in header file)
PkResult pk_begin_transaction(const uint8_t* store, size_t store_len, 
                             const uint8_t* currency, size_t currency_len,
                             PkTransactionHandle* out_handle);
PkResult pk_close_transaction(PkTransactionHandle handle);
PkResult pk_add_line(PkTransactionHandle handle, const uint8_t* sku, size_t sku_len,
                    int32_t qty, int64_t unit_minor);
PkResult pk_add_cash_tender(PkTransactionHandle handle, int64_t amount_minor);
PkResult pk_get_totals(PkTransactionHandle handle, int64_t* total, int64_t* tendered, 
                      int64_t* change, int32_t* state);
PkResult pk_get_line_count(PkTransactionHandle handle, uint32_t* count);
const uint8_t* pk_get_version(void);
int pk_result_is_ok(PkResult result);

// Helper functions
void print_currency(int64_t amount_minor) {
    printf("$%.2f", amount_minor / 100.0);
}

int add_string_line(PkTransactionHandle handle, const char* sku, int qty, double price) {
    int64_t unit_minor = (int64_t)(price * 100); // Convert to cents
    PkResult result = pk_add_line(handle, (uint8_t*)sku, strlen(sku), qty, unit_minor);
    return pk_result_is_ok(result);
}

int add_cash_payment(PkTransactionHandle handle, double amount) {
    int64_t amount_minor = (int64_t)(amount * 100); // Convert to cents
    PkResult result = pk_add_cash_tender(handle, amount_minor);
    return pk_result_is_ok(result);
}

// Example 1: Basic transaction
void basic_transaction_example() {
    printf("=== Basic Transaction Example ===\n");
    
    PkTransactionHandle handle;
    PkResult result;
    
    // Begin transaction
    const char* store = "Store-001";
    const char* currency = "USD";
    result = pk_begin_transaction((uint8_t*)store, strlen(store),
                                 (uint8_t*)currency, strlen(currency), &handle);
    
    if (!pk_result_is_ok(result)) {
        printf("Failed to begin transaction: %d\n", result.code);
        return;
    }
    
    printf("Transaction created with handle: %llu\n", handle);
    
    // Add line items
    if (!add_string_line(handle, "COFFEE", 1, 3.99)) {
        printf("Failed to add coffee\n");
        goto cleanup;
    }
    
    if (!add_string_line(handle, "MUFFIN", 1, 2.49)) {
        printf("Failed to add muffin\n");
        goto cleanup;
    }
    
    // Check line count
    uint32_t line_count;
    result = pk_get_line_count(handle, &line_count);
    if (pk_result_is_ok(result)) {
        printf("Transaction has %u line items\n", line_count);
    }
    
    // Add payment
    if (!add_cash_payment(handle, 10.00)) {
        printf("Failed to add payment\n");
        goto cleanup;
    }
    
    // Get totals
    int64_t total, tendered, change;
    int32_t state;
    result = pk_get_totals(handle, &total, &tendered, &change, &state);
    
    if (pk_result_is_ok(result)) {
        printf("Total: ");
        print_currency(total);
        printf(", Tendered: ");
        print_currency(tendered);
        printf(", Change: ");
        print_currency(change);
        printf(", State: %s\n", state == 0 ? "Building" : "Completed");
    }
    
cleanup:
    // Always close the transaction
    pk_close_transaction(handle);
    printf("Transaction closed\n\n");
}

// Example 2: Error handling
void error_handling_example() {
    printf("=== Error Handling Example ===\n");
    
    PkTransactionHandle handle;
    PkResult result;
    
    // Try to use invalid handle
    result = pk_add_line(PK_INVALID_HANDLE, (uint8_t*)"SKU", 3, 1, 100);
    printf("Using invalid handle result: %d (%s)\n", 
           result.code, 
           result.code == PK_VALIDATION_FAILED ? "VALIDATION_FAILED" : "OTHER");
    
    // Create valid transaction for further tests
    const char* store = "Test-Store";
    const char* currency = "USD";
    result = pk_begin_transaction((uint8_t*)store, strlen(store),
                                 (uint8_t*)currency, strlen(currency), &handle);
    
    if (!pk_result_is_ok(result)) {
        printf("Failed to create test transaction\n");
        return;
    }
    
    // Try invalid quantity
    result = pk_add_line(handle, (uint8_t*)"SKU", 3, 0, 100); // qty = 0
    printf("Invalid quantity result: %d (%s)\n", 
           result.code,
           result.code == PK_VALIDATION_FAILED ? "VALIDATION_FAILED" : "OTHER");
    
    // Try negative price
    result = pk_add_line(handle, (uint8_t*)"SKU", 3, 1, -100); // negative price
    printf("Negative price result: %d (%s)\n", 
           result.code,
           result.code == PK_VALIDATION_FAILED ? "VALIDATION_FAILED" : "OTHER");
    
    pk_close_transaction(handle);
    
    // Try to use closed handle
    result = pk_get_line_count(handle, NULL);
    printf("Using closed handle result: %d (%s)\n", 
           result.code,
           result.code == PK_NOT_FOUND ? "NOT_FOUND" : "OTHER");
    
    printf("\n");
}

// Example 3: Multiple transactions
void multiple_transactions_example() {
    printf("=== Multiple Transactions Example ===\n");
    
    struct {
        const char* store;
        const char* sku;
        double price;
        double payment;
    } transactions[] = {
        {"Store-A", "WIDGET", 5.99, 10.00},
        {"Store-B", "GADGET", 12.49, 15.00}, 
        {"Store-C", "ITEM", 3.25, 5.00}
    };
    
    for (int i = 0; i < 3; i++) {
        PkTransactionHandle handle;
        PkResult result;
        
        // Begin transaction
        result = pk_begin_transaction((uint8_t*)transactions[i].store, 
                                    strlen(transactions[i].store),
                                    (uint8_t*)"USD", 3, &handle);
        
        if (!pk_result_is_ok(result)) {
            printf("Failed to create transaction %d\n", i + 1);
            continue;
        }
        
        // Add item and payment
        add_string_line(handle, transactions[i].sku, 1, transactions[i].price);
        add_cash_payment(handle, transactions[i].payment);
        
        // Get results
        int64_t total, tendered, change;
        int32_t state;
        pk_get_totals(handle, &total, &tendered, &change, &state);
        
        printf("%s: Total=", transactions[i].store);
        print_currency(total);
        printf(", Payment=");
        print_currency(tendered);
        printf(", Change=");
        print_currency(change);
        printf("\n");
        
        pk_close_transaction(handle);
    }
    
    printf("\n");
}

int main() {
    // Print version
    const char* version = (const char*)pk_get_version();
    printf("POS Kernel Version: %s\n\n", version);
    
    // Run examples
    basic_transaction_example();
    error_handling_example();
    multiple_transactions_example();
    
    printf("All examples completed successfully!\n");
    return 0;
}
