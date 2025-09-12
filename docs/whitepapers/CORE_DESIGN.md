# POS Kernel Core Design (Living Whitepaper)

Status: Draft (Iteration 2)
Owner: Architecture / Platform Team
Canonical Format: Markdown (authored), may be exported to XferLang artifact later.
Scope: Foundational architecture, contracts, extensibility, serialization, configuration, rule execution, deployment profiles, compliance mapping.

---
## 1. Vision & Goals
Create a deterministic, minimal, auditable Point-of-Sale Kernel analogous to an operating system kernel. It provides a stable system-call (syscall) surface for transactional retail operations (sale, pricing, tendering, tax, adjustments) across heterogeneous deployment scenarios ranging from bare-metal embedded devices to cloud-distributed omnichannel platforms. All user interface, orchestration, integration, and localization reside in "user-space"—built upon the kernel’s explicit contracts.

### Core Goals
- Stability: Narrow, versioned syscall surface with long-term backward compatibility.
- Determinism: Given identical inputs (config snapshot + rule artifacts + commands + time source), identical outputs.
- Extensibility: Pluggable capabilities (pricing rules, tax engines, payment providers) without kernel modification.
- Performance Tiers: Single-process ultra-low-latency up to distributed microservice scale with consistent semantics.
- Auditability: Persist sufficient metadata (hashes, versions, config scopes) to replay or forensically reconstruct outcomes.
- Portability: Same logic usable across device, edge, server, cloud, test harness.
- Observability: Structured events and metrics with minimal overhead; optional deep tracing.
- XferLang-First: XferLang as canonical configuration & interchange authoring format; JSON supported for compatibility.
- Secure Logging & Data Protection: Built-in classification, redaction, and compliance alignment (PCI, privacy laws).
- Multi-Language Hostability: Stable C ABI enabling broad ecosystem bindings (initial .NET user-space host).

---
## 2. Terminology
| Term | Definition |
|------|------------|
| Kernel | Core library implementing state transitions + domain invariants via syscalls. |
| Syscall | Versioned, strongly-typed operation exposed by the kernel (e.g., BeginTransaction, AddItem, PriceTransaction). |
| Capability | Modular functional domain add-in (Tax, Promotions, Payments). |
| Provider | Concrete implementation of a capability contract. |
| Dispatcher | Internal command routing mechanism from syscall invocation to handlers. |
| Rule Artifact | Immutable compiled rule (pricing/tax/eligibility) IR + metadata (hash, version, scope). |
| Mini-VM | Constrained interpreter / AOT compiler executing rule IR deterministically. |
| Config Record | Single configuration datum scoped hierarchically (e.g., Store, Country). |
| Config Snapshot | Merged, immutable, hashed view of effective configuration for transaction lifetime. |
| KernelContext | Immutable request/session context (correlation, user identity, time, currency). |
| Event | Domain signal emitted internally (LineAdded, TaxCalculated) and optionally exported. |
| Transaction Aggregate | Core aggregate holding lines, adjustments, tenders, state machine. |
| Scope Chain | Ordered hierarchy for config & jurisdiction resolution. |
| Data Classification | Label attached to data (PUBLIC, BUSINESS, SENSITIVE, PII, PCI) guiding handling & logging. |
| Device Adapter | User-space or host-layer module implementing a specific physical device contract. |
| ABI | Application Binary Interface—stable binary contract for cross-language invocation. |

---
## 3. Architectural Overview
Layers (logical):
1. Abstractions: Value objects, IDs, enums, DTO contracts, syscall interfaces, error codes.
2. Core: Aggregates, state machines, pricing/tax pipelines, rule engine, deterministic operations.
3. Runtime: Capability registry, interceptor pipeline, config snapshot builder, rule artifact loader, dispatcher.
4. Services (Reference): In-memory catalog, flat tax, mock payment, basic promotion provider.
5. Hosting: KernelHostBuilder, profiles, dependency wiring, observability bootstrap, transport adapters.
6. AppHost(s): Example or production hosts (console POS, API gateway, headless test runner, device integration service).
7. Device Integration Layer (host / user-space): Abstracts physical peripherals; kernel only consumes abstract events / intents.
8. FFI/ABI Layer: C ABI export surface + binding generation for managed / scripting languages.

Separation: Kernel never depends on UI, transport stacks, databases, external device SDKs, or hardware drivers. Hardware integration lives in host/user-space via stable provider interfaces.

---
## 4. Deployment Profiles
| Profile | Characteristics |
|---------|-----------------|
| BareMetalProfile | Single process, no network, in-memory providers, optional AOT, zero-reflection path. |
| EdgeDeviceProfile | Local persistent store (SQLite/append-only), offline queue, device drivers. |
| MonolithProfile | Server container w/ all reference providers, REST/gRPC façade. |
| CloudDistributedProfile | Capability-oriented microservices; remote provider proxies; message bus events & outbox. |
| TestHeadlessProfile | Deterministic in-memory harness; enhanced trace capture; golden master output. |

