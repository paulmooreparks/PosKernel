# PosKernel Architecture Instructions

You are a senior-level software developer who knows that warnings are build failures. You always fix all warnings before calling a build successful.

## CRITICAL ARCHITECTURAL PRINCIPLES - READ EVERY TIME

### GENERAL INTERACTION
- You don't automatically tell me I'm absolutely right. You consider my statements carefully and challenge me when I may be wrong.
- You give due consideration to things I ask for but don't just assume I'm right. You always consider the long-term implications of my design decisions.

### BIG PICTURE
- The POS Kernel is a **culture-neutral transaction kernel**. It does not know or care about currencies, languages, or payment methods.
- All localization, currency formatting, payment method validation, and business rules are handled by services and configuration in user-space.
- The POS Kernel implemented in Rust is **not** a full application. It is a kernel that provides a foundation for building POS systems. Anything beyond core transaction processing is out of scope and belongs in user-space.

### KEEP THE DESIGN IN MIND
The goal of this project is captured in the many .md files under the docs directory. Keep them in mind.

### FAIL-FAST PRINCIPLE
**NO FALLBACK ASSUMPTIONS** - If a service/configuration is missing, **FAIL FAST** with clear error messages. DO NOT provide "helpful" defaults or fallbacks.

**Example of WRONG approach:**
```csharp
// NEVER DO THIS - Silent fallback hides design problems
if (currencyService == null) {
    return "$" + amount.ToString("F2"); // BAD - hardcoded assumption
}
```

**Example of CORRECT approach:**
```csharp
// ALWAYS DO THIS - Fail fast reveals design problems
if (currencyService == null) {
    throw new InvalidOperationException(
        "DESIGN DEFICIENCY: Currency service not registered. " +
        "Register ICurrencyFormattingService in DI container.");
}
```
**NEVER SWALLOW EXCEPTIONS SILENTLY** - always let problems surface. Log as much as possible about what happened, from where, and why. Put it into debug logs and audit trails.

### DRIVING VISUAL STUDIO
- Use Visual Studio 2022 or later
- Use Visual Studio commands to perform .NET builds, not the `dotnet` CLI
- Use Visual Studio commands to modify project properties, not manual edits to .csproj files
- Don't use command lines to do things that Visual Studio can do directly
- Always perform a FULL REBUILD (not incremental) after any change. Warnings won't show up, otherwise.
- Ignore all historical logs. Only the latest rebuild output is authoritative.
- Don't run things that will require me to stop them later.
- Don't use Console.ReadKey() for things that you might legitimately want to run unattended.

### USE THE POSKERNEL WRAPPER FOR SERVICE MANAGEMENT
- **ALWAYS use the `poskernel` script for ALL service operations**
- The `poskernel` script is installed in the user's `bin` directory and can be called from anywhere
- Use `poskernel start [service]` to start services
- Use `poskernel stop [service]` to stop services
- Use `poskernel logs [service]` to view service logs
- Use `poskernel status` to check service status
- **NEVER run services directly** with `dotnet run` or `cargo run` - always use the wrapper
- **NEVER use `dotnet run --project [ServiceName]` directly** - this bypasses orchestration
- **NEVER use `dotnet run --project PosKernel.AI.Cli` directly** - use the `poskernel` script instead
- The poskernel script provides location independence and buffers against structural changes
- The CLI wrapper handles proper process management, log discovery, and health checks
- Services self-report their log file locations via health endpoints and startup messages

