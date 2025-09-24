-- Coffee Shop Database Schema
-- Based on Starbucks menu structure
-- Currency-agnostic design with decimal precision

-- Categories table
CREATE TABLE IF NOT EXISTS categories (
    id VARCHAR(50) PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    description TEXT,
    display_order INT DEFAULT 0,
    is_active BOOLEAN DEFAULT TRUE,
    name_localization_key VARCHAR(100),
    description_localization_key VARCHAR(100),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Products table  
CREATE TABLE IF NOT EXISTS products (
    sku VARCHAR(50) PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    description TEXT,
    category_id VARCHAR(50) NOT NULL,
    base_price DECIMAL(15,6) NOT NULL,    -- Currency-flexible decimal precision
    tax_category VARCHAR(20) DEFAULT 'STANDARD',
    requires_preparation BOOLEAN DEFAULT TRUE,
    preparation_time_minutes INT DEFAULT 5,
    popularity_rank INT DEFAULT 99,
    is_active BOOLEAN DEFAULT TRUE,
    name_localization_key VARCHAR(100),
    description_localization_key VARCHAR(100),
    allergens TEXT,                       -- JSON array of allergen codes
    nutritional_info TEXT,               -- JSON nutritional data
    availability_hours TEXT,             -- JSON time restrictions
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (category_id) REFERENCES categories(id)
);

-- Modifications framework
CREATE TABLE IF NOT EXISTS modifications (
    id VARCHAR(50) PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    category VARCHAR(50),                -- 'size', 'milk', 'syrup', 'shots', 'temperature'
    price_adjustment DECIMAL(15,6) DEFAULT 0,
    tax_treatment VARCHAR(20) DEFAULT 'inherit',
    sort_order INT DEFAULT 0,
    is_active BOOLEAN DEFAULT TRUE,
    localization_key VARCHAR(100),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Modification groups (selection rules)
CREATE TABLE IF NOT EXISTS modification_groups (
    id VARCHAR(50) PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    selection_type VARCHAR(20) DEFAULT 'single', -- 'single', 'multiple'
    min_selections INT DEFAULT 0,
    max_selections INT DEFAULT 1,
    is_required BOOLEAN DEFAULT FALSE,
    localization_key VARCHAR(100),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Link modifications to groups
CREATE TABLE IF NOT EXISTS modification_group_items (
    group_id VARCHAR(50),
    modification_id VARCHAR(50),
    sort_order INT DEFAULT 0,
    PRIMARY KEY (group_id, modification_id),
    FOREIGN KEY (group_id) REFERENCES modification_groups(id),
    FOREIGN KEY (modification_id) REFERENCES modifications(id)
);

-- Link products to modification groups
CREATE TABLE IF NOT EXISTS product_modification_groups (
    product_id VARCHAR(50),
    category_id VARCHAR(50),
    modification_group_id VARCHAR(50),
    is_active BOOLEAN DEFAULT TRUE,
    CHECK ((product_id IS NOT NULL) != (category_id IS NOT NULL)), -- XOR constraint
    FOREIGN KEY (modification_group_id) REFERENCES modification_groups(id)
);

-- Multi-language localization
CREATE TABLE IF NOT EXISTS localizations (
    localization_key VARCHAR(100) NOT NULL,
    locale_code VARCHAR(35) NOT NULL,      -- BCP 47 language tags
    text_value TEXT NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (localization_key, locale_code)
);

-- Transaction-level modifications (audit trail)
CREATE TABLE IF NOT EXISTS transaction_line_modifications (
    id VARCHAR(50) PRIMARY KEY,
    transaction_line_id VARCHAR(50) NOT NULL,
    modification_id VARCHAR(50),
    quantity INT DEFAULT 1,
    price_adjustment DECIMAL(15,6),
    tax_treatment VARCHAR(20),
    is_voided BOOLEAN DEFAULT FALSE,
    void_reason TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (modification_id) REFERENCES modifications(id)
);

-- Indexes for performance
CREATE INDEX IF NOT EXISTS idx_products_category ON products(category_id);
CREATE INDEX IF NOT EXISTS idx_products_active ON products(is_active);
CREATE INDEX IF NOT EXISTS idx_products_popularity ON products(popularity_rank);
CREATE INDEX IF NOT EXISTS idx_products_search ON products(name, description);
CREATE INDEX IF NOT EXISTS idx_localizations_lookup ON localizations(localization_key, locale_code);
CREATE INDEX IF NOT EXISTS idx_modifications_category ON modifications(category);
CREATE INDEX IF NOT EXISTS idx_product_modifications ON product_modification_groups(product_id, modification_group_id);
