-- American Diner Modifications
-- Culture-specific modification definitions for American diner stores
-- These modifications extend the generic NRF framework with American cultural context

-- Clear any existing American-specific modifications
DELETE FROM modification_category_items WHERE category_id IN ('COFFEE_CHOICE', 'CREAM_SUGAR', 'TEMPERATURE_PREF');
DELETE FROM modification_availability WHERE parent_sku LIKE 'MOD_COFFEE%' OR parent_sku LIKE 'COMBO%';
DELETE FROM set_meal_components WHERE set_product_sku LIKE 'COMBO%';
DELETE FROM modification_categories WHERE category_id IN ('COFFEE_CHOICE', 'CREAM_SUGAR', 'TEMPERATURE_PREF');
DELETE FROM product_modifications WHERE modification_id LIKE 'MOD_COFFEE%' OR modification_id LIKE '%_CREAM' OR modification_id LIKE '%HASH_BROWNS%';

-- AMERICAN DINER MODIFICATION CATEGORIES
INSERT OR REPLACE INTO modification_categories (category_id, name, description, selection_type, min_selections, max_selections) VALUES
('COFFEE_CHOICE', 'Coffee Selection', 'Choose your coffee for combo meals', 'SINGLE', 1, 1),
('CREAM_SUGAR', 'Cream & Sugar', 'Coffee customization options', 'MULTIPLE', 0, 3),
('TEMPERATURE_PREF', 'Temperature', 'Hot or iced beverage', 'SINGLE', 0, 1);

-- AMERICAN-SPECIFIC DRINK MODIFICATIONS
INSERT OR REPLACE INTO product_modifications (modification_id, name, description, modification_type, price_adjustment_type, base_price_cents) VALUES
-- American coffee choices (culture-specific)
('MOD_COFFEE_REGULAR', 'Regular Coffee', 'House blend coffee', 'INGREDIENT', 'FREE', 0),
('MOD_COFFEE_DECAF', 'Decaf Coffee', 'Decaffeinated house blend', 'INGREDIENT', 'FREE', 0),
('MOD_COFFEE_DARK_ROAST', 'Dark Roast', 'Bold dark roast coffee', 'INGREDIENT', 'FREE', 0),

-- American cream & sugar options (culture-specific terminology)
('MOD_CREAM', 'Cream', 'Half & half cream', 'PREPARATION', 'FREE', 0),
('MOD_SUGAR_PACKETS', 'Sugar', 'White sugar packets', 'PREPARATION', 'FREE', 0),
('MOD_ARTIFICIAL_SWEETENER', 'Artificial Sweetener', 'Sugar substitute packets', 'PREPARATION', 'FREE', 0),

-- American temperature options
('MOD_HOT_BEVERAGE', 'Hot', 'Served hot', 'PREPARATION', 'FREE', 0),
('MOD_ICED_BEVERAGE', 'Iced', 'Served over ice', 'PREPARATION', 'FREE', 0),

-- American combo meal automatic inclusions
('MOD_HASH_BROWNS', 'Hash Browns', 'Golden fried potato hash', 'INCLUDED', 'FREE', 0),
('MOD_TOAST_WHITE', 'White Toast', 'Two slices white bread toast', 'INCLUDED', 'FREE', 0),
('MOD_TOAST_WHEAT', 'Wheat Toast', 'Two slices wheat bread toast', 'INCLUDED', 'FREE', 0);

-- Link American modifications to categories
INSERT OR REPLACE INTO modification_category_items (category_id, modification_id, sort_order, is_default) VALUES
-- American coffee choices
('COFFEE_CHOICE', 'MOD_COFFEE_REGULAR', 1, TRUE), -- Default choice for American diners
('COFFEE_CHOICE', 'MOD_COFFEE_DECAF', 2, FALSE),
('COFFEE_CHOICE', 'MOD_COFFEE_DARK_ROAST', 3, FALSE),

-- American cream & sugar options
('CREAM_SUGAR', 'MOD_CREAM', 1, FALSE),
('CREAM_SUGAR', 'MOD_SUGAR_PACKETS', 2, FALSE),
('CREAM_SUGAR', 'MOD_ARTIFICIAL_SWEETENER', 3, FALSE),

-- American temperature preferences
('TEMPERATURE_PREF', 'MOD_HOT_BEVERAGE', 1, TRUE), -- Default hot
('TEMPERATURE_PREF', 'MOD_ICED_BEVERAGE', 2, FALSE);

