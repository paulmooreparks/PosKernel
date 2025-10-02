-- Restaurant Extension Schema
-- Compatible with RestaurantExtensionService
-- Based on Starbucks-inspired coffee shop menu

-- Categories table
CREATE TABLE IF NOT EXISTS categories (
    id VARCHAR(50) PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    description TEXT,
    display_order INT DEFAULT 0,
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Products table
CREATE TABLE IF NOT EXISTS products (
    sku VARCHAR(50) PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    description TEXT,
    category_id VARCHAR(50) NOT NULL,
    base_price_cents INTEGER NOT NULL,        -- Price in cents (e.g. 350 = $3.50)
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (category_id) REFERENCES categories(id)
);

-- Indexes for performance
CREATE INDEX IF NOT EXISTS idx_products_category ON products(category_id);
CREATE INDEX IF NOT EXISTS idx_products_active ON products(is_active);
CREATE INDEX IF NOT EXISTS idx_products_search ON products(name, description);

-- NRF-COMPLIANT HIERARCHICAL MODIFICATIONS SYSTEM
-- Based on industry standards for product modifications and set meals

-- Product modifications catalog (NRF-style modifier definitions)
CREATE TABLE IF NOT EXISTS product_modifications (
    modification_id VARCHAR(50) PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    description TEXT,
    modification_type VARCHAR(20) NOT NULL, -- 'INGREDIENT', 'SIZE', 'PREPARATION', 'EXTRA', 'INCLUDED'
    price_adjustment_type VARCHAR(20) NOT NULL, -- 'FIXED', 'PERCENTAGE', 'FREE'
    base_price_cents INT NOT NULL DEFAULT 0,
    is_automatic BOOLEAN DEFAULT FALSE, -- For items like half-boiled eggs in sets
    display_order INT DEFAULT 0,
    is_active BOOLEAN DEFAULT TRUE,
    created_date TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_date TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Which modifications are available for which products/other modifications (hierarchical)
CREATE TABLE IF NOT EXISTS modification_availability (
    availability_id VARCHAR(50) PRIMARY KEY,
    parent_sku VARCHAR(50) NOT NULL, -- Product SKU or modification_id
    parent_type VARCHAR(20) NOT NULL, -- 'PRODUCT' or 'MODIFICATION'
    modification_id VARCHAR(50) NOT NULL,
    max_quantity INT DEFAULT 1,
    is_default BOOLEAN DEFAULT FALSE,
    is_required BOOLEAN DEFAULT FALSE,
    sort_order INT DEFAULT 0,
    is_active BOOLEAN DEFAULT TRUE,
    FOREIGN KEY (modification_id) REFERENCES product_modifications(modification_id)
);

-- Set meal definitions with included/optional modifications
CREATE TABLE IF NOT EXISTS set_meal_components (
    component_id VARCHAR(50) PRIMARY KEY,
    set_product_sku VARCHAR(50) NOT NULL,
    component_type VARCHAR(20) NOT NULL, -- 'MAIN', 'DRINK_CHOICE', 'SIDE_CHOICE', 'INCLUDED'
    modification_id VARCHAR(50), -- NULL for choice categories, set for specific included items
    is_required BOOLEAN DEFAULT FALSE,
    is_automatic BOOLEAN DEFAULT FALSE, -- Automatically added (like eggs in toast sets)
    quantity INT DEFAULT 1,
    price_adjustment_cents INT DEFAULT 0, -- Override for set-specific pricing
    display_order INT DEFAULT 0,
    is_active BOOLEAN DEFAULT TRUE,
    FOREIGN KEY (modification_id) REFERENCES product_modifications(modification_id),
    FOREIGN KEY (set_product_sku) REFERENCES products(sku)
);

-- Modification categories for choice groups (NRF-style categorization)
CREATE TABLE IF NOT EXISTS modification_categories (
    category_id VARCHAR(50) PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    description TEXT,
    selection_type VARCHAR(20) NOT NULL, -- 'SINGLE', 'MULTIPLE', 'OPTIONAL'
    min_selections INT DEFAULT 0,
    max_selections INT DEFAULT 1,
    is_active BOOLEAN DEFAULT TRUE
);

-- Link modifications to categories
CREATE TABLE IF NOT EXISTS modification_category_items (
    category_id VARCHAR(50),
    modification_id VARCHAR(50),
    sort_order INT DEFAULT 0,
    is_default BOOLEAN DEFAULT FALSE,
    PRIMARY KEY (category_id, modification_id),
    FOREIGN KEY (category_id) REFERENCES modification_categories(category_id),
    FOREIGN KEY (modification_id) REFERENCES product_modifications(modification_id)
);

-- Performance indexes for modifications system
CREATE INDEX IF NOT EXISTS idx_modification_availability_parent ON modification_availability(parent_sku, parent_type);
CREATE INDEX IF NOT EXISTS idx_modification_availability_mod ON modification_availability(modification_id);
CREATE INDEX IF NOT EXISTS idx_set_meal_components_set ON set_meal_components(set_product_sku);
CREATE INDEX IF NOT EXISTS idx_modification_categories_active ON modification_categories(is_active);
CREATE INDEX IF NOT EXISTS idx_product_modifications_active ON product_modifications(is_active);
CREATE INDEX IF NOT EXISTS idx_product_modifications_type ON product_modifications(modification_type);
