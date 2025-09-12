# POS Kernel

A high-performance, Rust-first Point of Sale (POS) kernel with multi-language support via Win32-style C ABI.

## 🚀 Features

- **Rust-First Architecture**: Core business logic implemented in Rust for maximum performance and memory safety
- **Multi-Language Support**: Win32-style C ABI enables wrappers for any programming language
- **Object-Oriented Ready**: Handle-based design maps naturally to OOP patterns
- **Memory Safe**: Zero unsafe operations in client code, comprehensive safety guarantees
- **Mixed-Mode Debugging**: Full debugging support across Rust ↔ .NET boundaries in Visual Studio
- **High Performance**: Optimized for transaction throughput and low latency

## 🏗️ Architecture

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│ Language        │───▶│ C ABI           │───▶│ Rust Core       │
│ Wrappers        │    │ (Win32-style)   │    │ (Business Logic)│
│ (.NET, Python,  │    │                 │    │                 │
│  JavaScript...) │    │                 │    │                 │
└─────────────────┘    └──────────────────┘    └─────────────────┘
```

### Core Design Principles

- **Handle-Based Resource Management**: Opaque handles instead of raw pointers
- **Structured Error Reporting**: Win32-style result codes with detailed error information  
- **Two-Phase String Retrieval**: Buffer sizing followed by data copy (like `GetWindowText`)
- **Explicit Cleanup**: Manual resource lifecycle management prevents leaks
- **Thread Safety**: Safe concurrent access with clear synchronization boundaries

## 📋 Quick Start

### Prerequisites

- [Rust](https://rustup.rs/) (latest stable)
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Visual Studio 2022 (for mixed-mode debugging)

### Build and Run

```bash
# Clone the repository
git clone https://github.com/paulmooreparks/PosKernel.git
cd PosKernel

# Build and run (automatically builds Rust library)
dotnet run --project PosKernel.Host
```

### Example Usage

#### .NET (Object-Oriented API)
```csharp
using PosKernel.Host;

// Automatic resource management with 'using'
using var transaction = Pos.CreateTransaction("Store-001", "USD");

// Add items
transaction.AddItem("COFFEE", 3.99m);
transaction.AddItems("MUFFIN", 2, 2.49m);

// Process payment
transaction.AddCashTender(10.00m);

// Get results
var totals = transaction.Totals;
Console.WriteLine($"Total: {totals.Total:C}, Change: {totals.Change:C}");
```

#### .NET (Static API)
```csharp
var handle = Pos.BeginTransaction("Store-001", "USD");
try 
{
    Pos.AddLine(handle, "ITEM", 1, 9.99m);
    Pos.AddCashTender(handle, 20.00m);
    var totals = Pos.GetTotals(handle);
}
finally 
{
    Pos.CloseTransaction(handle);
}
```

#### C (Direct API)
```c
PkTransactionHandle handle;
pk_begin_transaction("Store-001", 9, "USD", 3, &handle);

pk_add_line(handle, "ITEM", 4, 1, 999);     // $9.99
pk_add_cash_tender(handle, 2000);           // $20.00

int64_t total, tendered, change;
int32_t state;
pk_get_totals(handle, &total, &tendered, &change, &state);

pk_close_transaction(handle);
```

## 📚 Documentation

- **[Architecture Overview](docs/README.md)** - System design and principles
- **[Rust Implementation](docs/rust/README.md)** - Core implementation details  
- **[C ABI Reference](docs/c-abi/README.md)** - Complete API specification
- **[.NET Wrapper](docs/dotnet/README.md)** - .NET integration guide
- **[Examples](docs/examples/README.md)** - Multi-language examples

## 🔧 Development

### Project Structure
```
PosKernel/
├── pos-kernel-rs/          # Rust core implementation (cdylib)
├── PosKernel.Host/         # .NET wrapper and demo
├── docs/                   # Comprehensive documentation
└── [other language wrappers as needed]
```

### Building Components

```bash
# Rust library only
cd pos-kernel-rs && cargo build

# .NET wrapper (automatically builds Rust)
dotnet build PosKernel.Host

# Documentation examples
dotnet run --project docs/examples/basic/dotnet-basic.cs
```

### Testing

```bash
# Rust unit tests
cd pos-kernel-rs && cargo test

# .NET integration tests
dotnet test
```

## 🚀 Performance

- **Transaction Creation**: ~1M ops/sec
- **Line Item Addition**: ~2M ops/sec  
- **Total Calculation**: ~5M ops/sec
- **Memory Usage**: ~200 bytes/transaction + line items

See [Performance Documentation](docs/examples/performance/README.md) for detailed benchmarks.

## 🎯 Roadmap

### Current Features ✅
- [x] Basic transaction lifecycle (create, add items, tender, close)
- [x] Cash tender support
- [x] .NET wrapper with both static and OOP APIs
- [x] Comprehensive documentation
- [x] Mixed-mode debugging support

### Planned Features 🚧
- [ ] Multiple tender types (credit card, check, etc.)
- [ ] Line item modification/deletion
- [ ] Transaction persistence and serialization
- [ ] Python wrapper
- [ ] JavaScript/Node.js wrapper
- [ ] Performance optimizations (lock-free data structures)

## 📄 License

MIT License - see [LICENSE](LICENSE) for details.

## 🤝 Contributing

Contributions welcome! Please read our [Contributing Guidelines](CONTRIBUTING.md) first.

### Language Wrappers Wanted
We're looking for contributors to create wrappers for:
- Python
- JavaScript/Node.js  
- Go
- Java
- C++

The C ABI is stable and well-documented - perfect foundation for additional language bindings!

---

**Built with ❤️ in Rust and C#**
