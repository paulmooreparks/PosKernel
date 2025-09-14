-- Import coffee shop product data into SQLite database
-- This script converts the XferLang product catalog to SQL

-- Coffee products
INSERT INTO products (sku, name, description, category_id, base_price_cents, requires_preparation, preparation_time_minutes, popularity_rank) VALUES
    ('COFFEE_SM', 'Small Coffee', 'Classic drip coffee, 8oz', 'hot_beverages', 299, true, 2, 1),
    ('COFFEE_MD', 'Medium Coffee', 'Classic drip coffee, 12oz', 'hot_beverages', 349, true, 2, 3),
    ('COFFEE_LG', 'Large Coffee', 'Classic drip coffee, 16oz', 'hot_beverages', 399, true, 2, 1),
    ('LATTE', 'Caffe Latte', 'Espresso with steamed milk', 'hot_beverages', 499, true, 3, 2),
    ('CAPPUCCINO', 'Cappuccino', 'Espresso with steamed milk and foam', 'hot_beverages', 449, true, 3, 8),
    ('MUFFIN_BLUEBERRY', 'Blueberry Muffin', 'Fresh blueberry muffin with Maine blueberries', 'bakery', 249, false, 0, 3),
    ('CROISSANT_PLAIN', 'Plain Croissant', 'Buttery French croissant, baked fresh daily', 'bakery', 299, false, 0, 7),
    ('BREAKFAST_SANDWICH', 'Breakfast Sandwich', 'Egg, aged cheddar, and Canadian bacon on English muffin', 'breakfast', 649, true, 3, 4),
    ('TOAST_AVOCADO', 'Avocado Toast', 'Smashed avocado on artisan multigrain with lime and sea salt', 'breakfast', 899, true, 4, 10),
    ('SANDWICH_TURKEY', 'Turkey Club Sandwich', 'Roasted turkey, applewood bacon, lettuce, tomato on sourdough', 'lunch', 999, true, 5, 5),
    ('COFFEE_BEANS_1LB', 'House Blend Coffee Beans', 'Medium roast blend, 1lb bag', 'retail', 1299, false, 0, 20),
    ('GIFT_CARD_25', '$25 Gift Card', 'Electronic gift card worth $25', 'gift_cards', 2500, false, 0, 15);

-- Product identifiers
INSERT INTO product_identifiers (product_sku, identifier_type, identifier_value, is_primary) VALUES
    ('COFFEE_SM', 'barcode', '123456789012', true),
    ('COFFEE_SM', 'internal_code', 'HOT_001', false),
    ('COFFEE_MD', 'barcode', '123456789013', true),
    ('COFFEE_MD', 'internal_code', 'HOT_002', false),
    ('COFFEE_LG', 'barcode', '123456789014', true),
    ('COFFEE_LG', 'internal_code', 'HOT_003', false),
    ('LATTE', 'barcode', '123456789015', true),
    ('LATTE', 'internal_code', 'HOT_010', false),
    ('CAPPUCCINO', 'barcode', '123456789016', true),
    ('CAPPUCCINO', 'internal_code', 'HOT_011', false),
    ('MUFFIN_BLUEBERRY', 'barcode', '123456789020', true),
    ('MUFFIN_BLUEBERRY', 'internal_code', 'BAK_001', false),
    ('CROISSANT_PLAIN', 'barcode', '123456789021', true),
    ('CROISSANT_PLAIN', 'internal_code', 'BAK_010', false),
    ('BREAKFAST_SANDWICH', 'barcode', '123456789030', true),
    ('BREAKFAST_SANDWICH', 'internal_code', 'BRK_001', false),
    ('TOAST_AVOCADO', 'barcode', '123456789031', true),
    ('TOAST_AVOCADO', 'internal_code', 'BRK_010', false),
    ('SANDWICH_TURKEY', 'barcode', '123456789040', true),
    ('SANDWICH_TURKEY', 'internal_code', 'LUN_001', false),
    ('COFFEE_BEANS_1LB', 'barcode', '123456789050', true),
    ('COFFEE_BEANS_1LB', 'upc', '012345678905', false),
    ('COFFEE_BEANS_1LB', 'internal_code', 'RTL_001', false),
    ('GIFT_CARD_25', 'internal_code', 'GFT_025', true);

