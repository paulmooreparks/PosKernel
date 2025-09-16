# Running POS Kernel with Domain Extensions

## 🎯 **Architecture Overview**

POS Kernel now uses **pure domain extensions** that run as separate processes and communicate with clients via named pipes. This provides:

- ✅ **Process isolation** - Extensions can crash without affecting clients
- ✅ **Language flexibility** - Extensions can be written in any language
- ✅ **Clean interfaces** - No coupling between AI demo and restaurant logic
- ✅ **Real data** - AI demo uses actual SQLite product catalog

## 🚀 **Quick Start Guide**

### **Step 1: Start the Restaurant Extension Service**

```bash
# Terminal 1 - Start restaurant domain extension
cd PosKernel.Extensions.Restaurant
dotnet run
```

The service will:
- Initialize SQLite database with coffee shop products
- Start named pipe server: `poskernel-restaurant-extension`
- Wait for client connections

### **Step 2: Run the AI Demo**

```bash
# Terminal 2 - Start AI demo (connects to extension)
cd PosKernel.AI
dotnet run
```

The AI demo will:
- Connect to restaurant extension via named pipe
- Load real product data from SQLite
- Demonstrate AI-driven sales scenarios

## 📋 **What You'll See**

### **Restaurant Extension Output:**
```
POS Kernel Restaurant Extension v0.4.0
Pure domain service - no demo coupling
info: Program[0]
      Restaurant extension starting up...
info: Program[0]
      Database integrity verified: data/catalog/restaurant_catalog.db
info: RestaurantExtensionService[0]
      Restaurant extension service started on pipe: poskernel-restaurant-extension
dbug: RestaurantExtensionService[0]
      Waiting for client connection...
```

### **AI Demo Output:**
```
🤖 POS Kernel AI Integration Demo v0.4.0
Now with Real Domain Extensions!

═══════════════════════════════════════════
🤖 AI-Powered Coffee Shop Sales Assistant
   Using Real Restaurant Domain Extension
═══════════════════════════════════════════

🎭 Customer Interaction Simulation
Customer: "I'd like a large coffee please"
🤖 AI: "Great choice! One Large Coffee for $3.99"
🤖 AI: "This has high caffeine content - perfect for your energy needs!"
🤖 AI: "Would you like to add a Blueberry Muffin? They pair perfectly together!"
```

## 🔧 **Architecture Benefits Demonstrated**

### **1. Process Isolation**
- **Test**: Stop the restaurant extension while AI demo is running
- **Result**: AI demo handles connection failure gracefully
- **Benefit**: Extension crashes don't kill client applications

### **2. Clean Interfaces**
- **AI Demo**: Uses generic `IProductCatalogService` interface
- **Restaurant Extension**: Provides restaurant-specific product logic
- **Connection**: Clean JSON-RPC over named pipes
- **Benefit**: AI demo works with any domain extension

### **3. Real Data Integration**
- **Database**: SQLite with proper schema and relationships
- **Products**: Coffee shop catalog with allergens, customizations
- **Business Logic**: Time-based pricing, menu scheduling
- **Benefit**: Production-ready data handling

### **4. Language Independence**
- **Extension**: C# with SQLite (could be Python, Go, etc.)
- **Communication**: Standard JSON-RPC protocol
- **Interface**: Language-agnostic named pipes
- **Benefit**: Use best language for each domain

## 🐛 **Troubleshooting**

### **"Unable to connect to restaurant extension service"**
- Ensure restaurant extension is running first
- Check that named pipe `poskernel-restaurant-extension` is created
- On Linux/macOS, named pipes work differently - use Unix domain sockets

### **"Database file not found"**
- Ensure you're running from the correct directory
- Check that `data/catalog/` directory exists
- Run the PowerShell script: `./scripts/init-restaurant-db.ps1`

### **"No products returned"**
- Check restaurant extension logs for database errors
- Verify SQLite database has been populated with sample data
- Check JSON-RPC request/response logs

## 🎯 **Next Steps**

This foundation enables:

1. **Additional Domain Extensions**
   - Pharmacy extension (Python + NDC API)
   - Retail extension (Go + SQL Server)
   - Custom business domains

2. **Service Architecture**
   - POS Kernel service that coordinates extensions
   - Multi-terminal support
   - Centralized transaction management

3. **Production Features**
   - Health monitoring and auto-restart
   - Load balancing across extension instances
   - Enterprise configuration management

The architecture is now **production-ready** and demonstrates how POS Kernel can support real business domains with proper process isolation! 🏪
