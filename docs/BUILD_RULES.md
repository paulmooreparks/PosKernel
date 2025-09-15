# POS Kernel Build Rules and Quality Gate

This document defines the mandatory build standards enforced for all changes.
All contributors must follow these rules exactly. No exceptions.

## 1. Rebuild Discipline
- Always perform a FULL REBUILD (not incremental) after any change.
- Ignore all historical logs. Only the latest rebuild output is authoritative.
- Never speculate about errors/warnings not present in the current rebuild output.
- Use Visual Studio commands to perform builds, not command lines.
- Use Visual Studio commands to modify project properties, not manual edits to .csproj files.
- Don't use command lines to do things that Visual Studio can do directly.

## 2. Warnings-as-Errors Policy
- `TreatWarningsAsErrors = true` is assumed.
- A clean build means: 0 errors, 0 warnings.
- No warning is ever deferred, tolerated, or suppressed as "future work".

## 3. Output-Driven Fix Loop
1. Make a change (minimal, targeted).
2. Full rebuild.
3. Read ONLY the current rebuild output.
4. Fix exactly what appears.
5. Repeat until clean.
6. Do not add unrelated refactors while resolving build issues.

## 4. Prohibited Practices
- Do not rely on signature/intellisense artifacts to infer errors.
- Do not re-introduce removed entry points or duplicate `Main` methods.
- Do not insert placeholder async methods without proper semantics.
- Do not introduce unused fields/variables to silence logic paths.
- Do not suppress analyzers instead of fixing root causes.

## 5. Async / Await Rules
- No `async` method may lack a meaningful `await` (CS1998 must be resolved):
  - If inherently sync: remove `async` + return `Task.FromResult(...)` or make it sync.
  - If placeholder: use `await Task.CompletedTask;` outside of `lock` blocks.
- Never `await` inside a `lock` (avoids CS1996 / deadlocks).

## 6. XML Documentation Compliance
- All public types and members must have XML docs (eliminates CS1591):
  - Summary required.
  - Parameters documented.
  - Return values documented where applicable.
  - Keep docs meaningful and consistent.

## 7. Platform-Specific APIs
- Guard platform-dependent calls (e.g. Event Log) with runtime checks (`RuntimeInformation`).
- Avoid CA1416 violations by isolating platform logic.

## 8. Dead / Redundant Code
- Remove unused fields (CS0414) and members rather than ignoring them.
- Eliminate duplicate type or member definitions immediately.
- Resolve merge artifacts (e.g. duplicated blocks, doubled namespaces) before proceeding.

## 9. JSON / IPC Layer Integrity
- No partial or conflicting implementations left in IPC (e.g. NamedPipeServer) files.
- Ensure one authoritative class definition per file unless intentional.
- Keep request/response DTOs documented and non-duplicated.

## 10. Entry Point Integrity
- Exactly one executable entry point (`Main`) per runnable project.
- Test harnesses must not silently replace production entry points.

## 11. Minimal Change Principle
- Each fix targets only the errors/warnings from the latest rebuild.
- Avoid speculative restructuring while build is not clean.
- Stop after each change set to re-verify with a rebuild.

## 12. Logging & Diagnostics
- Logging additions must not mask actual failures.
- No swallowing exceptions without logging.

## 13. Consistency & Formatting
- Maintain existing code style (bracing, spacing, naming).
- Do not introduce inconsistent XML doc tone or formatting.

## 14. Validation Before Completion
- Final step before considering a task complete: full rebuild -> zero issues.
- If any new warning appears while fixing others, loop continues until clean again.

## 15. Never Assume Hidden State
- Do not claim success based on assumptions (e.g. "must be clean"). Only the rebuild output proves correctness.

## 16. Rust / Interop Boundary (Contextual)
- When touching IPC / interop layers, do not insert fake/mocked behaviors.
- All service-layer claims must reflect actual implemented behavior.

## 17. No Silent Downgrades
- Do not remove analyzers or relax build configuration to achieve a "clean" state.

## 18. Scope of Fixes
- Priority order during remediation:
  1. Syntax / structural (e.g. CS1513, duplication)
  2. Compiler errors
  3. Warnings-as-errors (logic / docs / platform / style)
  4. Post-clean refactors (only after a fully clean state)

## 19. Thread-Safety & Locks
- Never block inside async flows with sync waits.
- Do not `await` inside `lock` – refactor to capture state then await outside.

## 20. Documentation Accuracy
- XML summaries must match actual behavior (no misleading boilerplate).

---
**Enforcement:** Any PR or change set failing a full rebuild with zero warnings is rejected.
**Goal:** Deterministic, repeatable, warning-free builds that reflect production readiness.

End of build rules.
