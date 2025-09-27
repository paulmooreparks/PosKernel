# Architectural Debt Registry

This document tracks known architectural debt that needs to be addressed in future iterations.

## HIGH PRIORITY

### 1. MCP Tool Result Parsing (Receipt Sync Issue)
**Location**: `PosKernel.AI/Core/ChatOrchestrator.cs` - `SyncReceiptWithKernelAsync()`  
**Issue**: Uses fragile regex parsing of MCP tool text format instead of proper structured parser  
**Current**: Regex patterns like `@"Line\s+(\d+):\s+([A-Z0-9]+)\s+x(\d+)..."`  
**Should Be**: Dedicated `McpToolResultParser` class with proper state machine parsing  
**Risk**: Parsing breaks when MCP format changes, leading to empty receipts  
**Effort**: Medium - Create parser class and unit tests  

**Recommended Solution**:
```csharp
public class McpToolResultParser 
{
    public TransactionParseResult ParseGetTransactionResult(string mcpText);
    public PaymentMethodsParseResult ParsePaymentMethodsResult(string mcpText);
    // etc.
}
```

### 2. Direct Kernel Client Access Pattern
**Location**: `PosKernel.AI/Core/ChatOrchestrator.cs` - Receipt synchronization  
**Issue**: Going through text serialization instead of direct structured data access  
**Current**: `Tool -> Text -> Regex -> ReceiptItem`  
**Should Be**: `KernelClient -> TransactionResult -> ReceiptItem`  
**Risk**: Data loss, parsing errors, performance overhead  
**Effort**: Medium - Refactor sync methods to use kernel client directly  

## MEDIUM PRIORITY

### 3. Hardcoded SKU-to-Name Mapping
**Location**: `PosKernel.AI/Core/ChatOrchestrator.cs` - `GetProductNameFromSku()`  
**Issue**: Hardcoded dictionary mapping instead of service-based lookup  
**Should Be**: `IProductCatalogService.GetProductName(sku)`  
**Risk**: Stale product names, maintenance burden  
**Effort**: Low - Inject product service and replace dictionary  

## LOW PRIORITY

### 4. Regex Currency Parsing
**Location**: Various currency parsing locations  
**Issue**: Regex patterns for extracting currency values  
**Current**: `@"[\d,]+\.?\d*"` patterns  
**Should Be**: `ICurrencyParsingService.ParseAmount(text, currency)`  
**Risk**: Fails with different currency formats  
**Effort**: Low - Create currency parsing service  

---

## Guidelines for Adding Debt

When adding architectural debt items:

1. **Be Specific**: Include exact file paths and method names
2. **Explain Risk**: What happens if this isn't fixed?
3. **Estimate Effort**: High/Medium/Low based on complexity
4. **Provide Solution**: Concrete recommendations for fixes
5. **Update Status**: Move items between priority levels as needed

## Debt Review Process

- Review this document monthly
- Prioritize HIGH items for next sprint
- Update status as items are resolved
- Archive completed items to separate section