-- Product specifications
INSERT INTO product_specifications (product_sku, spec_key, spec_value, spec_type) VALUES
    ('COFFEE_SM', 'size', '8oz', 'string'),
    ('COFFEE_SM', 'caffeine_content', 'medium', 'string'),
    ('COFFEE_SM', 'temperature', 'hot', 'string'),
    ('COFFEE_SM', 'calories', '5', 'number'),
    ('COFFEE_MD', 'size', '12oz', 'string'),
    ('COFFEE_MD', 'caffeine_content', 'medium', 'string'),
    ('COFFEE_MD', 'calories', '8', 'number'),
    ('COFFEE_LG', 'size', '16oz', 'string'),
    ('COFFEE_LG', 'caffeine_content', 'high', 'string'),
    ('COFFEE_LG', 'calories', '10', 'number'),
    ('LATTE', 'size', '12oz', 'string'),
    ('LATTE', 'caffeine_content', 'medium', 'string'),
    ('LATTE', 'calories', '150', 'number'),
    ('CAPPUCCINO', 'size', '8oz', 'string'),
    ('CAPPUCCINO', 'caffeine_content', 'medium', 'string'),
    ('CAPPUCCINO', 'calories', '120', 'number'),
    ('MUFFIN_BLUEBERRY', 'weight', '120g', 'string'),
    ('MUFFIN_BLUEBERRY', 'fresh_baked', 'true', 'boolean'),
    ('MUFFIN_BLUEBERRY', 'vegetarian_friendly', 'true', 'boolean'),
    ('CROISSANT_PLAIN', 'weight', '65g', 'string'),
    ('CROISSANT_PLAIN', 'fresh_baked', 'true', 'boolean'),
    ('CROISSANT_PLAIN', 'origin', 'French_style', 'string'),
    ('BREAKFAST_SANDWICH', 'weight', '180g', 'string'),
    ('BREAKFAST_SANDWICH', 'served_hot', 'true', 'boolean'),
    ('BREAKFAST_SANDWICH', 'protein_level', 'high', 'string'),
    ('TOAST_AVOCADO', 'vegan', 'true', 'boolean'),
    ('TOAST_AVOCADO', 'organic', 'true', 'boolean'),
    ('TOAST_AVOCADO', 'healthy_option', 'true', 'boolean'),
    ('SANDWICH_TURKEY', 'weight', '280g', 'string'),
    ('SANDWICH_TURKEY', 'served_cold', 'false', 'boolean'),
    ('SANDWICH_TURKEY', 'protein_level', 'high', 'string'),
    ('COFFEE_BEANS_1LB', 'weight', '1lb', 'string'),
    ('COFFEE_BEANS_1LB', 'roast_level', 'medium', 'string'),
    ('COFFEE_BEANS_1LB', 'origin', 'Colombia/Brazil_blend', 'string'),
    ('COFFEE_BEANS_1LB', 'organic', 'false', 'boolean'),
    ('GIFT_CARD_25', 'value', '25.00', 'number'),
    ('GIFT_CARD_25', 'digital', 'true', 'boolean'),
    ('GIFT_CARD_25', 'reloadable', 'true', 'boolean');

