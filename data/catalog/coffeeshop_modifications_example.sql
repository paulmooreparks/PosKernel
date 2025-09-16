-- Example: Coffee shop modifications (Starbucks-style)
-- Demonstrates charged modifications with tax implications

-- Coffee shop modifications with pricing
INSERT INTO modifications (id, name, category, price_adjustment, tax_treatment) VALUES
-- Milk alternatives (upcharge)
('oat_milk', 'Oat Milk', 'milk_type', 0.65, 'inherit'),
('almond_milk', 'Almond Milk', 'milk_type', 0.60, 'inherit'),
('soy_milk', 'Soy Milk', 'milk_type', 0.60, 'inherit'),
('coconut_milk', 'Coconut Milk', 'milk_type', 0.65, 'inherit'),

-- Caffeine modifications
('extra_shot', 'Extra Shot', 'caffeine', 0.75, 'inherit'),
('decaf', 'Decaffeinated', 'caffeine', 0.00, 'inherit'),
('half_caff', 'Half Caffeine', 'caffeine', 0.00, 'inherit'),

-- Sweetness (no charge for basic sweeteners)
('extra_sweet', 'Extra Sweet', 'sweetness', 0.00, 'inherit'),
('sugar_free_syrup', 'Sugar-Free Syrup', 'sweetness', 0.00, 'inherit'),
('vanilla_syrup', 'Vanilla Syrup', 'flavor', 0.65, 'inherit'),
('caramel_syrup', 'Caramel Syrup', 'flavor', 0.65, 'inherit'),

-- Temperature
('extra_hot', 'Extra Hot', 'temperature', 0.00, 'inherit'),
('iced', 'Iced', 'temperature', 0.00, 'inherit'),

-- Premium additions
('whipped_cream', 'Whipped Cream', 'topping', 0.50, 'inherit'),
('extra_foam', 'Extra Foam', 'preparation', 0.00, 'inherit');

-- Coffee shop modification groups
INSERT INTO modification_groups (id, name, selection_type, max_selections, is_required) VALUES
('milk_choice', 'Milk Options', 'single', 1, false),
('caffeine_level', 'Caffeine Level', 'single', 1, false),
('sweetness_level', 'Sweetness & Flavor', 'multiple', 2, false),
('temperature_choice', 'Temperature', 'single', 1, false),
('premium_additions', 'Premium Add-ons', 'multiple', 5, false);

-- Link modifications to groups
INSERT INTO modification_group_items (group_id, modification_id, is_default) VALUES
('milk_choice', 'oat_milk', false),
('milk_choice', 'almond_milk', false),
('milk_choice', 'soy_milk', false),
('milk_choice', 'coconut_milk', false),

('caffeine_level', 'extra_shot', false),
('caffeine_level', 'decaf', false),
('caffeine_level', 'half_caff', false),

('sweetness_level', 'extra_sweet', false),
('sweetness_level', 'sugar_free_syrup', false),
('sweetness_level', 'vanilla_syrup', false),
('sweetness_level', 'caramel_syrup', false),

('temperature_choice', 'extra_hot', true),  -- Default selection
('temperature_choice', 'iced', false),

('premium_additions', 'whipped_cream', false),
('premium_additions', 'extra_foam', false);

-- Coffee shop localizations (English/Spanish for US market)
INSERT INTO localizations (localization_key, locale_code, text_value) VALUES
('mod.oat_milk', 'en-US', 'Oat Milk'),
('mod.oat_milk', 'es-US', 'Leche de Avena'),
('mod.almond_milk', 'en-US', 'Almond Milk'),
('mod.almond_milk', 'es-US', 'Leche de Almendra'),
('mod.extra_shot', 'en-US', 'Extra Shot'),
('mod.extra_shot', 'es-US', 'Shot Extra'),
('mod.vanilla_syrup', 'en-US', 'Vanilla Syrup'),
('mod.vanilla_syrup', 'es-US', 'Jarabe de Vainilla');

-- Update coffee modifications to reference localization keys
UPDATE modifications SET localization_key = 'mod.' || id 
WHERE id IN ('oat_milk', 'almond_milk', 'extra_shot', 'vanilla_syrup');

-- Note: In a real coffee shop database, you would apply these to relevant categories:
-- INSERT INTO product_modification_groups (category_id, modification_group_id) VALUES
-- ('ESPRESSO_DRINKS', 'milk_choice'),
-- ('ESPRESSO_DRINKS', 'caffeine_level'),
-- ('ALL_BEVERAGES', 'temperature_choice'),
-- ('SPECIALTY_DRINKS', 'premium_additions');
