-- Coffee Shop Modifications Data
-- Starbucks-style customizations with realistic pricing

-- Size Modifications
INSERT OR REPLACE INTO modifications (id, name, category, price_adjustment, sort_order) VALUES
('SIZE_TALL', 'Tall (12oz)', 'size', -0.30, 1),      -- Smaller size discount
('SIZE_GRANDE', 'Grande (16oz)', 'size', 0.00, 2),    -- Base size (default)
('SIZE_VENTI', 'Venti (20oz)', 'size', 0.65, 3),     -- Large size upcharge
('SIZE_TRENTA', 'Trenta (30oz)', 'size', 0.85, 4);   -- Extra large (cold drinks only)

-- Milk Alternative Modifications  
INSERT OR REPLACE INTO modifications (id, name, category, price_adjustment, sort_order) VALUES
('MILK_2PERCENT', '2% Milk', 'milk', 0.00, 1),       -- Default milk
('MILK_NONFAT', 'Nonfat Milk', 'milk', 0.00, 2),     -- No charge alternative
('MILK_WHOLE', 'Whole Milk', 'milk', 0.00, 3),       -- No charge alternative
('MILK_HALF_HALF', 'Half & Half', 'milk', 0.00, 4),  -- No charge alternative
('MILK_OAT', 'Oat Milk', 'milk', 0.65, 5),           -- Premium plant milk
('MILK_ALMOND', 'Almond Milk', 'milk', 0.65, 6),     -- Premium plant milk
('MILK_SOY', 'Soy Milk', 'milk', 0.65, 7),           -- Premium plant milk
('MILK_COCONUT', 'Coconut Milk', 'milk', 0.65, 8);   -- Premium plant milk

-- Espresso Shot Modifications
INSERT OR REPLACE INTO modifications (id, name, category, price_adjustment, sort_order) VALUES
('SHOTS_DECAF', 'Decaf', 'shots', 0.00, 1),          -- No charge
('SHOTS_HALF_CAFF', 'Half Caffeine', 'shots', 0.00, 2), -- No charge
('SHOTS_EXTRA', 'Extra Shot', 'shots', 0.75, 3),     -- Add espresso shot
('SHOTS_RISTRETTO', 'Ristretto', 'shots', 0.00, 4),  -- No charge variation
('SHOTS_LONG', 'Long Shot', 'shots', 0.00, 5);       -- No charge variation

-- Syrup Flavor Modifications
INSERT OR REPLACE INTO modifications (id, name, category, price_adjustment, sort_order) VALUES
('SYRUP_VANILLA', 'Vanilla Syrup', 'syrup', 0.65, 1),
('SYRUP_CARAMEL', 'Caramel Syrup', 'syrup', 0.65, 2),
('SYRUP_HAZELNUT', 'Hazelnut Syrup', 'syrup', 0.65, 3),
('SYRUP_CINNAMON', 'Cinnamon Dolce Syrup', 'syrup', 0.65, 4),
('SYRUP_PEPPERMINT', 'Peppermint Syrup', 'syrup', 0.65, 5),
('SYRUP_TOFFEE_NUT', 'Toffee Nut Syrup', 'syrup', 0.65, 6),
('SYRUP_SUGAR_FREE_VANILLA', 'Sugar-Free Vanilla', 'syrup', 0.65, 7),
('SYRUP_SUGAR_FREE_CARAMEL', 'Sugar-Free Caramel', 'syrup', 0.65, 8);

-- Temperature Modifications
INSERT OR REPLACE INTO modifications (id, name, category, price_adjustment, sort_order) VALUES
('TEMP_EXTRA_HOT', 'Extra Hot', 'temperature', 0.00, 1),
('TEMP_HOT', 'Hot', 'temperature', 0.00, 2),          -- Default for hot drinks
('TEMP_WARM', 'Warm', 'temperature', 0.00, 3),
('TEMP_ICED', 'Iced', 'temperature', 0.00, 4);       -- Default for cold drinks

-- Foam/Texture Modifications
INSERT OR REPLACE INTO modifications (id, name, category, price_adjustment, sort_order) VALUES
('FOAM_NO_FOAM', 'No Foam', 'foam', 0.00, 1),
('FOAM_LIGHT', 'Light Foam', 'foam', 0.00, 2),
('FOAM_NORMAL', 'Normal Foam', 'foam', 0.00, 3),     -- Default
('FOAM_EXTRA', 'Extra Foam', 'foam', 0.00, 4),
('FOAM_DRY', 'Dry (Extra Foam)', 'foam', 0.00, 5);