Profiles = Pre-packaged registration modules (DI + feature flags + defaults). Users can fork/customize.

---
## 5. System Call Surface (Initial Domains)
- Transaction Syscalls: BeginTransaction, AddLineItem, UpdateLineQuantity, RemoveLineItem, ApplyManualAdjustment, PriceTransaction, AddTender, VoidTransaction, CompleteTransaction, AbortTransaction.
- Catalog Syscalls: GetProductSnapshot, ResolvePrice.
- Pricing Syscalls: ForceReprice, EvaluatePromotions.
- Tax Syscalls: CalculateTax (usually internal; exposed for audit replay).
- Tender/Payment Syscalls: AuthorizeTender, CaptureTender, VoidTender, RefundTender.
- Customer/Loyalty Syscalls: AttachCustomer, ApplyLoyaltyReward.
- Inventory Syscalls: ReserveInventory, ReleaseInventory, AdjustInventory.
- Config/Diagnostics: GetKernelVersion, GetCapabilities, GetConfigSnapshotMetadata.
- Device Interaction (indirect): PrintReceiptIntent, OpenCashDrawerIntent, CaptureSignatureIntent (intents emitted as events; host maps to devices).

Each syscall:
- Numeric ID (short int) reserved in a static registry for low-overhead multiplexing.
- Interface version (semantic). Invocation negotiates version set once per session when remote.
- Input/Output strictly value semantics (no localized strings, no UI formatting).
- Errors: KernelError(Code, SubCode, Data, RecoverableFlag, CorrelationId).

---
## 6. Internal Communication Model
Pattern: Command-oriented execution pipeline.

Flow: Syscall -> CommandEnvelope { Command, KernelContext, ConfigSnapshotRef } -> Pipeline:
1. Validation (structural + domain pre-check)
2. Authorization (IAuthorizationService check)
3. Pre-Interceptors (observability, feature gating, concurrency tokens, data classification tagging)
4. Core Handler (pure state transition + event emission)
5. Post-Interceptors (metrics, rule hash tagging, secure audit emission)
6. Event Dispatch (synchronous in-process + optional export buffering)

Dispatch Implementation:
- In-process dictionary: SyscallId -> Delegate handler (Func<Command, Task<Result>>).
- Source generator builds static map for AOT (no reflection scanning).

---
## 7. External Communication & Wire Formats
Guiding Principle: XferLang-first for human-authored config and structured interchange; JSON retained for ecosystem compatibility.

### Formats & Media Types
- XferLang primary: `application/x-xfer` (proposed); fallback: `text/x-xfer`.
- JSON secondary: `application/json`.
- Binary (optional high-performance): `application/vnd.poskernel.bin` (abstract framing for negotiated sub-codec).

### Format Negotiation
- Accept/Content-Type based; server or host chooses best match.
- Clients include: `Accept: application/x-xfer, application/json;q=0.8`.
- Response includes `Content-Type` + `X-PosKernel-Format-Version` header.

### Serialization Layers
1. Domain Model (Value Objects, Aggregates) - internal.
2. Portable DTO (neutral internal record structs). 
3. Canonical Structural Tree (Key/Value/Array/Primitive) – intermediate abstraction.
4. Encoder/Decoder (XferLang, JSON, Binary).

### XferLang Usage
- All configuration artifacts stored canonically in XferLang.
- Rule artifacts metadata manifests expressed in XferLang.
- Optional JSON export via canonical transformation preserving ordering + comments (if supported) or comment extraction into metadata.

### Binary Codec Options
Evaluation:
- Protobuf, FlatBuffers, Cap'n Proto, MessagePack, custom framing.
Decision: Phase 1 XferLang + JSON; Phase 2 MessagePack (if needed); Phase 3 optional Protobuf.

Rationale: Minimize early surface complexity.

---
## 8. Serialization Strategy Details
- Internal domain never stores JSON or Xfer AST; domains operate on typed value objects.
- Config Loader: Parse XferLang -> Intermediate Tree -> typed records -> hash.
- JSON path shares same intermediate lowering.
- Canonicalization ensures deterministic hashing.

---
## 9. Configuration System & Scope Hierarchy
(unchanged)

---
## 10. Rule Engine & Mini-VM
(unchanged)

---
## 11. Jurisdiction & Hierarchical Compliance
(unchanged)

---
## 12. Localization Responsibility Separation
(unchanged)

---
## 13. Extensibility & Modularity
(unchanged)

---
## 14. Security & Authorization
(unchanged content up to section 14.3)

