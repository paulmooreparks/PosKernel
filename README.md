# POS Kernel

**A Rust point-of-sale transaction kernel with global extensibility and AI integration**

[![License: Apache 2.0](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)
[![Build Status](https://img.shields.io/badge/Build-Passing-green.svg)](#)
[![Version](https://img.shields.io/badge/Version-v0.4.0--service--ready-green.svg)](#)

## Overview

POS Kernel is a culture-neutral transaction processing kernel designed for global deployment. Built with Rust for security and performance, it provides both HTTP service and FFI interfaces. 

## Demo Video
[![POS Kernel Demo](https://img.youtube.com/vi/OvtzOJsVfEg/0.jpg)](https://www.youtube.com/watch?v=OvtzOJsVfEg)

## Current Status: Service Foundation Complete + Currency-Aware Architecture

HTTP service with comprehensive void functionality and multi-currency support
```bash
# Start Rust HTTP service
cd pos-kernel-rs && cargo run --bin pos-kernel-service
# Result: HTTP API at localhost:8080 with currency-aware operations

# Start .NET AI client
cd PosKernel.AI && dotnet run
# Result: Terminal.GUI interface communicating with Rust service
```

## Key Achievements

- **Service Architecture**: HTTP service with RESTful API
- **Currency-Aware Operations**: Proper decimal handling for multiple currencies (SGD, USD, JPY, BHD)
- **Comprehensive Void Functionality**: Audit-compliant void operations with reversing entries
- **ACID Compliance**: Write-ahead logging with transaction recovery
- **Multi-Process Support**: Terminal coordination with exclusive locking
- **AI Integration**: Natural language processing with cultural intelligence
- **Portable Interface**: Terminal.Gui demo interface with real-time receipt updates

## Architecture Status

### Current v0.4.0 Stack (Service Foundation Complete)

```
┌─────────────────────────────────────────────────────────────────┐
│                    .NET AI Client Layer                         │
│  • Terminal.Gui portable interface                              │
│  • Cultural AI personalities (Singaporean, American, etc.)      │
│  • Real-time receipt updates with void operations               │
│  • Multi-kernel support (HTTP service + in-process)             │
└─────────────────────────────────────────────────────────────────┘
                              ↓ HTTP API
┌─────────────────────────────────────────────────────────────────┐
│                  Rust HTTP Service Layer                        │
│  • RESTful API with session management                          │
│  • Currency-aware conversions (no hardcoded assumptions)        │  
│  • Void operations: DELETE and PUT endpoints                    │
│  • Error handling with descriptive messages                     │
└─────────────────────────────────────────────────────────────────┘
                              ↓ FFI calls
┌─────────────────────────────────────────────────────────────────┐
│                    Rust Kernel Core                             │
│  • Currency-aware decimal arithmetic (queries metadata)         │
│  • ACID-compliant Write-Ahead Logging                           │
│  • Multi-process terminal coordination                          │
│  • Void operations: pk_void_line_item(), pk_update_line_qty()   │
│  • Precise i64 storage (no floating-point errors)               │
└─────────────────────────────────────────────────────────────────┘
```

### Multi-Currency Support Verified
- **SGD/USD** (2 decimals): S$1.40 ↔ 140 minor units  
- **JPY** (0 decimals): ¥1400 ↔ 1400 minor units
- **BHD** (3 decimals): BD1.400 ↔ 1400 minor units

## Comprehensive Void Functionality

### Audit-Compliant Implementation
All void operations use **reversing entries** instead of destructive deletions:

```http
# Void line item with audit trail
DELETE /api/sessions/{session_id}/transactions/{transaction_id}/lines/2
Content-Type: application/json

{
    "reason": "customer requested",
    "operator_id": "CASHIER_001"
}

Response: {
    "success": true,
    "total": 1.40,  # Updated total after void
    "state": "Building",
    "line_count": 1
}
```

### Void Operation Features
- **Reversing entries**: Original entry preserved, negative entry created
- **Operator tracking**: All voids require operator identification
- **Reason codes**: Mandatory void reasons for audit compliance
- **Real-time updates**: Receipt updates immediately when items voided
- **Write-ahead logging**: All operations logged for recovery

## AI Integration with Cultural Intelligence

### Enhanced Intent Classification

### Cultural Intelligence Support
- **Multi-language**: Contextual interpretation and understanding
- **Cultural patterns**: For example, traditional Singaporean kopitiam vs coffee shop behavior
- **Modification handling**: Natural language item substitutions

## Quick Start

### Service Architecture Demo
```bash
# Terminal 1: Start Rust service
cd pos-kernel-rs
cargo run --bin pos-kernel-service
# Service starts at http://localhost:8080

# Terminal 2: Start restaurant extension service
dotnet run --project PosKernel.Extensions.Restaurant

# Start .NET AI client
dotnet run --project PosKernel.AI
```

## Architecture Goals

### Currency Neutrality ✅
- **No hardcoded currency symbols**: All formatting deferred to services
- **No decimal place assumptions**: Uses kernel currency metadata
- **Proper minor unit handling**: Currency-specific precision arithmetic

### Audit Compliance ✅  
- **Reversing entries for voids**: No destructive deletions
- **Operator ID tracking**: Full audit trail for modifications
- **Reason code requirements**: Mandatory void reasons
- **Timestamp preservation**: All operations timestamped
- **Write-ahead logging**: ACID-compliant transaction logs

### Fail-Fast Architecture ✅
- **No silent fallbacks**: All errors surface immediately
- **Clear error messages**: "DESIGN DEFICIENCY" pattern for missing services
- **Configuration validation**: Services must be properly registered
- **Architectural violation detection**: Clear failures when boundaries crossed

## Development

### Project Structure
```
PosKernel/
├── pos-kernel-rs/               # Rust kernel + HTTP service
│   ├── src/lib.rs              # Core kernel with currency support
│   ├── src/bin/service.rs      # HTTP service with currency-aware API  
│   └── proto/                  # gRPC protocol definitions (ready)
├── PosKernel.AI/                # .NET AI integration
│   ├── Core/ChatOrchestrator.cs # AI with void operation support
│   ├── Client/RustKernelClient.cs # HTTP client with void methods
│   └── Prompts/                 # Enhanced modification patterns
├── PosKernel.Extensions.Restaurant/ # Domain extension
└── docs/                        # Updated architecture docs
```

### Key Technologies
- **Rust**: Kernel core + HTTP service with Axum framework
- **.NET 9**: AI integration with multi-kernel client support
- **SQLite**: Product catalogs with modification support  
- **OpenAI**: GPT-4o with enhanced cultural intelligence
- **HTTP/REST**: Service API with currency-aware operations

### Build Commands
```bash
# Build Rust kernel + service
cd pos-kernel-rs && cargo build --release

# Build .NET client
dotnet build

# Run integration tests
cd pos-kernel-rs && cargo test
dotnet test

# Start service + client
cargo run --bin pos-kernel-service &
cd ../PosKernel.AI && dotnet run
```

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for development guidelines.

### Current Development Priorities
1. **AI Trainer Implementation**: Address intent classification improvements
2. **gRPC Protocol Support**: High-performance client integration
3. **Multi-Client Libraries**: Python, Node.js, C++ client ecosystems

## License

Licensed under the Apache License, Version 2.0. See [LICENSE](LICENSE) for details.

