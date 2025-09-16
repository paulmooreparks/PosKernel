-- Example: Grocery store modifications
-- Demonstrates how the general modification system works for different store types

-- Grocery store specific modifications
INSERT INTO modifications (id, name, category, price_adjustment, tax_treatment) VALUES
-- Organic substitutions
('organic_substitute', 'Organic Version', 'dietary', 0.50, 'inherit'),
('gluten_free', 'Gluten-Free Alternative', 'dietary', 0.75, 'inherit'),
('sugar_free', 'Sugar-Free Version', 'dietary', 0.25, 'inherit'),

-- Size modifications  
('half_portion', 'Half Portion', 'size', -1.00, 'inherit'),
('double_portion', 'Double Portion', 'size', 3.00, 'inherit'),

-- Preparation modifications
('pre_sliced', 'Pre-Sliced', 'preparation', 0.30, 'inherit'),
('extra_packaging', 'Gift Wrapping', 'service', 2.00, 'standard');

-- Grocery modification groups
INSERT INTO modification_groups (id, name, selection_type, max_selections) VALUES
('dietary_options', 'Dietary Alternatives', 'single', 1),
('portion_size', 'Portion Size', 'single', 1),
('service_options', 'Service Options', 'multiple', 3);

-- Link modifications to groups
INSERT INTO modification_group_items (group_id, modification_id) VALUES
('dietary_options', 'organic_substitute'),
('dietary_options', 'gluten_free'),
('dietary_options', 'sugar_free'),
('portion_size', 'half_portion'),
('portion_size', 'double_portion'),
('service_options', 'pre_sliced'),
('service_options', 'extra_packaging');

-- Example localizations for grocery items
INSERT INTO localizations (localization_key, locale_code, text_value) VALUES
('mod.organic_substitute', 'en-US', 'Organic Version'),
('mod.organic_substitute', 'es-US', 'Versión Orgánica'),
('mod.gluten_free', 'en-US', 'Gluten-Free Alternative'),
('mod.gluten_free', 'es-US', 'Alternativa Sin Gluten'),
('mod.sugar_free', 'en-US', 'Sugar-Free Version'),
('mod.sugar_free', 'es-US', 'Versión Sin Azúcar');

-- Update grocery modifications to reference localization keys
UPDATE modifications SET localization_key = 'mod.' || id 
WHERE id IN ('organic_substitute', 'gluten_free', 'sugar_free');

-- Note: In a real grocery database, you would apply these to specific categories:
-- INSERT INTO product_modification_groups (category_id, modification_group_id) VALUES
-- ('PRODUCE', 'dietary_options'),
-- ('BAKERY', 'dietary_options'),
-- ('DELI', 'portion_size'),
-- ('BAKERY', 'service_options');
