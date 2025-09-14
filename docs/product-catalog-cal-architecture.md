# Product Catalog CAL Architecture

## 🎯 **Vision: XferLang-First, Business-Agnostic Product Catalogs**

This document outlines POS Kernel's approach to product catalog management using:
1. **XferLang** for configuration and data files (human-readable, structured)
2. **CAL (Customization Abstraction Layer)** providers for different business domains
3. **Kernel-agnostic design** that never imposes business structure assumptions

## 🏗️ **Architecture Overview**

```
┌─────────────────────────────────────────────────────────────────┐
│                    POS Kernel Core                              │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │         IKernelProduct Interface                        │    │
│  │  (Minimal: Identifier, Name, Price, Available)        │    │
│  └─────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                Product Catalog CAL Layer                       │
│  ┌─────────────────┐  ┌──────────────────┐  ┌─────────────────┐ │
│  │   Coffee Shop   │  │   Pharmacy       │  │   Retail        │ │
│  │   Provider      │  │   Provider       │  │   Provider      │ │
│  │                 │  │                 │  │                 │ │
│  │ sku, allergens  │  │ ndc, rx_req     │  │ upc, gtin      │ │
│  │ prep_time       │  │ controlled_sub  │  │ inventory      │ │
│  └─────────────────┘  └──────────────────┘  └─────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                   Data Sources (XferLang)                      │
│  ┌─────────────────┐  ┌──────────────────┐  ┌─────────────────┐ │
│  │ coffee-shop-    │  │ pharmacy-        │  │ retail-         │ │
│  │ products.xfer   │  │ catalog.xfer     │  │ inventory.xfer  │ │
│  └─────────────────┘  └──────────────────┘  └─────────────────┘ │
│  ┌─────────────────┐  ┌──────────────────┐  ┌─────────────────┐ │  
│  │ SQL Database    │  │ REST API         │  │ Legacy CSV      │ │
│  │ via CAL         │  │ via CAL          │  │ via CAL         │ │
│  └─────────────────┘  └──────────────────┘  └─────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
```

## 📋 **Key Design Principles**

### **1. Kernel Minimalism**
The kernel only knows about:
- **Identifier**: String (SKU, UPC, NDC, etc. - format agnostic)
- **Display Name**: For receipts and UI
- **Base Price**: For transaction calculations
- **Availability**: Whether item can be sold

Everything else (allergens, specifications, business rules) is handled by CAL providers.

### **2. XferLang-First Configuration**
- **Human Readable**: Store managers can edit product files
- **Structured**: Better semantics than JSON for business data
- **Type Safe**: Built-in validation and parsing
- **Legacy Support**: JSON compatibility for existing systems

### **3. Business Domain Specialization**
Each CAL provider specializes in a business domain:
- **Restaurant**: Allergens, preparation time, menu seasonality
- **Retail**: UPC/GTIN codes, inventory tracking, variants
- **Pharmacy**: NDC codes, controlled substances, prescriptions
- **Automotive**: Part numbers, compatibility, fitment data

### **4. Multi-Provider Support**
Stores can use multiple providers simultaneously:
- Primary: Enterprise SQL database
- Fallback: Local XferLang file
- Specialty: Cloud API for specific products

## 🔧 **Implementation Example**

### **XferLang Product Definition**
```xfer
# Coffee shop product in XferLang
Catalog:Products = {
    "LATTE_LG" = {
        Name = "Large Latte"
        Description = "16oz latte with steamed milk"
        Category = "hot_beverages"
        BasePrice = 5.49
        IsActive = true
        
        # Business-domain specific attributes
        Specifications = {
            Size = "16oz"
            CaffeineContent = "medium"
            Temperature = "hot"
        }
        Allergens = ["dairy"]
        Customizations = {
            MilkOptions = ["whole", "oat", "almond", "soy"]
            SyrupOptions = ["vanilla", "caramel", "hazelnut"]
            ExtraShots = true
        }
        PopularityRank = 2
    }
}
```