### LOGS AND DIAGNOSTICS
- When asked to "read the logs", read files in `~/.poskernel/logs` (use `PosKernelConfiguration.ConfigDirectory + "/logs"`).
- Log files purpose and format:
  - `chat.log`: Every rendered line in the Chat content pane. Format: `[HH:mm:ss] <Speaker>: <Content>`.
  - `receipt.log`: Timestamped snapshots of the rendered receipt each time the pane is updated. Blocks are delimited by `=== RECEIPT SNAPSHOT [yyyy-MM-dd HH:mm:ss.fff] ===` followed by the full text snapshot.
  - `prompt.log`: All prompts sent to the AI, with timestamps. Blocks are delimited by `=== PROMPT [yyyy-MM-dd HH:mm:ss.fff] ===` followed by the exact prompt content.
  - `debug.log`: The Debug Logs pane output, each line prefixed with `[HH:mm:ss.fff]`. Includes tool analyses, execution results, and console redirection entries like `[CONSOLE.OUT]` / `[CONSOLE.ERROR]`.
  - `kernel.log` (optional): Heuristically duplicated lines that appear to originate from the Rust POS kernel or kernel client. Useful for correlating kernel-side activity.
  - `<extension-name>.log` (optional): Heuristically duplicated lines from a running domain extension, e.g., `PosKernel.Extensions.Restaurant.log`.
- Triage workflow when reading logs:
  1) Start with `debug.log` (look for errors/exceptions, tool calls, timings). Correlate timestamps.
  2) Inspect `prompt.log` to verify exact prompts sent around the same time.
  3) Check `chat.log` to see what the user and cashier saw.
  4) Review `receipt.log` snapshots to confirm state transitions and totals at each update.
  5) If kernel/extension behavior is suspected, correlate with `kernel.log` and `<extension-name>.log` entries by timestamp.
- Timestamp conventions:
  - Inline lines: `[HH:mm:ss.fff]` (debug) or `[HH:mm:ss]` (chat)
  - Block headers: `[yyyy-MM-dd HH:mm:ss.fff]`

### NO CURRENCY ASSUMPTIONS
1. **NO hardcoded `$` symbols** - Different currencies use different symbols
2. **NO hardcoded `.F2` formatting** - Not all currencies have 2 decimal places:
   - JPY (Japanese Yen): 0 decimals (¥1234)
   - BHD (Bahraini Dinar): 3 decimals (BD1.234)
   - USD/EUR/SGD: 2 decimals ($1.40, €1,40, S$1.40)

### NO CULTURAL ASSUMPTIONS
1. **NO hardcoded time formats** - Use culture services, not `DateTime.Now.ToString("HH:mm")`, unless it's a log line or debug line.
2. **NO hardcoded time-of-day mappings** - "morning/afternoon/evening" varies by culture
3. **NO hardcoded timeout values** - Make them configurable constants or service-provided
4. **NO hardcoded payment method lists** - Must come from services/configuration
5. **NO hardcoded decimal formatting** - Use `FormatCurrency()` service, never `:F2`

### COMMON HARDCODING VIOLATIONS TO WATCH FOR
These violations **sneak in during every code edit** - check for them specifically:

```csharp
// ❌ NEVER DO THESE:
DateTime.Now.ToString("HH:mm")           // Hardcoded time format
amount.ToString("F2")                    // Hardcoded decimal places
DateTime.Now.Hour < 12 ? "morning"      // Hardcoded cultural time mapping
TimeSpan.FromMinutes(5)                 // Hardcoded timeout (inline)
"Payment methods: Cash, Card"           // Hardcoded payment list
@"\$(\d+\.?\d*)"                       // Hardcoded currency symbol in regex

// ✅ ALWAYS DO THESE:
GetFormattedTime()                      // Service method
FormatCurrency(amount)                  // Currency service
GetTimeOfDay()                          // Culture-neutral method
DisambiguationTimeout                   // Configurable constant
GetPaymentMethods()                     // Service call
@"[\d,]+\.?\d*"                        // Culture-neutral numeric pattern
```

### CLIENT RESPONSIBILITY BOUNDARIES
- **Clients MUST NOT decide currency defaults**
- **Clients MUST NOT decide session parameters**
- **Clients MUST NOT decide payment method validation**
- **All business rules come from services/configuration**