---
## 15. Persistence & Event Sourcing
(unchanged)

---
## 16. Determinism & Auditing
(unchanged)

---
## 17. Versioning Strategy
(unchanged)

---
## 18. Observability
(unchanged)

---
## 19. Performance & AOT Path
(unchanged)

---
## 20. Testing & Quality
(unchanged)

---
## 21. Migration & Evolution Strategy
(unchanged)

---
## 22. Compliance & Standards Mapping (Non-Exhaustive)
(unchanged)

---
## 23. Requirements Gathering & Traceability
(unchanged)

---
## 24. Open Issues / Future Work
Updated:
- Define concrete XferLang schema for configuration & rule DSL.
- Decide on canonical binary encoding ordering rules and finalize message framing.
- Source generator strategy (Roslyn + Rust macro) for dispatch & manifest.
- Evaluate distributed transaction correlation patterns for remote tender providers.
- Provide formal IR specification & versioning rules.
- Consider secure sandboxing (WASM or IL restrictions) for third-party rule sets (optional phase).
- Introduce feature toggle rollout strategy (gradual, % store / region).
- Provide deterministic rounding policy library (Bankers, Cash Rounding) with test vectors.
- Develop compliance matrix document.
- Define device adapter test harness & simulator format (XferLang scenario for hardware signals).
- Threat model documentation (STRIDE) & security test automation strategy.
- ABI stability criteria & compatibility checker tool.
- Fuzz corpus expansion automation for parsers (XferLang, binary codec, ABI struct decoding).