-- Sweetener Modifications
INSERT OR REPLACE INTO modifications (id, name, category, price_adjustment, sort_order) VALUES
('SWEET_SUGAR', 'Sugar', 'sweetener', 0.00, 1),
('SWEET_SPLENDA', 'Splenda', 'sweetener', 0.00, 2),
('SWEET_STEVIA', 'Stevia', 'sweetener', 0.00, 3),
('SWEET_HONEY', 'Honey', 'sweetener', 0.00, 4),
('SWEET_AGAVE', 'Agave', 'sweetener', 0.00, 5),
('SWEET_NONE', 'No Sweetener', 'sweetener', 0.00, 6);

-- Topping Modifications
INSERT OR REPLACE INTO modifications (id, name, category, price_adjustment, sort_order) VALUES
('TOP_WHIP', 'Whipped Cream', 'topping', 0.50, 1),
('TOP_NO_WHIP', 'No Whipped Cream', 'topping', 0.00, 2),
('TOP_CINNAMON', 'Cinnamon Powder', 'topping', 0.00, 3),
('TOP_COCOA', 'Cocoa Powder', 'topping', 0.00, 4),
('TOP_NUTMEG', 'Nutmeg', 'topping', 0.00, 5);

-- Create Modification Groups
INSERT OR REPLACE INTO modification_groups (id, name, selection_type, min_selections, max_selections, is_required) VALUES
('SIZE_OPTIONS', 'Size Options', 'single', 0, 1, FALSE),
('MILK_OPTIONS', 'Milk Options', 'single', 0, 1, FALSE),
('SHOT_OPTIONS', 'Espresso Options', 'single', 0, 1, FALSE),
('SYRUP_OPTIONS', 'Flavor Syrups', 'multiple', 0, 3, FALSE),
('TEMP_OPTIONS', 'Temperature', 'single', 0, 1, FALSE),
('FOAM_OPTIONS', 'Foam Options', 'single', 0, 1, FALSE),
('SWEET_OPTIONS', 'Sweetener Options', 'single', 0, 1, FALSE),
('TOPPING_OPTIONS', 'Toppings', 'multiple', 0, 2, FALSE);

-- Link modifications to groups
INSERT OR REPLACE INTO modification_group_items (group_id, modification_id, sort_order) VALUES
-- Size options
('SIZE_OPTIONS', 'SIZE_TALL', 1),
('SIZE_OPTIONS', 'SIZE_GRANDE', 2),
('SIZE_OPTIONS', 'SIZE_VENTI', 3),
('SIZE_OPTIONS', 'SIZE_TRENTA', 4),

-- Milk options
('MILK_OPTIONS', 'MILK_2PERCENT', 1),
('MILK_OPTIONS', 'MILK_NONFAT', 2),
('MILK_OPTIONS', 'MILK_WHOLE', 3),
('MILK_OPTIONS', 'MILK_HALF_HALF', 4),
('MILK_OPTIONS', 'MILK_OAT', 5),
('MILK_OPTIONS', 'MILK_ALMOND', 6),
('MILK_OPTIONS', 'MILK_SOY', 7),
('MILK_OPTIONS', 'MILK_COCONUT', 8),

-- Espresso options
('SHOT_OPTIONS', 'SHOTS_DECAF', 1),
('SHOT_OPTIONS', 'SHOTS_HALF_CAFF', 2),
('SHOT_OPTIONS', 'SHOTS_EXTRA', 3),
('SHOT_OPTIONS', 'SHOTS_RISTRETTO', 4),
('SHOT_OPTIONS', 'SHOTS_LONG', 5),

-- Syrup options
('SYRUP_OPTIONS', 'SYRUP_VANILLA', 1),
('SYRUP_OPTIONS', 'SYRUP_CARAMEL', 2),
('SYRUP_OPTIONS', 'SYRUP_HAZELNUT', 3),
('SYRUP_OPTIONS', 'SYRUP_CINNAMON', 4),
('SYRUP_OPTIONS', 'SYRUP_PEPPERMINT', 5),
('SYRUP_OPTIONS', 'SYRUP_TOFFEE_NUT', 6),
('SYRUP_OPTIONS', 'SYRUP_SUGAR_FREE_VANILLA', 7),
('SYRUP_OPTIONS', 'SYRUP_SUGAR_FREE_CARAMEL', 8),