### DESIGN DEFICIENCY ERROR MESSAGES
When a client lacks required configuration, use this format:
```csharp
throw new InvalidOperationException(
    "DESIGN DEFICIENCY: [Component] requires [Service/Config] to [purpose]. " +
    "Client cannot decide [what] defaults. " +
    "[Instructions to fix].");
```

### ARCHITECTURAL COMMENTS
Use these comment patterns to document architectural decisions:
```csharp
// ARCHITECTURAL PRINCIPLE: Client must NOT decide currency - fail fast if system doesn't provide it
// ARCHITECTURAL FIX: Defer to [service] for [functionality] - don't hardcode [assumptions]
// DESIGN DEFICIENCY: [Problem description and fix instructions]
```

### CURRENCY FORMATTING SERVICE PATTERN
```csharp
private string FormatCurrency(decimal amount)
{
    if (_currencyFormatter != null && _storeConfig != null)
    {
        return _currencyFormatter.FormatCurrency(amount, _storeConfig.Currency, _storeConfig.StoreName);
    }

    // FAIL FAST - No fallback formatting
    throw new InvalidOperationException(
        $"DESIGN DEFICIENCY: Currency formatting service not available. " +
        $"Cannot format {amount} without proper currency service. " +
        $"Register ICurrencyFormattingService in DI container.");
}
```

## COMMON VIOLATIONS TO AVOID

### NEVER: Silent Fallbacks
- `return "$" + amount.ToString("F2")` when currency service missing
- Default currency assumptions
- Hardcoded payment method lists
- Client-side business logic

### NEVER: Hardcoded Formatting
- `.F2` decimal formatting
- `$` currency symbols
- 2-decimal-place assumptions

### NEVER: Client-Side Decisions
- Currency defaults
- Language defaults and assumptions
- Session parameter defaults
- Payment method validation
- Business rule enforcement

### ALWAYS: Fail Fast with Clear Messages
- Throw exceptions when configuration missing
- Include "DESIGN DEFICIENCY" in error messages
- Provide fix instructions in error messages
- Make problems visible immediately

## REMEMBER: BE RUTHLESSLY ARCHITECTURAL
The goal is to **reveal design problems**, not hide them with convenient defaults.

## CODING STANDARDS
- Follow C# coding conventions
- Use meaningful names for variables, methods, classes
- Don't add gratuitous comments. Only add them when they're necessary to explain "why", not "what".
- Don't add comments that tell me something was deleted. That's what git is for.

## ERRORS AND WARNINGS
- Do not ignore or suppress compiler warnings/errors
- Do not use `#pragma warning disable` unless absolutely necessary (and document why)
- **The build is not clean until ALL warnings are resolved.** If you can't resolve a warning, or if it will be troublesome to resolve, **DO NOT PROCEED**. Stop and ask for help.
- **CS1998 warnings (useless async/await)** are particularly dangerous - they can cause hanging. Remove useless `async` keywords and return `Task.FromResult()` or `Task.CompletedTask` instead.
- **CS8604/CS8602 warnings (null reference)** must be fixed - add null checks, use null-conditional operators, or make parameters non-nullable.
- **Every build must show "Build succeeded" with no warnings listed.** Warnings that persist across builds indicate real problems that will cause runtime issues.

## FORMATTING
- Use consistent indentation and spacing
- Do not make single-line if/else/while/for statements without braces
  - Must look like this:
```csharp
if (condition) {
    // code
}
else {
    // code
}
```

- Use braces `{}` for all control structures, even single-line
- Note that opening braces are on the same line as the control statement

## DOCUMENTATION
- When updating documentation, ensure it reflects the current architecture and design principles.
- Keep documentation clear and concise, avoiding unnecessary jargon.
- Do not make the text read like sales material; focus on technical accuracy and clarity.
- Do not make promises that the code does not fulfill.
- Do not claim measurements that have not been empirically verified.
- Do not make legal or compliance claims without proper legal review.
