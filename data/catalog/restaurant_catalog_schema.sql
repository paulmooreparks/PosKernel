-- POS Kernel Product Database Schema
-- SQLite database for restaurant domain extension
-- Optimized for product catalog operations

-- Enable foreign key constraints
PRAGMA foreign_keys = ON;

-- Product categories table
CREATE TABLE categories (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    description TEXT,
    tax_category TEXT NOT NULL DEFAULT 'STANDARD',
    requires_preparation BOOLEAN NOT NULL DEFAULT FALSE,
    average_prep_time_minutes INTEGER DEFAULT 0,
    display_order INTEGER DEFAULT 0,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Product table - core product information
CREATE TABLE products (
    sku TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    description TEXT,
    category_id TEXT NOT NULL,
    base_price_cents INTEGER NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    requires_preparation BOOLEAN NOT NULL DEFAULT FALSE,
    preparation_time_minutes INTEGER DEFAULT 0,
    popularity_rank INTEGER DEFAULT 999,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    FOREIGN KEY (category_id) REFERENCES categories(id)
);

-- Product identifiers - support multiple identifier types
CREATE TABLE product_identifiers (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    product_sku TEXT NOT NULL,
    identifier_type TEXT NOT NULL, -- 'barcode', 'internal_code', 'upc', 'gtin'
    identifier_value TEXT NOT NULL,
    is_primary BOOLEAN NOT NULL DEFAULT FALSE,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    FOREIGN KEY (product_sku) REFERENCES products(sku) ON DELETE CASCADE,
    UNIQUE(identifier_type, identifier_value)
);

-- Product specifications - flexible key-value storage
CREATE TABLE product_specifications (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    product_sku TEXT NOT NULL,
    spec_key TEXT NOT NULL,
    spec_value TEXT NOT NULL,
    spec_type TEXT NOT NULL DEFAULT 'string', -- 'string', 'number', 'boolean', 'json'
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    FOREIGN KEY (product_sku) REFERENCES products(sku) ON DELETE CASCADE,
    UNIQUE(product_sku, spec_key)
);

-- Allergen information
CREATE TABLE allergens (
    id TEXT PRIMARY KEY, -- 'dairy', 'gluten', 'nuts', etc.
    name TEXT NOT NULL,
    description TEXT,
    severity TEXT DEFAULT 'standard', -- 'mild', 'standard', 'severe'
    is_active BOOLEAN NOT NULL DEFAULT TRUE
);

-- Product allergens - many-to-many relationship
CREATE TABLE product_allergens (
    product_sku TEXT NOT NULL,
    allergen_id TEXT NOT NULL,
    contamination_risk TEXT DEFAULT 'direct', -- 'direct', 'cross', 'trace'
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    PRIMARY KEY (product_sku, allergen_id),
    FOREIGN KEY (product_sku) REFERENCES products(sku) ON DELETE CASCADE,
    FOREIGN KEY (allergen_id) REFERENCES allergens(id)
);

-- Nutritional information
CREATE TABLE product_nutrition (
    product_sku TEXT PRIMARY KEY,
    calories INTEGER,
    fat_grams REAL,
    saturated_fat_grams REAL,
    carbohydrates_grams REAL,
    fiber_grams REAL,
    sugar_grams REAL,
    protein_grams REAL,
    sodium_mg INTEGER,
    serving_size_grams INTEGER,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    FOREIGN KEY (product_sku) REFERENCES products(sku) ON DELETE CASCADE
);

-- Product customizations (for coffee drinks, etc.)
CREATE TABLE product_customizations (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    product_sku TEXT NOT NULL,
    customization_type TEXT NOT NULL, -- 'milk_option', 'syrup_option', 'size_option'
    option_value TEXT NOT NULL,
    price_modifier_cents INTEGER DEFAULT 0,
    is_default BOOLEAN NOT NULL DEFAULT FALSE,
    display_order INTEGER DEFAULT 0,
    is_available BOOLEAN NOT NULL DEFAULT TRUE,
    
    FOREIGN KEY (product_sku) REFERENCES products(sku) ON DELETE CASCADE
);

-- Upsell suggestions
CREATE TABLE product_upsells (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    product_sku TEXT NOT NULL,
    suggested_sku TEXT NOT NULL,
    suggestion_type TEXT NOT NULL DEFAULT 'complement', -- 'complement', 'upgrade', 'bundle'
    priority INTEGER DEFAULT 0,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    FOREIGN KEY (product_sku) REFERENCES products(sku) ON DELETE CASCADE,
    FOREIGN KEY (suggested_sku) REFERENCES products(sku) ON DELETE CASCADE,
    UNIQUE(product_sku, suggested_sku)
);

-- Business analytics data
CREATE TABLE product_analytics (
    product_sku TEXT PRIMARY KEY,
    daily_sales_velocity INTEGER DEFAULT 0,
    peak_hours TEXT, -- JSON array of time ranges
    seasonal_trends TEXT, -- JSON object with seasonal data
    customer_ratings REAL,
    last_sold_date DATE,
    total_units_sold INTEGER DEFAULT 0,
    revenue_to_date_cents INTEGER DEFAULT 0,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    FOREIGN KEY (product_sku) REFERENCES products(sku) ON DELETE CASCADE
);

-- Pricing rules (time-based, quantity-based, etc.)
CREATE TABLE pricing_rules (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    description TEXT,
    rule_type TEXT NOT NULL, -- 'time_based', 'quantity_based', 'customer_based'
    discount_type TEXT NOT NULL, -- 'percentage', 'fixed_amount', 'buy_x_get_y'
    discount_value REAL NOT NULL,
    start_date DATE,
    end_date DATE,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    conditions TEXT, -- JSON object with rule conditions
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Products affected by pricing rules
CREATE TABLE pricing_rule_products (
    rule_id TEXT NOT NULL,
    product_sku TEXT NOT NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    PRIMARY KEY (rule_id, product_sku),
    FOREIGN KEY (rule_id) REFERENCES pricing_rules(id) ON DELETE CASCADE,
    FOREIGN KEY (product_sku) REFERENCES products(sku) ON DELETE CASCADE
);

-- Indexes for performance
CREATE INDEX idx_products_category ON products(category_id);
CREATE INDEX idx_products_active ON products(is_active);
CREATE INDEX idx_products_popularity ON products(popularity_rank);
CREATE INDEX idx_product_identifiers_type_value ON product_identifiers(identifier_type, identifier_value);
CREATE INDEX idx_product_specifications_key ON product_specifications(product_sku, spec_key);
CREATE INDEX idx_product_allergens_sku ON product_allergens(product_sku);
CREATE INDEX idx_product_customizations_sku ON product_customizations(product_sku);
CREATE INDEX idx_product_upsells_sku ON product_upsells(product_sku);
CREATE INDEX idx_pricing_rules_active ON pricing_rules(is_active);
CREATE INDEX idx_pricing_rules_dates ON pricing_rules(start_date, end_date);

-- Triggers to update timestamps
CREATE TRIGGER update_categories_timestamp 
    AFTER UPDATE ON categories
    BEGIN
        UPDATE categories SET updated_at = CURRENT_TIMESTAMP WHERE id = NEW.id;
    END;

CREATE TRIGGER update_products_timestamp 
    AFTER UPDATE ON products
    BEGIN
        UPDATE products SET updated_at = CURRENT_TIMESTAMP WHERE sku = NEW.sku;
    END;

CREATE TRIGGER update_pricing_rules_timestamp 
    AFTER UPDATE ON pricing_rules
    BEGIN
        UPDATE pricing_rules SET updated_at = CURRENT_TIMESTAMP WHERE id = NEW.id;
    END;

-- Insert default allergens
INSERT INTO allergens (id, name, description) VALUES
    ('dairy', 'Dairy', 'Contains milk or milk products'),
    ('gluten', 'Gluten', 'Contains wheat, barley, rye, or other gluten sources'),
    ('nuts', 'Tree Nuts', 'Contains almonds, walnuts, pecans, or other tree nuts'),
    ('peanuts', 'Peanuts', 'Contains peanuts or peanut products'),
    ('soy', 'Soy', 'Contains soy or soy products'),
    ('eggs', 'Eggs', 'Contains eggs or egg products'),
    ('fish', 'Fish', 'Contains fish or fish products'),
    ('shellfish', 'Shellfish', 'Contains shellfish or shellfish products'),
    ('sesame', 'Sesame', 'Contains sesame seeds or sesame oil');

-- Insert default categories
INSERT INTO categories (id, name, description, tax_category, requires_preparation, average_prep_time_minutes, display_order) VALUES
    ('hot_beverages', 'Hot Beverages', 'Coffee, tea, and hot drinks', 'BEVERAGE', true, 3, 1),
    ('cold_beverages', 'Cold Beverages', 'Iced drinks, sodas, and juices', 'BEVERAGE', false, 1, 2),
    ('bakery', 'Bakery Items', 'Fresh baked goods and pastries', 'FOOD', false, 0, 3),
    ('breakfast', 'Breakfast', 'Morning meals and breakfast items', 'FOOD', true, 5, 4),
    ('lunch', 'Lunch', 'Sandwiches, salads, and lunch items', 'FOOD', true, 7, 5),
    ('snacks', 'Snacks & Treats', 'Cookies, chips, and quick snacks', 'FOOD', false, 0, 6),
    ('retail', 'Retail Items', 'Coffee beans, mugs, and merchandise', 'RETAIL', false, 0, 7),
    ('gift_cards', 'Gift Cards', 'Electronic and physical gift cards', 'EXEMPT', false, 0, 8);