-- Temperature options
('TEMP_OPTIONS', 'TEMP_EXTRA_HOT', 1),
('TEMP_OPTIONS', 'TEMP_HOT', 2),
('TEMP_OPTIONS', 'TEMP_WARM', 3),
('TEMP_OPTIONS', 'TEMP_ICED', 4),

-- Foam options
('FOAM_OPTIONS', 'FOAM_NO_FOAM', 1),
('FOAM_OPTIONS', 'FOAM_LIGHT', 2),
('FOAM_OPTIONS', 'FOAM_NORMAL', 3),
('FOAM_OPTIONS', 'FOAM_EXTRA', 4),
('FOAM_OPTIONS', 'FOAM_DRY', 5),

-- Sweetener options
('SWEET_OPTIONS', 'SWEET_SUGAR', 1),
('SWEET_OPTIONS', 'SWEET_SPLENDA', 2),
('SWEET_OPTIONS', 'SWEET_STEVIA', 3),
('SWEET_OPTIONS', 'SWEET_HONEY', 4),
('SWEET_OPTIONS', 'SWEET_AGAVE', 5),
('SWEET_OPTIONS', 'SWEET_NONE', 6),

-- Topping options
('TOPPING_OPTIONS', 'TOP_WHIP', 1),
('TOPPING_OPTIONS', 'TOP_NO_WHIP', 2),
('TOPPING_OPTIONS', 'TOP_CINNAMON', 3),
('TOPPING_OPTIONS', 'TOP_COCOA', 4),
('TOPPING_OPTIONS', 'TOP_NUTMEG', 5);

-- Link products to modification groups (coffee drinks get most options)
INSERT OR REPLACE INTO product_modification_groups (product_id, modification_group_id, is_active) VALUES
-- Hot coffee drinks with all options
('LATTE', 'SIZE_OPTIONS', TRUE),
('LATTE', 'MILK_OPTIONS', TRUE),
('LATTE', 'SHOT_OPTIONS', TRUE),
('LATTE', 'SYRUP_OPTIONS', TRUE),
('LATTE', 'TEMP_OPTIONS', TRUE),
('LATTE', 'FOAM_OPTIONS', TRUE),
('LATTE', 'SWEET_OPTIONS', TRUE),

('CAPPUCCINO', 'SIZE_OPTIONS', TRUE),
('CAPPUCCINO', 'MILK_OPTIONS', TRUE),
('CAPPUCCINO', 'SHOT_OPTIONS', TRUE),
('CAPPUCCINO', 'SYRUP_OPTIONS', TRUE),
('CAPPUCCINO', 'TEMP_OPTIONS', TRUE),
('CAPPUCCINO', 'FOAM_OPTIONS', TRUE),
('CAPPUCCINO', 'SWEET_OPTIONS', TRUE),

('AMERICANO', 'SIZE_OPTIONS', TRUE),
('AMERICANO', 'SHOT_OPTIONS', TRUE),
('AMERICANO', 'SYRUP_OPTIONS', TRUE),
('AMERICANO', 'TEMP_OPTIONS', TRUE),
('AMERICANO', 'SWEET_OPTIONS', TRUE),

('MACCHIATO', 'SIZE_OPTIONS', TRUE),
('MACCHIATO', 'MILK_OPTIONS', TRUE),
('MACCHIATO', 'SHOT_OPTIONS', TRUE),
('MACCHIATO', 'SYRUP_OPTIONS', TRUE),
('MACCHIATO', 'TEMP_OPTIONS', TRUE),
('MACCHIATO', 'FOAM_OPTIONS', TRUE),

('MOCHA', 'SIZE_OPTIONS', TRUE),
('MOCHA', 'MILK_OPTIONS', TRUE),
('MOCHA', 'SHOT_OPTIONS', TRUE),
('MOCHA', 'TEMP_OPTIONS', TRUE),
('MOCHA', 'TOPPING_OPTIONS', TRUE),