-- AMERICAN HIERARCHICAL MODIFICATIONS
-- Cream & sugar can be added to any American coffee choice (American business rules)
INSERT OR REPLACE INTO modification_availability (availability_id, parent_sku, parent_type, modification_id, max_quantity, is_default) VALUES
-- Cream & sugar available on American coffee modifications
('AVAIL_REGULAR_CREAM', 'MOD_COFFEE_REGULAR', 'MODIFICATION', 'MOD_CREAM', 1, FALSE),
('AVAIL_REGULAR_SUGAR', 'MOD_COFFEE_REGULAR', 'MODIFICATION', 'MOD_SUGAR_PACKETS', 3, FALSE),
('AVAIL_REGULAR_SWEETENER', 'MOD_COFFEE_REGULAR', 'MODIFICATION', 'MOD_ARTIFICIAL_SWEETENER', 3, FALSE),

('AVAIL_DECAF_CREAM', 'MOD_COFFEE_DECAF', 'MODIFICATION', 'MOD_CREAM', 1, FALSE),
('AVAIL_DECAF_SUGAR', 'MOD_COFFEE_DECAF', 'MODIFICATION', 'MOD_SUGAR_PACKETS', 3, FALSE),
('AVAIL_DECAF_SWEETENER', 'MOD_COFFEE_DECAF', 'MODIFICATION', 'MOD_ARTIFICIAL_SWEETENER', 3, FALSE),

('AVAIL_DARK_CREAM', 'MOD_COFFEE_DARK_ROAST', 'MODIFICATION', 'MOD_CREAM', 1, FALSE),
('AVAIL_DARK_SUGAR', 'MOD_COFFEE_DARK_ROAST', 'MODIFICATION', 'MOD_SUGAR_PACKETS', 3, FALSE),
('AVAIL_DARK_SWEETENER', 'MOD_COFFEE_DARK_ROAST', 'MODIFICATION', 'MOD_ARTIFICIAL_SWEETENER', 3, FALSE);

-- AMERICAN COMBO MEAL DEFINITIONS
-- Example: Breakfast Combo (American diner business rules)
INSERT OR REPLACE INTO set_meal_components (component_id, set_product_sku, component_type, modification_id, is_required, is_automatic, quantity) VALUES
-- Main item (eggs & bacon)
('COMBO_BREAKFAST_MAIN', 'COMBO001', 'MAIN', NULL, TRUE, TRUE, 1),
-- Required coffee choice (customer must select from American coffees)
('COMBO_BREAKFAST_COFFEE', 'COMBO001', 'COFFEE_CHOICE', NULL, TRUE, FALSE, 1),
-- Automatic inclusion (American diner tradition)
('COMBO_BREAKFAST_HASH', 'COMBO001', 'INCLUDED', 'MOD_HASH_BROWNS', FALSE, TRUE, 1),
-- Choice inclusion (customer chooses toast type)
('COMBO_BREAKFAST_TOAST', 'COMBO001', 'CHOICE', NULL, TRUE, FALSE, 1);

-- Make American coffee choices available for combo meals
INSERT OR REPLACE INTO modification_availability (availability_id, parent_sku, parent_type, modification_id, max_quantity, is_required) VALUES
('AVAIL_COMBO001_REGULAR', 'COMBO001', 'PRODUCT', 'MOD_COFFEE_REGULAR', 1, FALSE),
('AVAIL_COMBO001_DECAF', 'COMBO001', 'PRODUCT', 'MOD_COFFEE_DECAF', 1, FALSE),
('AVAIL_COMBO001_DARK', 'COMBO001', 'PRODUCT', 'MOD_COFFEE_DARK_ROAST', 1, FALSE),
-- Toast choices for American combo
('AVAIL_COMBO001_WHITE_TOAST', 'COMBO001', 'PRODUCT', 'MOD_TOAST_WHITE', 1, FALSE),
('AVAIL_COMBO001_WHEAT_TOAST', 'COMBO001', 'PRODUCT', 'MOD_TOAST_WHEAT', 1, FALSE),
-- Automatic inclusions for American combos
('AVAIL_COMBO001_HASH', 'COMBO001', 'PRODUCT', 'MOD_HASH_BROWNS', 1, FALSE);

-- USAGE: This file demonstrates how the same NRF framework supports different cultures
-- Compare with singapore_kopitiam_modifications.sql to see cultural differences:
-- - Singapore: "Kopi C Kosong" vs American: "Regular Coffee, Cream, No Sugar" 
-- - Singapore: "Peng" vs American: "Iced"
-- - Singapore: "Half-boiled eggs" vs American: "Hash browns"
-- 
-- The framework is the same, but the cultural implementations differ completely
