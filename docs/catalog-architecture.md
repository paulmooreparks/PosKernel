# Product Catalog Architecture

## 🏗️ **Overview**

This document outlines the proper architecture for product data storage and management in POS Kernel, addressing the current issue where product data is hard-coded in the AI module.

## ❌ **Current Problem**

The product catalog is currently hard-coded in `PosKernel.AI/Catalog/ProductCatalog.cs`, which creates several architectural issues:

- **Wrong Layer**: Product data belongs in core business logic, not the AI module
- **No Persistence**: Data is lost on restart
- **No External Integration**: Cannot sync with external inventory systems
- **Not Scalable**: Difficult to update or manage in production

## ✅ **Proper Architecture**

### **Data Storage Hierarchy**

```
1. Persistent Storage Layer (Bottom)
   ├── pos_kernel_data/catalog/products.db     (SQLite primary storage)
   ├── pos_kernel_data/catalog/catalog.wal     (Write-ahead log)
   └── data/catalog/products.json              (Import/export format)

2. Rust Kernel Core
   ├── pos-kernel-rs/src/catalog/
   │   ├── product.rs          (Core Product struct)
   │   ├── catalog_store.rs    (Persistent storage)
   │   └── pricing.rs          (Price calculation logic)

3. C ABI Layer  
   ├── pk_get_product()        (Get product by SKU)
   ├── pk_catalog_search()     (Search products)
   └── pk_get_price()          (Get effective price)

4. .NET Host Layer
   ├── PosKernel.Core/Catalog/
   │   ├── Product.cs                    (Domain object)
   │   ├── IProductRepository.cs         (Data access interface)
   │   └── IProductCatalogService.cs     (Business logic interface)

5. Application Layer (Top)
   ├── PosKernel.AI/           (AI consumes catalog via service)
   ├── PosKernel.Host/         (Transaction processing)
   └── External Integrations   (Inventory management systems)
```

### **Data Flow**

```
External System → JSON Import → Rust Kernel → SQLite DB → WAL → C ABI → .NET Host → AI Module
     ↑                                                                           ↓
   Export API ←─────────────────────────────────────────────────────── Application Layer
```

## 📁 **File Organization**

### **Data Files**
```
data/
├── catalog/
│   ├── products.json           # Master product catalog (import/export)
│   ├── categories.json         # Product categories and tax rules
│   └── pricing-rules.json      # Promotional pricing and discounts
│
pos_kernel_data/
├── catalog/
│   ├── products.db             # SQLite database (runtime storage)
│   ├── catalog.wal             # Write-ahead log for ACID compliance
│   └── schema_version.txt      # Database schema version tracking
```

### **Code Organization**
```
pos-kernel-rs/src/
├── catalog/
│   ├── mod.rs                  # Module declarations
│   ├── product.rs              # Core Product struct and validation
│   ├── catalog_store.rs        # SQLite storage implementation
│   ├── pricing.rs              # Price calculation and rules engine
│   ├── search.rs               # Product search and filtering
│   └── import_export.rs        # JSON import/export functionality

PosKernel.Core/
├── Catalog/
│   ├── Product.cs              # .NET domain object
│   ├── ProductCatalogInterfaces.cs  # Repository and service interfaces
│   └── ProductCatalogService.cs     # Business logic implementation

PosKernel.Host/
├── Catalog/
│   ├── ProductRepository.cs    # Data access implementation
│   └── CatalogServiceExtensions.cs  # Dependency injection setup
```

## 🔧 **Implementation Strategy**

### **Phase 1: Core Data Layer**
1. **Rust Implementation**
   - Define `Product` struct in Rust
   - Implement SQLite storage with WAL
   - Add C ABI functions for product access

2. **JSON Schema** 
   - Define standardized product catalog format
   - Support categories, pricing rules, metadata
   - Enable import/export functionality

### **Phase 2: .NET Integration**
1. **Repository Pattern**
   - Implement `IProductRepository` interface
   - Add caching layer for performance
   - Support async operations

2. **Service Layer**
   - Implement `IProductCatalogService`
   - Add business logic (pricing, upselling)
   - Integration with external systems

### **Phase 3: AI Integration**
1. **Remove Hard-coded Catalog**
   - Refactor AI module to consume `IProductCatalogService`
   - Update AI prompts to work with dynamic catalog
   - Add product-aware AI recommendations

## 📊 **Data Schema**

### **Product Entity**
```json
{
  "sku": "COFFEE_LG",
  "name": "Large Coffee", 
  "description": "Classic drip coffee, 16oz",
  "category": "hot-beverages",
  "base_price": 3.99,
  "tax_category": "BEVERAGE",
  "requires_preparation": true,
  "is_active": true,
  "metadata": {
    "size": "16oz",
    "caffeine_content": "high",
    "allergens": [],
    "calories": 10,
    "popularity_rank": 1
  }
}
```

### **Category Entity**
```json
{
  "id": "hot-beverages",
  "name": "Hot Beverages",
  "description": "Coffee, tea, and hot drinks", 
  "tax_category": "BEVERAGE",
  "requires_preparation": true
}
```

## 🔒 **Security & Compliance**

### **ACID Compliance**
- All catalog changes logged to WAL before commit
- Transactional updates for price changes
- Rollback capability for failed updates

### **Audit Trail**  
- Track all product catalog modifications
- Log price changes with timestamps
- Maintain change history for compliance

### **Access Control**
- Repository pattern enables authorization layers
- Role-based access to catalog management
- Separate read/write permissions

## 🎯 **Benefits of This Architecture**

### **Separation of Concerns**
- **Rust Core**: Performance-critical storage and validation
- **.NET Layer**: Object-oriented business logic
- **AI Module**: Consumes catalog via clean interfaces

### **Scalability**
- Database storage supports millions of products
- Caching layer for high-performance access
- External system integration capabilities

### **Maintainability**
- JSON import/export for easy data management
- Standardized interfaces across layers
- Proper dependency injection and testing

### **Compliance**
- ACID transactions for financial accuracy
- Complete audit trails for regulatory requirements
- Data integrity guarantees

## 🚀 **Migration Plan**

### **Step 1: Create Core Infrastructure** 
- Implement Rust catalog storage
- Add C ABI functions
- Create .NET repository interfaces

### **Step 2: Migrate Data**
- Export current hard-coded products to JSON
- Import into new SQLite database
- Validate data integrity

### **Step 3: Update AI Module**
- Replace `ProductCatalog` class with service injection
- Update AI prompts to use dynamic catalog
- Add product-aware recommendation logic

### **Step 4: Add External Integration**
- Implement import/export APIs
- Add real-time inventory sync
- Enable external catalog management

This architecture ensures product data is properly managed at the core business logic level while enabling the AI module to consume catalog data through clean, well-defined interfaces.