('WHITE_MOCHA', 'SIZE_OPTIONS', TRUE),
('WHITE_MOCHA', 'MILK_OPTIONS', TRUE),
('WHITE_MOCHA', 'SHOT_OPTIONS', TRUE),
('WHITE_MOCHA', 'TEMP_OPTIONS', TRUE),
('WHITE_MOCHA', 'TOPPING_OPTIONS', TRUE),

('CARAMEL_MACCHIATO', 'SIZE_OPTIONS', TRUE),
('CARAMEL_MACCHIATO', 'MILK_OPTIONS', TRUE),
('CARAMEL_MACCHIATO', 'SHOT_OPTIONS', TRUE),
('CARAMEL_MACCHIATO', 'TEMP_OPTIONS', TRUE),

('FLAT_WHITE', 'MILK_OPTIONS', TRUE),
('FLAT_WHITE', 'SHOT_OPTIONS', TRUE),
('FLAT_WHITE', 'SYRUP_OPTIONS', TRUE),
('FLAT_WHITE', 'TEMP_OPTIONS', TRUE),

-- Iced coffee drinks
('ICED_LATTE', 'SIZE_OPTIONS', TRUE),
('ICED_LATTE', 'MILK_OPTIONS', TRUE),
('ICED_LATTE', 'SHOT_OPTIONS', TRUE),
('ICED_LATTE', 'SYRUP_OPTIONS', TRUE),
('ICED_LATTE', 'SWEET_OPTIONS', TRUE),

('ICED_AMERICANO', 'SIZE_OPTIONS', TRUE),
('ICED_AMERICANO', 'SHOT_OPTIONS', TRUE),
('ICED_AMERICANO', 'SYRUP_OPTIONS', TRUE),
('ICED_AMERICANO', 'SWEET_OPTIONS', TRUE),

('ICED_CARAMEL_MACCHIATO', 'SIZE_OPTIONS', TRUE),
('ICED_CARAMEL_MACCHIATO', 'MILK_OPTIONS', TRUE),
('ICED_CARAMEL_MACCHIATO', 'SHOT_OPTIONS', TRUE),

('COLD_BREW', 'SIZE_OPTIONS', TRUE),
('COLD_BREW', 'SYRUP_OPTIONS', TRUE),
('COLD_BREW', 'SWEET_OPTIONS', TRUE),

-- Tea drinks
('CHAI_LATTE', 'SIZE_OPTIONS', TRUE),
('CHAI_LATTE', 'MILK_OPTIONS', TRUE),
('CHAI_LATTE', 'TEMP_OPTIONS', TRUE),
('CHAI_LATTE', 'SWEET_OPTIONS', TRUE),

('MATCHA_LATTE', 'SIZE_OPTIONS', TRUE),
('MATCHA_LATTE', 'MILK_OPTIONS', TRUE),
('MATCHA_LATTE', 'TEMP_OPTIONS', TRUE),
('MATCHA_LATTE', 'SWEET_OPTIONS', TRUE),

-- Frappuccinos  
('CARAMEL_FRAPPUCCINO', 'SIZE_OPTIONS', TRUE),
('CARAMEL_FRAPPUCCINO', 'MILK_OPTIONS', TRUE),
('CARAMEL_FRAPPUCCINO', 'TOPPING_OPTIONS', TRUE),

('MOCHA_FRAPPUCCINO', 'SIZE_OPTIONS', TRUE),
('MOCHA_FRAPPUCCINO', 'MILK_OPTIONS', TRUE),
('MOCHA_FRAPPUCCINO', 'TOPPING_OPTIONS', TRUE),

('VANILLA_FRAPPUCCINO', 'SIZE_OPTIONS', TRUE),
('VANILLA_FRAPPUCCINO', 'MILK_OPTIONS', TRUE),
('VANILLA_FRAPPUCCINO', 'TOPPING_OPTIONS', TRUE),

-- Hot chocolate
('HOT_CHOCOLATE', 'SIZE_OPTIONS', TRUE),
('HOT_CHOCOLATE', 'MILK_OPTIONS', TRUE),
('HOT_CHOCOLATE', 'TEMP_OPTIONS', TRUE),
('HOT_CHOCOLATE', 'TOPPING_OPTIONS', TRUE),
('HOT_CHOCOLATE', 'SWEET_OPTIONS', TRUE);
