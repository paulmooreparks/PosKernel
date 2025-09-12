# POS Kernel Documentation

This directory contains comprehensive documentation for the POS Kernel system, covering all layers from the Rust implementation to language-specific wrappers.

## Documentation Structure

- **[rust/](rust/)** - Rust implementation documentation
  - Core primitives and internal architecture
  - Memory management and safety guarantees
  - Performance characteristics
  
- **[c-abi/](c-abi/)** - C ABI specification
  - Function signatures and calling conventions
  - Error codes and result handling
  - Win32-style patterns and design rationale
  
- **[dotnet/](dotnet/)** - .NET wrapper documentation
  - API reference and usage examples
  - Object-oriented patterns and resource management
  - Integration guides and best practices
  
- **[examples/](examples/)** - Cross-language examples
  - Basic usage patterns
  - Advanced scenarios
  - Language-specific implementations

## Quick Start

1. Review the [C ABI specification](c-abi/README.md) for the core interface
2. Check [language-specific documentation](dotnet/README.md) for your target platform
3. Browse [examples](examples/) for practical usage patterns

## Design Principles

The POS Kernel follows these key design principles:

- **Language Agnostic**: Pure C ABI that works with any language
- **OOP Ready**: Handle-based design that maps naturally to object-oriented patterns
- **Memory Safe**: All string handling and resource management is safe
- **Performance First**: Zero-copy operations where possible, minimal overhead
- **Win32 Style**: Consistent patterns familiar to systems programmers