### **CAL Provider Implementation**
```csharp
public class CoffeeShopCatalogProvider : IProductCatalogProvider
{
    public string ProviderId => "coffee_shop";
    public string BusinessDomain => "restaurant";
    
    public async Task<ProductLookupResult?> LookupProductAsync(string identifier, ProductLookupContext context)
    {
        // Load from XferLang file
        var xferProduct = await LoadFromXferFileAsync(identifier);
        
        // Create minimal kernel interface
        var kernelProduct = new KernelProduct
        {
            Identifier = xferProduct.SKU,
            DisplayName = xferProduct.Name,
            BasePrice = xferProduct.BasePrice,
            IsAvailableForSale = xferProduct.IsActive
        };
        
        // Include business-specific attributes
        var businessAttributes = new Dictionary<string, object>
        {
            ["allergens"] = xferProduct.Allergens,
            ["customizations"] = xferProduct.Customizations,
            ["preparation_time"] = xferProduct.PreparationTime,
            ["popularity_rank"] = xferProduct.PopularityRank
        };
        
        return new ProductLookupResult
        {
            KernelProduct = kernelProduct,
            BusinessAttributes = businessAttributes
        };
    }
}
```

## 📁 **File Organization**

### **Configuration Files**
```
config-templates/
├── product-catalog.xfer        # Main CAL provider configuration
├── coffee-shop-catalog.xfer    # Coffee shop specific settings
├── pharmacy-catalog.xfer       # Pharmacy specific settings
└── retail-catalog.xfer         # Retail specific settings
```

### **Data Files**
```
data/catalog/
├── coffee-shop-products.xfer   # Coffee shop product catalog
├── pharmacy-products.xfer      # Pharmacy product catalog
├── retail-inventory.xfer       # Retail inventory
└── legacy/
    ├── products.json          # JSON compatibility
    └── products.csv           # CSV import support
```

### **Provider Code**
```
PosKernel.Providers.Demo/
├── CoffeeShopCatalogProvider.cs    # XferLang-based provider
├── SqlCatalogProvider.cs           # SQL database provider  
├── RestApiCatalogProvider.cs       # Cloud API provider
└── LegacyCsvProvider.cs            # CSV file provider

PosKernel.Abstractions/CAL/Products/
├── IProductCatalogProvider.cs      # Core CAL interfaces
├── ProductLookupResult.cs          # Result structures
└── ProductSearchCriteria.cs        # Search interfaces
```

## 🎯 **Benefits of This Approach**

### **1. Business Domain Flexibility**
- **Coffee Shop**: Focus on allergens, preparation, customizations
- **Pharmacy**: NDC codes, controlled substances, prescriptions
- **Electronics**: GTIN codes, warranties, specifications
- **Fashion**: Sizes, colors, seasons, variants

### **2. Data Source Agnostic** 
- XferLang files for small businesses
- SQL databases for enterprises  
- REST APIs for cloud integration
- Legacy CSV/JSON for migrations

### **3. Human-Readable Configuration**
Store managers can edit XferLang files directly:
```xfer
# Easy to read and modify
"LATTE_LG" = {
    Name = "Large Latte"
    BasePrice = 5.49
    Allergens = ["dairy"]
}
```

### **4. Kernel Simplicity**
The kernel never needs to understand:
- Business-specific product attributes
- Data source formats
- Domain-specific validation rules
- Pricing customizations

### **5. Migration Path**
Easy migration from existing systems:
- Start with JSON compatibility
- Gradually move to XferLang
- Add CAL providers incrementally
- No disruption to existing operations

## 🔄 **Migration from Hard-Coded Catalog**

### **Phase 1: Extract to XferLang**
1. Convert `ProductCatalog.cs` to `coffee-shop-products.xfer`
2. Create `CoffeeShopCatalogProvider` 
3. Update AI module to use CAL interface

### **Phase 2: Add CAL Layer**
1. Implement `IProductCatalogService`
2. Add provider configuration
3. Support multiple data sources

### **Phase 3: Business Domain Expansion**
1. Add pharmacy provider for NDC codes
2. Add retail provider for UPC/GTIN
3. Add automotive provider for part numbers

This architecture positions POS Kernel as a true **business-agnostic platform** that adapts to any industry's product catalog needs while maintaining kernel simplicity and performance.
