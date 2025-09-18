# POS Kernel

**A Rust point-of-sale transaction kernel with global extensibility and AI integration**

[![License: Apache 2.0](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)
[![Build Status](https://img.shields.io/badge/Build-Passing-green.svg)](#)
[![Version](https://img.shields.io/badge/Version-v0.5.0--ai--complete-green.svg)](#)

## Overview

POS Kernel is a culture-neutral transaction processing kernel designed for global deployment. Built with Rust for security and performance, it provides a C ABI that can be consumed by any programming language. Phase 1 Complete: Full AI integration with real business data through domain extensions.

### Phase 1 Complete: AI Integration + Terminal.Gui Interface

**Working Demo**: Interactive AI-powered POS system with natural language processing
```bash
cd PosKernel.AI && dotnet run
# Result: Terminal.GUI interface with AI assistant using real restaurant database
```

### Key Features

- **Security-First Design**: ACID-compliant transactions with tamper-proof logging
- **Global Ready**: Culture-neutral kernel with user-space localization
- **Compliance Goal**: Designed to support audit trails and regulatory requirements
- **High Performance**: Multi-process architecture with sub-millisecond transaction processing
- **Extensible**: Domain extension architecture with Restaurant extension implemented
- **AI-Powered**: Natural language processing with OpenAI integration (Phase 1 Complete)
- **Professional UI**: Terminal.Gui-based interface with real-time debugging
- **Enterprise Grade**: Handle-based API design for robust resource management

## Architecture

### Current v0.5.0 Stack (AI Integration Complete)

```
AI Application Layer
  Interactive AI Assistant, Natural Language Processing
  Terminal.Gui Interface, Real-time Chat + Receipt
  Multi-language Personalities (Singaporean, American, etc.)
                              ↓
AI Integration Layer
  Model Context Protocol, Prompt Engineering
  Context Management, Response Validation
  OpenAI GPT-4o Integration, Conversation History
                              ↓
Domain Extension Layer
  Restaurant Extension, SQLite Product Catalog
  Product Recommendations, Natural Language to Transaction
  Real Business Data, Fuzzy Matching
                              ↓
.NET Host Layer
  Terminal.Gui UI, Resource Management
  Exception Translation, Object-Oriented Interface
  Cross-platform Support, Debug Logging Integration
                              ↓
FFI Boundary
  Win32-style C ABI, Handle-Based Operations
  Memory Safety, Cross-Language Support
                              ↓
Rust Kernel Core
  ACID Transactions, Write-Ahead Logging
  Multi-Process Safe, Culture-Neutral Processing
  Exception Safety, High-Performance Operations
```

## Quick Start

### AI-Powered POS Demo
```bash
# Clone repository
git clone https://github.com/paulmooreparks/PosKernel.git
cd PosKernel

# Set OpenAI API key (optional - has fallback)
export OPENAI_API_KEY="your-key-here"

# Run interactive demo
cd PosKernel.AI
dotnet run

# Alternative modes
dotnet run --mock          # Mock AI mode
dotnet run --debug         # Enhanced debugging
```

**Demo Experience**:
- Terminal UI: Split-pane interface with chat, receipt, and debug logs
- Natural Language Orders: "I'll have two coffees and a blueberry muffin"
- Real Business Data: SQLite restaurant catalog with products, prices, allergens
- AI Assistant: Handles typos, makes suggestions, processes payments
- Multi-language Support: Choose from 6 regional personalities

### Core Transaction Processing
```csharp
// Basic kernel usage (works with or without AI)
using PosKernel.Host;

using var transaction = Pos.CreateTransaction("STORE_001", "USD");
transaction.AddItem("COFFEE_LARGE", 3.99m);
transaction.AddItem("MUFFIN_BLUEBERRY", 2.49m);
transaction.AddCashTender(10.00m);

Console.WriteLine($"Total: {transaction.Total}");
Console.WriteLine($"Change: {transaction.ChangeDue}");
// Total: $6.48
// Change: $3.52
```

## AI Integration Features

### Phase 1 Completed (v0.5.0)

#### Natural Language Processing
- Conversational commerce: "Can I get a large coffee?" maps to database items
- Fuzzy matching: "capu puccino" resolves to "Cappuccino" 
- Typo handling: "blubery mufin" resolves to "Blueberry Muffin"
- Context awareness: remembers conversation history and current order

#### Real Business Data Integration
- SQLite database: 12 restaurant products with categories, allergens, specifications  
- Dynamic pricing: actual prices from database
- Product intelligence: AI understands ingredients, allergens, preparation options
- Business rules: handles upselling, combo suggestions, dietary restrictions

#### Terminal.Gui Professional Interface
- Split layout: Chat (60%) | Receipt (40%)
- Collapsible debug panels: AI prompt context and system logs
- Real-time updates: Receipt updates as AI processes orders
- Status bar: Order status, navigation hints, system messages
- Full keyboard navigation: Tab between sections, scroll with arrows
- Mouse support: Click to expand/collapse, scroll with mouse


## Global Deployment Ready

### Multi-Region Support
```csharp
// Regional configuration examples
var singaporeConfig = new StoreConfig {
    StoreName = "Uncle's Traditional Kopitiam",
    Currency = "SGD",
    StoreType = StoreType.Traditional,
    PersonalityType = PersonalityType.SingaporeanKopitiamUncle
};

var americaConfig = new StoreConfig {
    StoreName = "Downtown Coffee Co.",
    Currency = "USD", 
    StoreType = StoreType.Modern,
    PersonalityType = PersonalityType.AmericanBarista
};
```

### Designed Features
- ACID transaction logging: every operation durably logged
- Audit trails: complete transaction history with timestamps
- Multi-currency: supports 180+ world currencies
- Regional customization: tax rules, receipt formats, regulatory adaptations
- Data privacy: GDPR-compliant data handling patterns

## Development Phases

### Phase 1 Complete: AI + Domain Extensions (v0.5.0)
- Restaurant domain extension with SQLite database
- AI natural language processing with OpenAI integration  
- Terminal.Gui professional interface
- Multi-cultural personality system
- Real business data integration
- Production-ready error handling and logging

### Phase 2: Service Architecture (v0.6.0)
- Service-based architecture: HTTP/gRPC/WebSocket APIs
- Cross-platform service: Windows, macOS, Linux support
- Multi-client support: .NET, Python, Node.js, Web clients
- Service discovery: auto-discovery and load balancing
- Enterprise security: authentication, authorization, encryption

### Phase 3: Production Extensions (v1.0.0)
- Additional domain extensions: retail, pharmacy, restaurant chains
- Advanced AI features: voice commands, computer vision, predictive analytics
- Enterprise integration: ERP systems, accounting software, analytics platforms
- Global compliance: region-specific legal requirements and certifications

## Development

### Project Structure
```
PosKernel/
├── pos-kernel-rs/               # Rust kernel core
├── PosKernel.Host/              # .NET C# wrapper  
├── PosKernel.AI/                # AI integration layer
│   ├── UI/Terminal/             # Terminal.Gui interface
│   ├── Core/                    # AI orchestration
│   ├── Services/                # AI services & personality
│   └── Prompts/                 # Multi-language prompts
├── PosKernel.Extensions.Restaurant/  # Domain extension
├── data/catalog/                # SQLite databases
└── docs/                        # Architecture documentation
```

### Key Technologies
- Rust: Kernel core, ACID logging, multi-process architecture
- .NET 9: Host layer, AI integration, Terminal.Gui interface  
- SQLite: Product catalogs, business data persistence
- OpenAI: GPT-4o for natural language processing
- Terminal.Gui: Cross-platform professional terminal interface

### Build Commands
```bash
# Full build (Rust + .NET)
dotnet build

# Run tests
cd pos-kernel-rs && cargo test
dotnet test

# Development with hot reload
dotnet watch --project PosKernel.AI
```

## Success Criteria Achievement

### AI Integration Success
- Natural language accuracy: handles common variations and typos
- Real data integration: SQLite restaurant catalog fully integrated
- Multi-language support: 6 personality types implemented
- Production quality: professional Terminal.Gui interface
- Error resilience: graceful fallbacks for AI service unavailability

### Business Validation
- Conversation flow: natural order-taking experience
- Product intelligence: AI understands ingredients, prices, options
- Payment processing: complete transaction lifecycle
- Receipt generation: professional receipt formatting
- Multi-cultural: authentic regional language and behavior

### Technical Excellence
- Zero warnings: all code compiles warning-free
- Exception safety: no exception swallowing, comprehensive error handling
- Performance: maintains kernel performance guarantees
- Extensibility: clean separation between AI, extensions, and kernel

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for development guidelines.

### Current Focus Areas
1. Service architecture: converting to service-based deployment
2. Additional domain extensions: retail, pharmacy, quick-service
3. Advanced AI features: voice commands, computer vision
4. Enterprise integration: ERP, accounting, analytics platforms

## License

Licensed under the Apache License, Version 2.0. See [LICENSE](LICENSE) for details.

---

**POS Kernel v0.5.0**: Production-ready AI-integrated point-of-sale kernel with professional Terminal.Gui interface and real business data processing.
