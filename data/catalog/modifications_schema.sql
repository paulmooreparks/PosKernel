-- General-purpose product modifications and localization schema
-- Works across all store types: kopitiam, coffee shops, grocery stores, etc.

-- Localization support for multi-language stores
-- Simple key-value approach with BCP 47 language tags
CREATE TABLE IF NOT EXISTS localizations (
    localization_key VARCHAR(100) NOT NULL,
    locale_code VARCHAR(35) NOT NULL,        -- BCP 47: 'en-US', 'en-SG', 'zh-Hans-SG', 'ms-SG'
    text_value TEXT NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (localization_key, locale_code)
);

-- Product modifications system
-- General enough for any store: coffee customizations, grocery substitutions, etc.
CREATE TABLE IF NOT EXISTS modifications (
    id VARCHAR(50) PRIMARY KEY,              -- 'no_sugar', 'extra_shot', 'organic_substitute'
    name TEXT NOT NULL,                      -- Default name: 'No Sugar', 'Extra Shot'
    localization_key VARCHAR(100),          -- Optional: links to localizations table
    category VARCHAR(50),                    -- 'sweetness', 'strength', 'dietary', 'size'
    price_adjustment DECIMAL(15,6) DEFAULT 0, -- Currency-flexible, high precision
    tax_treatment TEXT DEFAULT 'inherit',   -- 'inherit', 'exempt', 'standard', 'reduced'
    sort_order INT DEFAULT 0,
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CHECK (tax_treatment IN ('inherit', 'exempt', 'standard', 'reduced'))
);

-- Groups of related modifications
-- Supports single/multiple selection rules
CREATE TABLE IF NOT EXISTS modification_groups (
    id VARCHAR(50) PRIMARY KEY,              -- 'drink_sweetness', 'milk_options', 'dietary_preferences'
    name TEXT NOT NULL,                      -- Default name: 'Sweetness Options'
    localization_key VARCHAR(100),          -- Optional localization
    selection_type TEXT DEFAULT 'single',   -- 'single', 'multiple', 'optional'
    min_selections INT DEFAULT 0,
    max_selections INT DEFAULT 1,
    is_required BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CHECK (selection_type IN ('single', 'multiple', 'optional'))
);

-- Which modifications belong to which groups
CREATE TABLE IF NOT EXISTS modification_group_items (
    group_id VARCHAR(50) NOT NULL,
    modification_id VARCHAR(50) NOT NULL,
    is_default BOOLEAN DEFAULT FALSE,
    PRIMARY KEY (group_id, modification_id),
    FOREIGN KEY (group_id) REFERENCES modification_groups(id) ON DELETE CASCADE,
    FOREIGN KEY (modification_id) REFERENCES modifications(id) ON DELETE CASCADE
);

-- Association of modification groups with products or categories
-- Supports both product-specific and category-wide modifications
CREATE TABLE IF NOT EXISTS product_modification_groups (
    product_id VARCHAR(50),                  -- Optional: specific product (products.sku)
    category_id VARCHAR(50),                 -- Optional: entire category (categories.id)
    modification_group_id VARCHAR(50) NOT NULL,
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (modification_group_id) REFERENCES modification_groups(id) ON DELETE CASCADE,
    -- Ensure exactly one of product_id or category_id is specified
    CHECK ((product_id IS NOT NULL) != (category_id IS NOT NULL))
);

-- Create composite primary key separately to avoid COALESCE issues
CREATE UNIQUE INDEX IF NOT EXISTS idx_product_modification_groups_pk 
ON product_modification_groups(
    CASE WHEN product_id IS NOT NULL THEN 'P:' || product_id ELSE 'C:' || category_id END,
    modification_group_id
);

-- Transaction-level modifications capture
-- This would typically be in the kernel database, but shown here for completeness
CREATE TABLE IF NOT EXISTS transaction_line_modifications (
    id VARCHAR(50) PRIMARY KEY,             -- For individual modification void tracking
    transaction_line_id VARCHAR(50) NOT NULL,
    modification_id VARCHAR(50) NOT NULL,
    quantity INT DEFAULT 1,
    price_adjustment DECIMAL(15,6),         -- Captured at transaction time
    tax_treatment VARCHAR(20),              -- Captured for audit: 'inherit', 'exempt', etc.
    is_voided BOOLEAN DEFAULT FALSE,
    voided_at TIMESTAMP NULL,
    voided_by VARCHAR(100) NULL,            -- User/system that voided
    void_reason TEXT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (modification_id) REFERENCES modifications(id)
);

-- Indexes for performance
CREATE INDEX IF NOT EXISTS idx_localizations_key ON localizations(localization_key);
CREATE INDEX IF NOT EXISTS idx_modifications_category ON modifications(category);
CREATE INDEX IF NOT EXISTS idx_modifications_active ON modifications(is_active);
CREATE INDEX IF NOT EXISTS idx_product_mod_groups_product ON product_modification_groups(product_id);
CREATE INDEX IF NOT EXISTS idx_product_mod_groups_category ON product_modification_groups(category_id);
CREATE INDEX IF NOT EXISTS idx_transaction_line_mods_line ON transaction_line_modifications(transaction_line_id);
CREATE INDEX IF NOT EXISTS idx_transaction_line_mods_not_voided ON transaction_line_modifications(is_voided) WHERE is_voided = FALSE;