-- Product allergens
INSERT INTO product_allergens (product_sku, allergen_id, contamination_risk) VALUES
    ('LATTE', 'dairy', 'direct'),
    ('CAPPUCCINO', 'dairy', 'direct'),
    ('MUFFIN_BLUEBERRY', 'gluten', 'direct'),
    ('MUFFIN_BLUEBERRY', 'eggs', 'direct'),
    ('MUFFIN_BLUEBERRY', 'dairy', 'direct'),
    ('CROISSANT_PLAIN', 'gluten', 'direct'),
    ('CROISSANT_PLAIN', 'dairy', 'direct'),
    ('CROISSANT_PLAIN', 'eggs', 'direct'),
    ('BREAKFAST_SANDWICH', 'gluten', 'direct'),
    ('BREAKFAST_SANDWICH', 'eggs', 'direct'),
    ('BREAKFAST_SANDWICH', 'dairy', 'direct'),
    ('TOAST_AVOCADO', 'gluten', 'direct'),
    ('SANDWICH_TURKEY', 'gluten', 'direct'),
    ('SANDWICH_TURKEY', 'dairy', 'direct');

-- Nutritional information
INSERT INTO product_nutrition (product_sku, calories, fat_grams, carbohydrates_grams, protein_grams, sodium_mg) VALUES
    ('COFFEE_SM', 5, 0.0, 1.0, 0.3, 5),
    ('COFFEE_MD', 8, 0.0, 1.2, 0.4, 6),
    ('COFFEE_LG', 10, 0.0, 1.5, 0.5, 8),
    ('LATTE', 150, 8.0, 12.0, 8.0, 105),
    ('CAPPUCCINO', 120, 6.0, 10.0, 6.0, 85),
    ('MUFFIN_BLUEBERRY', 420, 18.0, 61.0, 6.0, 280),
    ('BREAKFAST_SANDWICH', 450, 24.0, 32.0, 28.0, 890),
    ('TOAST_AVOCADO', 320, 22.0, 28.0, 8.0, 420);

-- Product customizations
INSERT INTO product_customizations (product_sku, customization_type, option_value, price_modifier_cents, is_default, display_order) VALUES
    ('LATTE', 'milk_option', 'whole', 0, true, 1),
    ('LATTE', 'milk_option', '2%', 0, false, 2),
    ('LATTE', 'milk_option', 'skim', 0, false, 3),
    ('LATTE', 'milk_option', 'oat', 60, false, 4),
    ('LATTE', 'milk_option', 'almond', 50, false, 5),
    ('LATTE', 'milk_option', 'soy', 50, false, 6),
    ('LATTE', 'syrup_option', 'vanilla', 50, false, 1),
    ('LATTE', 'syrup_option', 'caramel', 50, false, 2),
    ('LATTE', 'syrup_option', 'hazelnut', 50, false, 3),
    ('LATTE', 'extra_shots', 'extra_shot', 75, false, 1),
    ('BREAKFAST_SANDWICH', 'egg_option', 'scrambled', 0, true, 1),
    ('BREAKFAST_SANDWICH', 'egg_option', 'over_easy', 0, false, 2),
    ('BREAKFAST_SANDWICH', 'meat_option', 'canadian_bacon', 0, true, 1),
    ('BREAKFAST_SANDWICH', 'meat_option', 'sausage', 0, false, 2),
    ('BREAKFAST_SANDWICH', 'meat_option', 'turkey_sausage', 25, false, 3),
    ('BREAKFAST_SANDWICH', 'cheese_option', 'cheddar', 0, true, 1),
    ('BREAKFAST_SANDWICH', 'cheese_option', 'swiss', 0, false, 2),
    ('BREAKFAST_SANDWICH', 'cheese_option', 'pepper_jack', 0, false, 3),
    ('SANDWICH_TURKEY', 'bread_option', 'sourdough', 0, true, 1),
    ('SANDWICH_TURKEY', 'bread_option', 'whole_wheat', 0, false, 2),
    ('SANDWICH_TURKEY', 'bread_option', 'rye', 0, false, 3),
    ('SANDWICH_TURKEY', 'bread_option', 'gluten_free', 100, false, 4),
    ('SANDWICH_TURKEY', 'sauce_option', 'mayo', 0, true, 1),
    ('SANDWICH_TURKEY', 'sauce_option', 'mustard', 0, false, 2),
    ('SANDWICH_TURKEY', 'sauce_option', 'avocado_spread', 75, false, 3),
    ('SANDWICH_TURKEY', 'add_on', 'extra_bacon', 150, false, 1),
    ('SANDWICH_TURKEY', 'add_on', 'cheese', 75, false, 2),
    ('SANDWICH_TURKEY', 'add_on', 'avocado', 100, false, 3);

