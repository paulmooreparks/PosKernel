-- Singapore Kopitiam Modifications
-- Culture-specific modification definitions for Singapore kopitiam stores
-- These modifications extend the generic NRF framework with Singapore cultural context

-- Clear any existing Singapore-specific modifications
DELETE FROM modification_category_items WHERE category_id IN ('DRINK_CHOICE', 'SUGAR_LEVEL', 'ICE_LEVEL');
DELETE FROM modification_availability WHERE parent_sku LIKE 'MOD_KOPI%' OR parent_sku LIKE 'MOD_TEH%' OR parent_sku LIKE 'TSET%';
DELETE FROM set_meal_components;
DELETE FROM modification_categories WHERE category_id IN ('DRINK_CHOICE', 'SUGAR_LEVEL', 'ICE_LEVEL');
DELETE FROM product_modifications WHERE modification_id LIKE 'MOD_KOPI%' OR modification_id LIKE 'MOD_TEH%' OR modification_id LIKE '%_SUGAR' OR modification_id LIKE '%EGGS%';

-- SINGAPORE KOPITIAM MODIFICATION CATEGORIES
INSERT OR REPLACE INTO modification_categories (category_id, name, description, selection_type, min_selections, max_selections) VALUES
('DRINK_CHOICE', 'Drink Choice', 'Choose your beverage for set meals', 'SINGLE', 1, 1),
('SUGAR_LEVEL', 'Sugar Level', 'Sugar customization for drinks', 'SINGLE', 0, 1),
('ICE_LEVEL', 'Ice Level', 'Ice customization for drinks', 'SINGLE', 0, 1);

-- SINGAPORE-SPECIFIC DRINK MODIFICATIONS
INSERT OR REPLACE INTO product_modifications (modification_id, name, description, modification_type, price_adjustment_type, base_price_cents) VALUES
-- Traditional Singapore drink choices (culture-specific)
('MOD_KOPI', 'Kopi', 'Coffee with condensed milk', 'INGREDIENT', 'FREE', 0),
('MOD_KOPI_C', 'Kopi C', 'Coffee with evaporated milk', 'INGREDIENT', 'FREE', 0),
('MOD_KOPI_O', 'Kopi O', 'Black coffee with sugar', 'INGREDIENT', 'FREE', 0),
('MOD_TEH', 'Teh', 'Tea with condensed milk', 'INGREDIENT', 'FREE', 0),
('MOD_TEH_C', 'Teh C', 'Tea with evaporated milk', 'INGREDIENT', 'FREE', 0),
('MOD_TEH_O', 'Teh O', 'Black tea with sugar', 'INGREDIENT', 'FREE', 0),

-- Singapore sugar level terminology (culture-specific Hokkien terms)
('MOD_NO_SUGAR', 'No Sugar (Kosong)', 'No sugar added', 'PREPARATION', 'FREE', 0),
('MOD_LESS_SUGAR', 'Less Sugar (Siew Dai)', 'Less sugar than normal', 'PREPARATION', 'FREE', 0),
('MOD_EXTRA_SUGAR', 'Extra Sugar (Ga Dai)', 'More sugar than normal', 'PREPARATION', 'FREE', 0),

-- Singapore ice level terminology (culture-specific Hokkien terms)
('MOD_ICED', 'Iced (Peng)', 'Served cold with ice', 'PREPARATION', 'FREE', 0),
('MOD_HOT', 'Hot', 'Served hot', 'PREPARATION', 'FREE', 0),
('MOD_WARM', 'Warm (Pua Sio)', 'Served warm', 'PREPARATION', 'FREE', 0),

-- Singapore set meal automatic inclusions
('MOD_HALF_BOILED_EGGS', '2 Half-boiled Eggs', 'Two soft-boiled eggs', 'INCLUDED', 'FREE', 0),
('MOD_KAYA_TOAST_SIDE', 'Kaya Toast', 'Traditional kaya toast as side', 'INGREDIENT', 'FREE', 0),
('MOD_THICK_TOAST_SIDE', 'Thick Toast', 'Thick toast as side', 'INGREDIENT', 'FREE', 0);

-- Link Singapore modifications to categories
INSERT OR REPLACE INTO modification_category_items (category_id, modification_id, sort_order, is_default) VALUES
-- Singapore drink choices
('DRINK_CHOICE', 'MOD_KOPI', 1, FALSE),
('DRINK_CHOICE', 'MOD_KOPI_C', 2, FALSE), 
('DRINK_CHOICE', 'MOD_KOPI_O', 3, FALSE),
('DRINK_CHOICE', 'MOD_TEH', 4, TRUE), -- Default choice for Singapore
('DRINK_CHOICE', 'MOD_TEH_C', 5, FALSE),
('DRINK_CHOICE', 'MOD_TEH_O', 6, FALSE),

-- Singapore sugar levels
('SUGAR_LEVEL', 'MOD_NO_SUGAR', 1, FALSE),
('SUGAR_LEVEL', 'MOD_LESS_SUGAR', 2, FALSE),
('SUGAR_LEVEL', 'MOD_EXTRA_SUGAR', 3, FALSE),

-- Singapore ice levels
('ICE_LEVEL', 'MOD_ICED', 1, FALSE),
('ICE_LEVEL', 'MOD_HOT', 2, TRUE), -- Default hot
('ICE_LEVEL', 'MOD_WARM', 3, FALSE);

