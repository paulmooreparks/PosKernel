-- NRF-Compliant Product Modifications Schema
-- Culture-neutral hierarchical modification framework
-- This schema can be used by any store type regardless of culture or currency

-- Clear existing modification data (culture-neutral)
DELETE FROM modification_category_items;
DELETE FROM modification_availability;
DELETE FROM set_meal_components;
DELETE FROM modification_categories;
DELETE FROM product_modifications;

-- NOTE: This schema does NOT include products or categories
-- Those are culture-specific and belong in store-specific catalog files

-- NRF-COMPLIANT MODIFICATION SYSTEM (Culture-Neutral Framework)

-- Modification categories (NRF-style choice groups) - Generic framework
INSERT OR REPLACE INTO modification_categories (category_id, name, description, selection_type, min_selections, max_selections) VALUES
-- Generic beverage customization categories
('BEVERAGE_CHOICE', 'Beverage Selection', 'Choose beverage for combination meals', 'SINGLE', 1, 1),
('SIDE_CHOICE', 'Side Selection', 'Choose side dish for combination meals', 'SINGLE', 0, 1),
('SWEETNESS_LEVEL', 'Sweetness Level', 'Sweetness customization for beverages', 'SINGLE', 0, 1),
('TEMPERATURE', 'Temperature', 'Temperature customization for beverages', 'SINGLE', 0, 1),
('INCLUDED_ITEMS', 'Included Items', 'Items automatically included with combinations', 'MULTIPLE', 0, 10),

-- Generic food customization categories
('SIZE_OPTIONS', 'Size Options', 'Portion size selections', 'SINGLE', 0, 1),
('COOKING_STYLE', 'Cooking Style', 'Preparation method options', 'SINGLE', 0, 1),
('DIETARY_OPTIONS', 'Dietary Options', 'Dietary restriction accommodations', 'MULTIPLE', 0, 5);

-- Generic modification types (culture-neutral modification framework)
INSERT OR REPLACE INTO product_modifications (modification_id, name, description, modification_type, price_adjustment_type, base_price_cents) VALUES
-- Generic size modifications
('MOD_SIZE_SMALL', 'Small', 'Smaller portion size', 'SIZE', 'PERCENTAGE', -15),
('MOD_SIZE_REGULAR', 'Regular', 'Standard portion size', 'SIZE', 'FREE', 0),
('MOD_SIZE_LARGE', 'Large', 'Larger portion size', 'SIZE', 'PERCENTAGE', 20),

-- Generic sweetness levels (culture-neutral)
('MOD_NO_SWEETENER', 'No Sweetener', 'No sweetening agent added', 'PREPARATION', 'FREE', 0),
('MOD_REDUCED_SWEETENER', 'Reduced Sweetener', 'Less sweetening than standard', 'PREPARATION', 'FREE', 0),
('MOD_EXTRA_SWEETENER', 'Extra Sweetener', 'More sweetening than standard', 'PREPARATION', 'FREE', 0),

-- Generic temperature options (culture-neutral)
('MOD_COLD', 'Cold', 'Served cold', 'PREPARATION', 'FREE', 0),
('MOD_HOT', 'Hot', 'Served hot', 'PREPARATION', 'FREE', 0),
('MOD_ROOM_TEMP', 'Room Temperature', 'Served at room temperature', 'PREPARATION', 'FREE', 0),

-- Generic dietary accommodations
('MOD_VEGAN', 'Vegan', 'No animal products', 'DIETARY', 'FREE', 0),
('MOD_GLUTEN_FREE', 'Gluten Free', 'No gluten-containing ingredients', 'DIETARY', 'FIXED', 50),
('MOD_LACTOSE_FREE', 'Lactose Free', 'No lactose-containing ingredients', 'DIETARY', 'FREE', 0);

-- Link generic modifications to categories (culture-neutral mappings)
INSERT OR REPLACE INTO modification_category_items (category_id, modification_id, sort_order, is_default) VALUES
-- Size options
('SIZE_OPTIONS', 'MOD_SIZE_SMALL', 1, FALSE),
('SIZE_OPTIONS', 'MOD_SIZE_REGULAR', 2, TRUE), -- Default size
('SIZE_OPTIONS', 'MOD_SIZE_LARGE', 3, FALSE),

-- Sweetness levels (generic framework)
('SWEETNESS_LEVEL', 'MOD_NO_SWEETENER', 1, FALSE),
('SWEETNESS_LEVEL', 'MOD_REDUCED_SWEETENER', 2, FALSE),
('SWEETNESS_LEVEL', 'MOD_EXTRA_SWEETENER', 3, FALSE),

-- Temperature options
('TEMPERATURE', 'MOD_COLD', 1, FALSE),
('TEMPERATURE', 'MOD_HOT', 2, TRUE), -- Default temperature
('TEMPERATURE', 'MOD_ROOM_TEMP', 3, FALSE),

-- Dietary options
('DIETARY_OPTIONS', 'MOD_VEGAN', 1, FALSE),
('DIETARY_OPTIONS', 'MOD_GLUTEN_FREE', 2, FALSE),
('DIETARY_OPTIONS', 'MOD_LACTOSE_FREE', 3, FALSE);

-- HIERARCHICAL MODIFICATION FRAMEWORK (Culture-Neutral)
-- Generic hierarchical relationships that any culture can use
INSERT OR REPLACE INTO modification_availability (availability_id, parent_sku, parent_type, modification_id, max_quantity, is_default) VALUES
-- Generic rule: Size modifications can have dietary modifications applied
('AVAIL_SMALL_VEGAN', 'MOD_SIZE_SMALL', 'MODIFICATION', 'MOD_VEGAN', 1, FALSE),
('AVAIL_SMALL_GLUTEN_FREE', 'MOD_SIZE_SMALL', 'MODIFICATION', 'MOD_GLUTEN_FREE', 1, FALSE),
('AVAIL_REGULAR_VEGAN', 'MOD_SIZE_REGULAR', 'MODIFICATION', 'MOD_VEGAN', 1, FALSE),
('AVAIL_REGULAR_GLUTEN_FREE', 'MOD_SIZE_REGULAR', 'MODIFICATION', 'MOD_GLUTEN_FREE', 1, FALSE),
('AVAIL_LARGE_VEGAN', 'MOD_SIZE_LARGE', 'MODIFICATION', 'MOD_VEGAN', 1, FALSE),
('AVAIL_LARGE_GLUTEN_FREE', 'MOD_SIZE_LARGE', 'MODIFICATION', 'MOD_GLUTEN_FREE', 1, FALSE);

-- NOTE: Culture-specific modification availability rules should be defined in store-specific files
-- For example: singapore_kopitiam_modifications.sql, american_diner_modifications.sql, etc.

-- USAGE INSTRUCTIONS:
-- 1. Apply this schema to create the NRF modification framework
-- 2. Load culture-specific product catalogs (singapore_kopitiam_catalog_data.sql, etc.)
-- 3. Load culture-specific modification rules (singapore_kopitiam_modifications.sql, etc.)
-- 4. The framework supports hierarchical mod→mod relationships regardless of culture
