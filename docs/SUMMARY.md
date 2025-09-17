# POS Kernel AI - Current Status Summary

## Context
This is a .NET 9 C# Point of Sale (POS) system with AI integration using OpenAI. The system features a Terminal.Gui-based user interface for order processing with real-time chat, receipt display, and debug logging.

## Recent Work Completed
1. **Removed Console UI**: Eliminated `PosKernel.AI.UI.Console.ConsoleUserInterface` - now uses Terminal.Gui exclusively
2. **Implemented Store Selection Dialog**: Created radio button dialog using Terminal.Gui v2 API with proper event handling (`Accepting` event, not `Clicked`)
3. **Fixed Unicode Issues**: Restored Unicode emoji support in chat display that was previously stripped out
4. **Orchestrator Timing**: Fixed timing issues where orchestrator wasn't available when UI events fired

## Current Problem - CRITICAL BUG
**The store selection dialog Cancel/ESC functionality is broken.**

### Expected Behavior
- User presses ESC or clicks Cancel in store selection dialog
- `ShowStoreSelectionDialogAsync` returns `null`
- Application logs "Store selection cancelled - exiting application" 
- Application exits cleanly via `ui.ShutdownAsync()`

### Actual Behavior
- User presses ESC or clicks Cancel
- Dialog shows debug log: "Dialog completed - selectedStore: [SomeStoreName], cancelled: [true/false]"
- Application continues to main TUI interface instead of exiting
- Shows chat interface with a default store instead of null

### Code Location
- **File**: `PosKernel.AI\Program.cs`
- **Method**: `ShowStoreSelectionDialogAsync` (line ~294)
- **Issue**: The dialog's Cancel/ESC event handlers are not properly returning `null`

## Terminal.Gui v2 API Details
- **Package**: Terminal.Gui v2.0.0
- **Button Events**: Use `Accepting` event, NOT `Clicked` or `Command`
- **Event Signature**: `(sender, e) => { ... }` for `EventHandler<CommandEventArgs>`
- **RadioGroup**: Use `SelectedItem` property for selected index
- **Modal Dialogs**: Use `Modal = true` and `Application.Run(dialog)`

## Project Structure
- **Main UI**: `PosKernel.AI\UI\Terminal\TerminalUserInterface.cs`
- **Chat Logic**: `PosKernel.AI\Core\ChatOrchestrator.cs`
- **Program Entry**: `PosKernel.AI\Program.cs`
- **Models**: `PosKernel.AI\Models\` (ChatMessage, Receipt, etc.)

## Next Steps - IMMEDIATE PRIORITY
1. **Fix Cancel/ESC Bug**: Debug why `ShowStoreSelectionDialogAsync` isn't returning `null` when Cancel/ESC is pressed
2. **Test Thoroughly**: Ensure ESC key and Cancel button both properly exit the application
3. **Remove Debug Logging**: Clean up temporary debug logs once bug is fixed

## Technical Notes
- Uses dependency injection with `ServiceCollection`
- Real kernel integration via Rust service (optional mock mode available)
- Custom logging that routes to Terminal.Gui debug pane to avoid console corruption
- Store configurations support multiple personalities (American, Singaporean, French, etc.)

## Working Features
- ✅ Terminal.Gui interface initialization
- ✅ Store selection dialog UI (radio buttons work)
- ✅ Chat orchestrator integration
- ✅ Receipt display and updates
- ✅ Debug logging pane
- ✅ Unicode emoji support in logs
- ❌ **BROKEN**: Cancel/ESC exits application (CRITICAL BUG)

## Command Line Usage
```bash
PosKernel.AI                 # Real kernel integration  
PosKernel.AI --mock          # Mock integration
PosKernel.AI --debug         # Debug logging enabled
```

## Key Dependencies
- Terminal.Gui 2.0.0
- Microsoft.Extensions.DependencyInjection 9.0.0
- .NET 9 target framework
- OpenAI API integration via custom MCP client