-- SINGAPORE HIERARCHICAL MODIFICATIONS
-- Sugar can be added to any Singapore drink choice (culture-specific business rules)
INSERT OR REPLACE INTO modification_availability (availability_id, parent_sku, parent_type, modification_id, max_quantity, is_default) VALUES
-- Sugar modifications available on Singapore drink modifications
('AVAIL_KOPI_SUGAR_1', 'MOD_KOPI', 'MODIFICATION', 'MOD_NO_SUGAR', 1, FALSE),
('AVAIL_KOPI_SUGAR_2', 'MOD_KOPI', 'MODIFICATION', 'MOD_LESS_SUGAR', 1, FALSE),
('AVAIL_KOPI_SUGAR_3', 'MOD_KOPI', 'MODIFICATION', 'MOD_EXTRA_SUGAR', 1, FALSE),

('AVAIL_KOPI_C_SUGAR_1', 'MOD_KOPI_C', 'MODIFICATION', 'MOD_NO_SUGAR', 1, FALSE),
('AVAIL_KOPI_C_SUGAR_2', 'MOD_KOPI_C', 'MODIFICATION', 'MOD_LESS_SUGAR', 1, FALSE),
('AVAIL_KOPI_C_SUGAR_3', 'MOD_KOPI_C', 'MODIFICATION', 'MOD_EXTRA_SUGAR', 1, FALSE),

('AVAIL_KOPI_O_SUGAR_1', 'MOD_KOPI_O', 'MODIFICATION', 'MOD_NO_SUGAR', 1, FALSE),
('AVAIL_KOPI_O_SUGAR_2', 'MOD_KOPI_O', 'MODIFICATION', 'MOD_LESS_SUGAR', 1, FALSE),
('AVAIL_KOPI_O_SUGAR_3', 'MOD_KOPI_O', 'MODIFICATION', 'MOD_EXTRA_SUGAR', 1, FALSE),

('AVAIL_TEH_SUGAR_1', 'MOD_TEH', 'MODIFICATION', 'MOD_NO_SUGAR', 1, FALSE),
('AVAIL_TEH_SUGAR_2', 'MOD_TEH', 'MODIFICATION', 'MOD_LESS_SUGAR', 1, FALSE),
('AVAIL_TEH_SUGAR_3', 'MOD_TEH', 'MODIFICATION', 'MOD_EXTRA_SUGAR', 1, FALSE),

('AVAIL_TEH_C_SUGAR_1', 'MOD_TEH_C', 'MODIFICATION', 'MOD_NO_SUGAR', 1, FALSE),
('AVAIL_TEH_C_SUGAR_2', 'MOD_TEH_C', 'MODIFICATION', 'MOD_LESS_SUGAR', 1, FALSE),
('AVAIL_TEH_C_SUGAR_3', 'MOD_TEH_C', 'MODIFICATION', 'MOD_EXTRA_SUGAR', 1, FALSE),

('AVAIL_TEH_O_SUGAR_1', 'MOD_TEH_O', 'MODIFICATION', 'MOD_NO_SUGAR', 1, FALSE),
('AVAIL_TEH_O_SUGAR_2', 'MOD_TEH_O', 'MODIFICATION', 'MOD_LESS_SUGAR', 1, FALSE),
('AVAIL_TEH_O_SUGAR_3', 'MOD_TEH_O', 'MODIFICATION', 'MOD_EXTRA_SUGAR', 1, FALSE);

-- SINGAPORE SET MEAL DEFINITIONS
-- Traditional Kaya Toast Set components (Singapore kopitiam business rules)
INSERT OR REPLACE INTO set_meal_components (component_id, set_product_sku, component_type, modification_id, is_required, is_automatic, quantity) VALUES
-- Main item (the toast itself)
('SET_KAYA_MAIN', 'TSET001', 'MAIN', NULL, TRUE, TRUE, 1),
-- Required drink choice (customer must select from Singapore drinks)
('SET_KAYA_DRINK', 'TSET001', 'DRINK_CHOICE', NULL, TRUE, FALSE, 1),
-- Automatic inclusion (Singapore kopitiam tradition)
('SET_KAYA_EGGS', 'TSET001', 'INCLUDED', 'MOD_HALF_BOILED_EGGS', FALSE, TRUE, 1);

-- Make Singapore drink choices available for toast sets
INSERT OR REPLACE INTO modification_availability (availability_id, parent_sku, parent_type, modification_id, max_quantity, is_required) VALUES
('AVAIL_TSET001_KOPI', 'TSET001', 'PRODUCT', 'MOD_KOPI', 1, FALSE),
('AVAIL_TSET001_KOPI_C', 'TSET001', 'PRODUCT', 'MOD_KOPI_C', 1, FALSE),
('AVAIL_TSET001_KOPI_O', 'TSET001', 'PRODUCT', 'MOD_KOPI_O', 1, FALSE),
('AVAIL_TSET001_TEH', 'TSET001', 'PRODUCT', 'MOD_TEH', 1, FALSE),
('AVAIL_TSET001_TEH_C', 'TSET001', 'PRODUCT', 'MOD_TEH_C', 1, FALSE),
('AVAIL_TSET001_TEH_O', 'TSET001', 'PRODUCT', 'MOD_TEH_O', 1, FALSE),
-- Automatic inclusions for Singapore sets
('AVAIL_TSET001_EGGS', 'TSET001', 'PRODUCT', 'MOD_HALF_BOILED_EGGS', 1, FALSE);

-- USAGE: This file should be loaded AFTER the NRF framework and Singapore product catalog
-- It provides Singapore-specific cultural modifications that work with the generic NRF structure