-- Upsell suggestions
INSERT INTO product_upsells (product_sku, suggested_sku, suggestion_type, priority) VALUES
    ('COFFEE_LG', 'MUFFIN_BLUEBERRY', 'complement', 1),
    ('COFFEE_LG', 'CROISSANT_PLAIN', 'complement', 2),
    ('COFFEE_LG', 'BREAKFAST_SANDWICH', 'complement', 3),
    ('LATTE', 'MUFFIN_BLUEBERRY', 'complement', 1),
    ('MUFFIN_BLUEBERRY', 'COFFEE_LG', 'complement', 1),
    ('MUFFIN_BLUEBERRY', 'LATTE', 'complement', 2),
    ('BREAKFAST_SANDWICH', 'COFFEE_LG', 'complement', 1),
    ('COFFEE_SM', 'COFFEE_MD', 'upgrade', 1),
    ('COFFEE_MD', 'COFFEE_LG', 'upgrade', 1);

-- Analytics data
INSERT INTO product_analytics (product_sku, daily_sales_velocity, peak_hours, total_units_sold, revenue_to_date_cents) VALUES
    ('COFFEE_LG', 47, '["07:00-09:00", "14:00-16:00"]', 8500, 3391500),
    ('LATTE', 32, '["08:00-10:00", "13:00-15:00"]', 5800, 2894200),
    ('MUFFIN_BLUEBERRY', 28, '["07:30-09:30"]', 5100, 1269900),
    ('BREAKFAST_SANDWICH', 23, '["07:00-10:00"]', 4200, 2725800),
    ('SANDWICH_TURKEY', 19, '["11:30-14:00"]', 3450, 3446550),
    ('CAPPUCCINO', 18, '["08:00-10:00"]', 3200, 1436800),
    ('CROISSANT_PLAIN', 15, '["07:00-09:00"]', 2800, 837200),
    ('COFFEE_SM', 25, '["07:00-09:00", "14:00-16:00"]', 4500, 1345500),
    ('COFFEE_MD', 20, '["07:00-09:00", "14:00-16:00"]', 3600, 1256400);

-- Pricing rules
INSERT INTO pricing_rules (id, name, description, rule_type, discount_type, discount_value, start_date, end_date, conditions) VALUES
    ('happy_hour_coffee', 'Happy Hour Coffee Discount', '15% off all coffee drinks 2-4 PM weekdays', 'time_based', 'percentage', 15.0, '2025-01-01', '2025-12-31', 
     '{"days": ["monday", "tuesday", "wednesday", "thursday", "friday"], "start_time": "14:00", "end_time": "16:00"}'),
    ('bulk_coffee_discount', 'Bulk Coffee Bean Discount', '10% off when buying 3 or more bags', 'quantity_based', 'percentage', 10.0, '2025-01-01', '2025-12-31', 
     '{"minimum_quantity": 3, "same_product": false}');

-- Pricing rule products
INSERT INTO pricing_rule_products (rule_id, product_sku) VALUES
    ('happy_hour_coffee', 'COFFEE_SM'),
    ('happy_hour_coffee', 'COFFEE_MD'),
    ('happy_hour_coffee', 'COFFEE_LG'),
    ('happy_hour_coffee', 'LATTE'),
    ('happy_hour_coffee', 'CAPPUCCINO'),
    ('bulk_coffee_discount', 'COFFEE_BEANS_1LB');
