# Restaurant Extension Catalog Architecture

## Four C's Compliance: Culture-Neutral Design

This directory demonstrates proper separation between **culture-neutral framework** and **culture-specific implementations**.

### Architecture Overview

```
Store Config → Determines → Culture + Currency + Business Rules
     ↓
Culture-Specific Files → Extend → Generic NRF Framework
     ↓  
Runtime System → Uses → Store-Appropriate Data
```

## File Organization

### 🏛️ Culture-Neutral Framework
- `restaurant_catalog_schema.sql` - Generic database schema (works for any culture)
- `nrf_modifications_framework.sql` - Generic NRF modification framework

### 🌏 Culture-Specific Implementations
- `singapore_kopitiam_catalog_data.sql` - Singapore products & SGD pricing
- `singapore_kopitiam_modifications.sql` - Singapore modifications ("Kopi C Kosong", "Peng")
- `american_diner_modifications.sql` - American modifications ("Regular Coffee, Cream, Sugar")

## Loading Sequence

**Correct Order:**
1. Load schema: `restaurant_catalog_schema.sql`
2. Load NRF framework: `nrf_modifications_framework.sql` 
3. Load culture-specific products: `singapore_kopitiam_catalog_data.sql`
4. Load culture-specific modifications: `singapore_kopitiam_modifications.sql`

## Cultural Differences Supported

### Singapore Kopitiam
- **Currency**: SGD (Singapore Dollars) - 2 decimal places
- **Language**: Singlish/Hokkien terms ("Kosong", "Siew Dai", "Peng", "Pua Sio")
- **Products**: Kopi, Teh, Kaya Toast, Half-boiled eggs
- **Business Model**: Set meals with automatic egg inclusions

### American Diner  
- **Currency**: USD (US Dollars) - 2 decimal places
- **Language**: American English ("Cream", "Sugar packets", "Hash browns")
- **Products**: Regular Coffee, Decaf, Hash Browns, Toast
- **Business Model**: Combo meals with automatic hash brown inclusions

## Extension Pattern for New Cultures

To add a French Café:

1. **Create culture-specific catalog**: `french_cafe_catalog_data.sql`
   ```sql
   -- French products with EUR pricing
   ('CAFE001', 'Café', 'Café traditionnel', 'BOISSONS', 280), -- €2.80
   ```

2. **Create culture-specific modifications**: `french_cafe_modifications.sql`
   ```sql
   -- French terminology
   ('MOD_SANS_SUCRE', 'Sans Sucre', 'Pas de sucre ajouté', 'PREPARATION', 'GRATUIT', 0),
   ('MOD_AVEC_LAIT', 'Avec Lait', 'Avec du lait', 'PREPARATION', 'GRATUIT', 0),
   ```

3. **Configure store settings**:
   ```json
   {
     "storeType": "FrenchCafe",
     "currency": "EUR",
     "culture": "fr-FR",
     "catalogFiles": ["french_cafe_catalog_data.sql", "french_cafe_modifications.sql"]
   }
   ```

## Architectural Benefits

### ✅ Culture Neutrality
- **Framework**: Generic NRF structure works for any culture
- **Implementation**: Each culture provides its own terminology and business rules
- **Kernel**: Rust kernel remains completely culture-neutral

### ✅ Currency Flexibility  
- **Singapore**: SGD pricing (340 cents = S$3.40)
- **American**: USD pricing (295 cents = $2.95) 
- **French**: EUR pricing (280 cents = €2.80)
- **Japanese**: JPY pricing (350 cents = ¥350) - no decimal places

### ✅ Communication Adaptability
- **Singapore**: "Kopi C Kosong" (Hokkien/Singlish)
- **American**: "Regular Coffee, No Sugar" (English)
- **French**: "Café Sans Sucre" (French)
- **Framework**: Supports any terminology

### ✅ Configuration Flexibility
- **Each store**: Defines its own business rules
- **Framework**: Supports any combination logic
- **Runtime**: Loads appropriate culture based on store configuration

## Anti-Pattern Warning

❌ **DON'T** create files like:
- `generic_restaurant_data.sql` with Singapore-specific content
- `restaurant_modifications.sql` with hardcoded "Kopi" terminology

✅ **DO** create files like:
- `nrf_modifications_framework.sql` with truly generic framework
- `singapore_kopitiam_modifications.sql` with culture-specific content
- Clear naming that indicates cultural scope

This architecture enables the POS Kernel to support any culture while maintaining clean separation between framework and implementation.
