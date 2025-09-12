# Development Log

This file tracks key decisions and context for the POS Kernel project development.

## Recent Changes (December 2025)

### Project Structure Cleanup
- **Issue**: Multiple .NET assemblies with only empty `Class1.cs` files
- **Action**: Removed references to PosKernel.Services, PosKernel.Runtime, PosKernel.AppHost
- **Result**: Simplified architecture with direct Rust ↔ .NET integration

### Git Repository Setup
- **Issue**: Git repo was initialized in `pos-kernel-rs/` instead of solution root
- **Action**: Moved `.git` folder to solution root, created comprehensive `.gitignore`
- **Result**: Unified repository for all components (Rust + .NET + docs)
- **GitHub**: https://github.com/paulmooreparks/PosKernel

### Documentation Structure
- **Created**: Comprehensive documentation in `docs/` directory
  - `docs/rust/` - Internal Rust implementation details
  - `docs/c-abi/` - C API specification
  - `docs/dotnet/` - .NET wrapper documentation
  - `docs/examples/` - Multi-language usage examples
- **Philosophy**: Documentation-driven development with parallel maintenance

### Architecture Decisions
- **Rust-First**: Core business logic in Rust for performance and safety
- **Win32-Style C ABI**: Handle-based resource management, structured error codes
- **Thin .NET Wrapper**: Object-oriented convenience layer over C ABI
- **Mixed-Mode Debugging**: Full debugging support across language boundaries

## Current Issues

### Visual Studio Integration
- **Problem**: Folder View vs Solution View affects build shortcuts
- **Solution**: Use Solution View for full MSBuild integration
- **Shortcuts**: Ctrl+Shift+B (build), F5 (debug), Ctrl+F5 (run)

### Copilot Chat Context
- **Limitation**: Chat history lost when switching views or restarting VS
- **Workaround**: Document key context in code comments and this log file

## Next Steps

1. **Performance Optimization**: Replace `Mutex<HashMap>` with `DashMap` for better concurrency
2. **Feature Extensions**: Multiple tender types, line item modification
3. **Language Bindings**: Python, JavaScript wrappers following same patterns
4. **CI/CD**: GitHub Actions for automated building and testing

## Key Files

- `pos-kernel-rs/src/lib.rs` - Core Rust implementation
- `PosKernel.Host/Pos.cs` - .NET wrapper API
- `PosKernel.Host/RustNative.cs` - P/Invoke declarations  
- `docs/` - All documentation
- `README.md` - GitHub repository front page

## Build Commands

```bash
# Full build (Rust + .NET)
dotnet build

# Rust only
cd pos-kernel-rs && cargo build

# Run demo
dotnet run --project PosKernel.Host

# Run tests
cd pos-kernel-rs && cargo test
```
