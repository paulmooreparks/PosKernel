# Requirements Catalog (Iteration 1)
Status: Draft
Source Domains: NRF (legacy ARTS / Unified Commerce), Internal Enhancements
Tracking Format: Markdown (authoritative). XferLang catalog deprecated.

## 1. Phases
| Phase | Purpose | Goals |
|-------|---------|-------|
| CoreFoundation | Minimum set for compliant sales transaction, receipt, tax, tender audit trail | Sale + void + basic return; Item pricing + layered tax; Tender capture (cash, card token, gift card token); Config & rule snapshot hashing; Deterministic totals reproduction |

## 2. Entity Mappings (NRF -> Kernel Concepts)
Legend: Status = planned | partial | complete | deferred | n/a

| Mapping ID | NRF Entity | Kernel Concept | Phase | Status | Notes |
|------------|-----------|---------------|-------|--------|-------|
| NRF-TRANS-HEADER | RETAIL_TRANSACTION | TransactionAggregate | CoreFoundation | planned | Header: ids, timestamps, store, operator, currency. Fiscal fields deferred. |
| NRF-TRANS-LINE-ITEM | RETAIL_TRANSACTION_LINE_ITEM | TransactionLineItem | CoreFoundation | planned | Sale/return indicator, product ref, qty, unit price, extended, discounts. |
| NRF-ITEM | ITEM | ProductSnapshot | CoreFoundation | planned | SKU, GTIN, description, tax category, regular price, hierarchy. |
| NRF-PRICE | PRICE_DERIVATION_RULE | PricingAdjustmentRule | CoreFoundation | planned | Reduced to promo rule IR reference + hash. |
| NRF-TAX | TAX | TaxComponent | CoreFoundation | planned | Jurisdiction layers + basis + rate + amount + inclusive flag. |
| NRF-TENDER-LINE | TENDER_LINE_ITEM | TenderLine | CoreFoundation | planned | Cash, tokenized card, gift card token; no raw PAN. |
| NRF-CUSTOMER-LINK | CUSTOMER | AttachedCustomerRef | CoreFoundation | planned | Opaque customer id + loyalty id token only. |
| NRF-STORE | RETAIL_STORE | StoreContext | CoreFoundation | planned | Store id, region codes, currency, timezone. |
| NRF-OPERATOR | OPERATOR | OperatorContext | CoreFoundation | planned | Operator id + role claims only. |
| NRF-DISCOUNT-ALLOCATION | RETAIL_PRICE_MODIFICATION | PriceAdjustment | CoreFoundation | planned | Per-line & transaction adjustments; rule hash + reason code. |
| NRF-LOYALTY-ACCUM | LOYALTY_ACCOUNT_TRANSACTION | LoyaltyAccrualEvent | CoreFoundation | deferred | Phase 2. |
| NRF-INVENTORY-ONHAND | INVENTORY_BALANCE | InventorySnapshotRef | CoreFoundation | deferred | Phase 2; only reservation stub Phase 1. |

## 3. Requirements (CoreFoundation Phase)
Format: ID, Title, Criticality, External Refs, Description (optional), Acceptance Criteria.

### REQ-NRF-TRANS-010
- Title: Create new sales transaction
- Criticality: High
- External Refs: RETAIL_TRANSACTION
- Acceptance:
  1. BeginTransaction returns new TransactionId and SnapshotId
  2. Transaction state = Building

### REQ-NRF-TRANS-020
- Title: Add standard sale line
- Criticality: High
- External Refs: RETAIL_TRANSACTION_LINE_ITEM, ITEM
- Acceptance:
  1. AddLineItem with valid SKU creates line with quantity and base price
  2. Line total = unitPrice * quantity prior to adjustments

### REQ-NRF-TRANS-030
- Title: Apply pricing adjustments
- Criticality: High
- External Refs: RETAIL_PRICE_MODIFICATION
- Acceptance:
  1. Promotion rule IR produces deterministic adjustment with ruleHash
  2. Multiple adjustments ordered by priority then ruleHash

### REQ-NRF-TAX-010
- Title: Layered tax calculation
- Criticality: High
- External Refs: TAX
- Acceptance:
  1. Given multi-jurisdiction rates all components returned separately
  2. Rounding policy applied after per-component accumulation

### REQ-NRF-TENDER-010
- Title: Add cash tender
- Criticality: High
- External Refs: TENDER_LINE_ITEM
- Acceptance:
  1. AddTender cash reduces balance due
  2. Change due computed if amount > outstanding

### REQ-NRF-TENDER-020
- Title: Add tokenized card tender
- Criticality: High
- External Refs: TENDER_LINE_ITEM
- Acceptance:
  1. PAN never logged
  2. Authorization event emitted with maskedAccountNumber

### REQ-NRF-CONFIG-010
- Title: Config snapshot determinism
- Criticality: High
- External Refs: (none)
- Acceptance:
  1. SnapshotId stable for identical source records
  2. Transaction audit includes SnapshotId

### REQ-NRF-AUDIT-010
- Title: Deterministic totals hashing
- Criticality: High
- External Refs: RETAIL_TRANSACTION, RETAIL_TRANSACTION_LINE_ITEM
- Acceptance:
  1. Totals hash changes when any line/tax/tender amount changes
  2. Replayed scenario yields identical hash

### REQ-NRF-SEC-010
- Title: Secure logging classification enforcement
- Criticality: High
- External Refs: (none)
- Acceptance:
  1. Unclassified field excluded from log output
  2. Payment tender logs contain no raw PAN

## 4. Traceability Placeholders
| Requirement ID | Syscalls Impacted (expected) | Tests (planned) | Coverage Status |
|----------------|------------------------------|-----------------|-----------------|
| REQ-NRF-TRANS-010 | BeginTransaction | Scenario: StartTxn | pending |
| REQ-NRF-TRANS-020 | AddLineItem | Scenario: AddLine Basic | pending |
| REQ-NRF-TRANS-030 | PriceTransaction | Scenario: PromoDeterminism | pending |
| REQ-NRF-TAX-010 | PriceTransaction | Scenario: TaxLayers | pending |
| REQ-NRF-TENDER-010 | AddTender | Scenario: CashTender | pending |
| REQ-NRF-TENDER-020 | AddTender | Scenario: CardTokenTender | pending |
| REQ-NRF-CONFIG-010 | BeginTransaction | Unit: SnapshotHashStable | pending |
| REQ-NRF-AUDIT-010 | CompleteTransaction | Scenario: TotalsHashReplay | pending |
| REQ-NRF-SEC-010 | All logging interceptors | Unit: RedactionEnforced | pending |

## 5. Gating Mapping
| Gate | Related Requirements |
|------|----------------------|
| Security | REQ-NRF-SEC-010, REQ-NRF-TENDER-020 |
| Conformance | All core foundation REQs |
| Performance | REQ-NRF-TRANS-020, REQ-NRF-TRANS-030, REQ-NRF-TAX-010 |

## 6. Next Additions (Planned for Iteration 2)
- Returns / Exchange flow (new REQs)
- Suspended transaction support
- Loyalty accrual (activate NRF-LOYALTY-ACCUM mapping)
- Inventory reservation semantics (partial coverage)

## 7. Change Log
| Date | Change | Author |
|------|--------|--------|
| 2025-09-12 | Initial Markdown catalog created (migrated from XferLang draft) | System |

---
End of Iteration 1 Requirements Catalog.
