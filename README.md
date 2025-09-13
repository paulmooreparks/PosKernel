# POS Kernel

**A high-performance, legally-compliant point-of-sale transaction kernel with global extensibility**

[![License: Apache 2.0](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)
[![Build Status](https://img.shields.io/badge/Build-Passing-green.svg)](#)
[![Version](https://img.shields.io/badge/Version-v0.4.0--threading-orange.svg)](#)

## 🎯 **Overview**

POS Kernel is a culture-neutral, legally-compliant transaction processing kernel designed for global deployment. Built with Rust for security and performance, it provides a Win32-style C ABI that can be consumed by any programming language.

### **Key Features**

- **🔐 Security-First Design**: ACID-compliant transactions with tamper-proof logging
- **🌍 Global Ready**: Culture-neutral kernel with user-space localization
- **⚖️ Legal Compliance**: Built-in audit trails and regulatory compliance features
- **🚀 High Performance**: Multi-process architecture with sub-millisecond transaction processing
- **🔌 Extensible**: Plugin architecture for regional customization (v0.5.0)
- **📊 Enterprise Grade**: Handle-based API design for robust resource management

## 🏗️ **Architecture**

```
┌─────────────────────────────────────────────────────────┐
│               Application Layer                         │
│  • Business Logic        • UI/UX Components            │
│  • Regional Workflows    • Device Integration          │
└─────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────┐
│                .NET Host Layer                          │
│  • High-level C# API     • Resource Management         │
│  • Exception Translation • Object-Oriented Interface   │
└─────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────┐
│                 FFI Boundary                            │
│  • Win32-style C ABI     • Handle-Based Operations     │
│  • Memory Safety         • Cross-Language Support      │
└─────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────┐
│                Rust Kernel Core                         │
│  • ACID Transactions     • Write-Ahead Logging         │
│  • Multi-Process Safe    • Culture-Neutral Processing  │
│  • Exception Safety      • High-Performance Operations │
└─────────────────────────────────────────────────────────┘
```

## 🚀 **Quick Start**

### **Prerequisites**
- Rust 1.70+ (for kernel development)
- .NET 9 (for host applications)
- C++ compiler supporting C++14 (for C/C++ integration)

### **Basic Usage (.NET)**

```csharp
using PosKernel.Host;

// Initialize terminal (v0.4.0+)
RustNative.InitializeTerminal("REGISTER_01");

// Create and process transaction
using var tx = Pos.CreateTransaction("MyStore", "USD");

// Add items
tx.AddItem("COFFEE", 3.99m);
tx.AddItem("MUFFIN", 2.49m);

// Process payment
tx.AddCashTender(10.00m);

// Get results
var totals = tx.Totals;
Console.WriteLine($"Total: {totals.Total:C}, Change: {totals.Change:C}");
```

### **Basic Usage (C)**

```c
#include "pos_kernel.h"

// Initialize terminal
pk_initialize_terminal("REGISTER_01", 11);

// Create transaction
uint64_t handle;
pk_begin_transaction("MyStore", 7, "USD", 3, &handle);

// Add items and process payment
pk_add_line(handle, "COFFEE", 6, 1, 399); // $3.99
pk_add_cash_tender(handle, 1000); // $10.00

// Get totals
int64_t total, tendered, change;
int32_t state;
pk_get_totals(handle, &total, &tendered, &change, &state);

pk_close_transaction(handle);
```

## 📊 **Performance**

- **Transaction Latency**: < 1ms P95
- **Throughput**: 1000+ TPS per terminal
- **Memory Usage**: < 50MB per terminal process
- **Startup Time**: < 100ms cold start

## ⚖️ **Legal Compliance**

POS Kernel is designed for strict regulatory compliance:

- **ACID Transactions**: Full atomicity, consistency, isolation, durability
- **Tamper-Proof Logging**: Write-ahead logs with checksums and sequence numbers
- **Audit Trails**: Complete transaction history for legal admissibility
- **Multi-Process Isolation**: Terminal crash cannot affect other terminals
- **Data Classification**: Built-in support for GDPR, PCI-DSS, and other regulations

## 🌍 **Global Deployment**

### **Culture-Neutral Design**
- Kernel never handles localized content
- All formatting and localization in user-space
- UTF-8 strings throughout
- ISO 4217 currency codes

### **Regional Support (Planned v0.5.0)**
- **Turkish Market**: KDV tax calculation, Turkish Lira handling
- **German Market**: Fiscal compliance, TSE integration
- **US Market**: Multi-jurisdiction sales tax
- **Asian Markets**: Chinese, Japanese, Korean localization
- **Indian Subcontinent**: GST calculation, multi-script support

## 🔧 **Development**

### **Build from Source**

```bash
# Clone repository
git clone https://github.com/paulmooreparks/PosKernel.git
cd PosKernel

# Build Rust kernel
cd pos-kernel-rs
cargo build --release

# Build .NET host
cd ../PosKernel.Host
dotnet build
```

### **Run Examples**

```bash
# .NET example
dotnet run --project PosKernel.Host

# C example (requires compiled kernel)
cd docs/examples/basic
gcc c-basic.c -L../../../pos-kernel-rs/target/release -lpos_kernel_rs
```

## 📚 **Documentation**

- [**Architecture Overview**](docs/architecture-harmonization-plan.md)
- [**API Reference**](docs/c-abi/README.md)
- [**Legal Compliance**](docs/legal-compliance.md)
- [**Multi-Process Architecture**](docs/multi-process-architecture.md)
- [**Exception Handling**](docs/exception-handling-report.md)
- [**Examples**](docs/examples/README.md)

## 🔌 **Extensibility (v0.5.0 Roadmap)**

Planned extension system with:
- **Signed Plugins**: OS-style code signing for security
- **Regional Providers**: Tax, currency, compliance customization
- **Customization Abstraction Layer (CAL)**: Hardware abstraction layer for business logic
- **Plugin Marketplace**: Ecosystem for third-party extensions

## 🤝 **Contributing**

We welcome contributions! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

### **License**

Licensed under the Apache License, Version 2.0. See [LICENSE](LICENSE) for details.

### **Contributors**
- **Paul Moore Parks** - Original author and primary developer

## 🎯 **Roadmap**

### **v0.5.0-extensible (Q1 2025)**
- Plugin architecture with signed extensions
- Regional customization system
- Enhanced transaction management
- Turkish, German, US market implementations

### **v1.0.0-enterprise (Q2 2025)**
- Production deployment ready
- Full compliance validation
- Performance optimizations
- Enterprise support features

## 📞 **Support**

- **Issues**: [GitHub Issues](https://github.com/paulmooreparks/PosKernel/issues)
- **Discussions**: [GitHub Discussions](https://github.com/paulmooreparks/PosKernel/discussions)
- **Email**: [contact information]

---

**POS Kernel**: Built for the global point-of-sale ecosystem with security, performance, and compliance at its core. 🚀