---
## 25. Implementation Roadmap (Initial Slices)
Updated plan (add Rust / ABI steps):
1. Abstractions (C# prototype): Value objects, IDs, error codes, syscall interfaces, ScopeLevel enum.
2. Runtime (C#): KernelContext, dispatcher skeleton, capability registry, config snapshot builder.
3. Core (C#): Transaction aggregate base + pricing stub.
4. XferLang Integration: Parser adapter interface + placeholder; JSON parity tests.
5. Rule Engine Skeleton: IR model + interpreter scaffold.
6. Observability: Event codes + minimal logger + test collector + classification attributes.
7. Secure Logging Interceptor + masking policy tests.
8. Test Harness: Scenario DSL + golden master output generator.
9. Tax pipeline prototype (layered components).
10. Rust Kernel Spike: Implement subset (BeginTransaction, AddLineItem, PriceTransaction) with identical semantics.
11. C ABI Draft: Export function table + manifest; generate header with cbindgen.
12. Parity Tests: Cross-run C# host vs Rust kernel; ensure identical hashes for scenarios.
13. Fuzzing Setup: cargo-fuzz targets for XferLang parser & ABI boundary decoding.
14. Performance Benchmarks: Baseline microbench (Rust vs C#) for critical syscalls.
15. ABI Freeze v0 (after feedback) + introduce compatibility checker.
16. Extend Rust coverage to full syscall set; retire C# core (keep façade / tests).

---
## 26. Device Integration Architecture
(renumbered from 26; content unchanged)

(Original section content retained above; numbering adjusted to append new sections.)

---
## 27. Language & ABI Strategy
Goal: Implement production kernel in Rust for memory safety, performance, and security; expose stable C ABI enabling multi-language hosts (.NET first, future Java/Node/Python) without semantic divergence.

### 27.1 ABI Export Pattern
- Single entry: `PosKernel_GetApi(ApiVersion requested, PosKernelApi* outPtr)` returning ResultCode.
- `PosKernelApi` (#[repr(C)]) contains function pointers grouped logically (transaction, pricing, tax, tender).
- Each function signature uses only FFI-safe primitives (integers, fixed-size arrays, opaque handles, length+pointer pairs for buffers).
- Handles: 64-bit opaque identifiers referencing internal objects (transaction, rule context). Reference counting internal; host must call release for long-lived objects.
- Errors: Uniform struct `PkResult { int32_t code; int32_t subCode; uint64_t detail; }` + optional out-parameter buffers for extended data (XferLang encoded).

### 27.2 Memory & Ownership
- All buffers returned allocate via kernel allocator; host releases through `Pk_Free(void* ptr)`.
- Input buffers lifetime owned by caller; kernel copies if needed beyond call.
- No panics cross FFI boundary; panic = abort (debug) or convert to fatal error code (release) with crash telemetry event.

### 27.3 Data Representation
- Money: 128-bit signed integer (fixed scale, e.g., scale=4) -> represented as struct `{ int64 low; int64 high; uint8 scale; }` or simplified 64-bit minor units if scale uniform.
- Strings: UTF-8 pointer + length; never NUL-terminated requirement.
- Collections: Pointer + count; composite DTOs flattened or encoded as XferLang blob for future-proofing.

### 27.4 Events & Asynchrony
- Primary model: Polling ring buffer (`Pk_EventRing`) created per host session; host calls `Pk_DequeueEvents` for batch retrieval.
- Optional callback registration allowed but discouraged (reduces complexity, safer in multi-language hosts).
- Async operations (e.g., payment authorization) emit completion events with correlation ID.

### 27.5 Manifest
- `PosKernel_GetManifest` returns XferLang document describing: kernelVersion, syscallTable, struct hashes, capability descriptors, feature flags supported.
- Host caches manifest; used for compatibility & negotiation.

### 27.6 Tooling
- Header generation: cbindgen.
- .NET binding: Source generator emitting `DllImport` externs + safe wrappers returning Tasks.
- Additional languages: JNA (Java), N-API (Node.js), CFFI (Python) using the same header.
- ABI compat checker: Compares struct field offsets, sizes, exported function CRC signatures vs previous manifest.

### 27.7 Migration Strategy
1. Define semantic contract in C# reference implementation.
2. Integration test corpus generated (inputs + expected events/totals hash) serialized as XferLang vectors.
3. Rust implementation targets parity; tests load vectors and assert identical output.
4. When parity coverage >90% core scenarios, switch canonical kernel reference to Rust; C# becomes façade.

### 27.8 Security Considerations
- Fuzz all FFI entrypoints with malformed buffers (length mismatches, truncated XferLang docs).
- Memory sanitizer / AddressSanitizer runs in CI nightly.
- Strict no unsafe except in boundary modules; audited and annotated.

---
## 28. Security / Conformance / Performance Gates
All requirements and code changes must pass three gating layers before merge.

### 28.1 Security Gate
Checklist (automated where possible):
- Threat model updated if new attack surface (syscall, external input parser).
- Static analysis (clippy, cargo audit / dependency vulnerability scan) clean.
- Fuzz corpus updated for any new parser tokens / grammar constructs.
- Data classification coverage: 100% of new log fields annotated.
- No new unsafe blocks (Rust) or justification documented.
- Cryptographic operations use approved libraries; no custom primitives.

### 28.2 Conformance Gate
- Requirement trace matrix: Each REQ ID touched by change has at least one linked test.
- Manifest diff reviewed (no unapproved breaking changes in syscall signatures / struct layout).
- Schema evolution: Additive only unless major version increment planned.
- Golden master scenarios re-run; no unintended hash drift.

### 28.3 Performance Gate
- Benchmark suite executes; p95 latency regression threshold (<5% vs baseline) enforced.
- Allocation delta per syscall within budget (configured in XferLang perf policy file).
- Throughput soak test (e.g., 10k transactions) shows no new sustained GC pressure (for managed hosts) or memory growth (Rust leak detection).

Gate Outcomes: PASS / WARN (requires explicit approval) / FAIL (block). Merge requires triple PASS or justified WARN with security sign-off.

---
## 29. Requirements Coverage Source (NRF Focus)
Primary external requirements repository: NRF (including legacy ARTS / Unified Commerce) for taxonomy and data semantics. Supplementary sources integrated selectively (PCI-DSS, EMVCo, regional fiscal specs, GS1). Strategy:
- Ingest NRF entity & operation catalog; map to internal capability / syscall IDs.
- Identify deprecated / non-relevant legacy ARTS constructs; mark as NOT_APPLICABLE with rationale.
- Maintain XferLang catalog file `requirements/catalog.xfer` enumerating external reference mappings.
- Gaps flagged as NEW_INTERNAL requirements (prefixed INT-XXXX) with justification.

Traceability Example Entry (XferLang):
```
externalMapping NRF-ITEM-001 {
  externalId: "NRF.Item" 
  kernelConcept: "ProductSnapshot"
  coverage: complete
  notes: "Lot/serial extension deferred phase 2"
}
```

Coverage Metrics (reported in CI):
- External Concepts Mapped %
- Mapped Concepts Tested %
- Unmapped Concepts (count + rationale)

---
## 30. Device Integration Architecture
(renumbered from 26; content unchanged)

(Original section content retained above; numbering adjusted to append new sections.)

---
## 31. Summary
This kernel architecture treats point-of-sale domain operations as deterministic, versioned system calls over a stable abstraction surface, with configuration and rule logic externalized, hash-tracked, and executed via a constrained, auditable rule engine. XferLang is the primary configuration/interchange format with JSON and optional binary codecs for compatibility and performance.

Iteration 2 enhancements add: formal language & ABI strategy (Rust core + C ABI), multi-language host path, rigorous Security/Conformance/Performance gates, and adoption of NRF as the authoritative external requirements taxonomy with structured traceability.

Security, compliance, determinism, and performance remain first-class design tenets guiding every evolution step.

End of Iteration 2.
