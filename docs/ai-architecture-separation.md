# PosKernel AI Architecture Separation

## Overview

The PosKernel AI system has been architecturally separated to follow proper separation of concerns:

- **`PosKernel.AI`** - Pure business logic library (no UI dependencies)
- **`PosKernel.AI.Demo`** - Terminal.Gui demo application (pure UI)

## Architecture

### Before Separation
```
PosKernel.AI (Executable)
├── Core Business Logic (ChatOrchestrator, McpClient, etc.)
├── Terminal.Gui UI Implementation 
├── Program.cs (main entry point)
└── Dependencies: Terminal.Gui v2 + Business Logic
```

### After Separation
```
PosKernel.AI (Library)
├── Core Business Logic Only
├── Interfaces (IUserInterface, IChatDisplay, etc.)
├── ChatOrchestrator, McpClient, AiPersonalityFactory
├── No UI dependencies
└── Pure business logic library

PosKernel.AI.Demo (Executable) 
├── Terminal.Gui Implementation
├── TerminalUserInterface, TerminalChatDisplay, etc.
├── Program.cs (main entry point)
├── Store Selection UI Logic
└── Dependencies: Terminal.Gui v2 + PosKernel.AI
```

## Benefits

### ✅ **Reusable Business Logic**
- `PosKernel.AI` can be used by any UI framework (WPF, Blazor, MAUI, etc.)
- Core AI logic is completely UI-agnostic
- Easy to create different demo applications

### ✅ **Clean Dependencies**
- No circular dependencies between UI and business logic
- Terminal.Gui dependency isolated to demo project only
- Clear separation of concerns

### ✅ **Better Testing**
- Business logic can be unit tested without UI dependencies
- UI components can be tested separately
- Easier to mock dependencies

### ✅ **Architectural Compliance**
- Follows FAIL FAST principle
- No hardcoded UI assumptions in business logic
- Pure interfaces enable any UI implementation

## Usage

### Running the Demo
```bash
# Terminal.Gui demo application
dotnet run --project PosKernel.AI.Demo

# With different modes
dotnet run --project PosKernel.AI.Demo --mock     # Development mode
dotnet run --project PosKernel.AI.Demo --debug   # Debug logging
dotnet run --project PosKernel.AI.Demo --help    # Show options
```

### Using the Library
```csharp
// Reference PosKernel.AI in your project
// Implement IUserInterface for your UI framework
// Use ChatOrchestrator for business logic

var orchestrator = new ChatOrchestrator(
    mcpClient,
    personalityConfig,
    storeConfig,
    logger,
    // ... other dependencies
);

// Your UI implementation
public class MyCustomUI : IUserInterface
{
    public IChatDisplay Chat { get; }
    public IReceiptDisplay Receipt { get; }
    public ILogDisplay Log { get; }
    
    // Implement for your UI framework (WPF, Blazor, etc.)
}
```

## Project Structure

### PosKernel.AI (Library)
- `Core/` - ChatOrchestrator, StringHelpers, etc.
- `Services/` - McpClient, AiPersonalityFactory, etc.
- `Interfaces/` - IUserInterface, IChatDisplay, etc.
- `Models/` - ChatMessage, Receipt, etc.
- `Tools/` - KernelPosToolsProvider, PosToolsProvider

### PosKernel.AI.Demo (Application)
- `UI/Terminal/` - Terminal.Gui implementation
- `Services/` - Demo-specific services (KopitiamProductCatalogService, etc.)
- `Program.cs` - Application entry point

## Creating New UI Implementations

To create a new UI implementation (e.g., Blazor web app):

1. **Create new project** referencing `PosKernel.AI`
2. **Implement interfaces**:
   ```csharp
   public class BlazorChatDisplay : IChatDisplay { /* ... */ }
   public class BlazorReceiptDisplay : IReceiptDisplay { /* ... */ }
   public class BlazorLogDisplay : ILogDisplay { /* ... */ }
   public class BlazorUserInterface : IUserInterface { /* ... */ }
   ```
3. **Use ChatOrchestrator** for business logic
4. **Configure services** as needed

The business logic remains identical across all UI implementations.
