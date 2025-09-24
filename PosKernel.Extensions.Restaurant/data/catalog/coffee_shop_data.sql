-- Coffee Shop Product Data
-- Starbucks-inspired menu with realistic pricing
-- All prices in USD (can be converted by currency service)

-- Insert categories first
INSERT OR REPLACE INTO categories (id, name, description, display_order, is_active) VALUES
('HOT_COFFEES', 'Hot Coffees', 'Freshly brewed hot coffee drinks', 1, TRUE),
('ICED_COFFEES', 'Iced Coffees', 'Refreshing cold coffee beverages', 2, TRUE),
('HOT_TEAS', 'Hot Teas', 'Premium hot tea selections', 3, TRUE),
('ICED_TEAS', 'Iced Teas', 'Refreshing iced tea beverages', 4, TRUE),
('FRAPPUCCINOS', 'Frappuccinos', 'Blended iced coffee drinks', 5, TRUE),
('HOT_DRINKS', 'Hot Drinks', 'Hot chocolate and specialty drinks', 6, TRUE),
('PASTRIES', 'Pastries', 'Fresh baked goods and sweets', 7, TRUE),
('SANDWICHES', 'Sandwiches & Wraps', 'Fresh sandwiches and wraps', 8, TRUE),
('SNACKS', 'Snacks', 'Quick bites and protein boxes', 9, TRUE);

-- Hot Coffee Products
INSERT OR REPLACE INTO products (sku, name, description, category_id, base_price, popularity_rank, preparation_time_minutes, allergens) VALUES
('AMERICANO', 'Americano', 'Espresso shots topped with hot water', 'HOT_COFFEES', 2.65, 3, 3, '[]'),
('PIKE_PLACE', 'Pike Place Roast', 'Our signature medium roast coffee', 'HOT_COFFEES', 2.45, 1, 2, '[]'),
('LATTE', 'Caffè Latte', 'Rich espresso with steamed milk', 'HOT_COFFEES', 4.65, 2, 4, '["dairy"]'),
('CAPPUCCINO', 'Cappuccino', 'Espresso with steamed milk and foam', 'HOT_COFFEES', 4.45, 4, 4, '["dairy"]'),
('MACCHIATO', 'Caffè Macchiato', 'Espresso marked with steamed milk', 'HOT_COFFEES', 4.75, 8, 4, '["dairy"]'),
('MOCHA', 'Caffè Mocha', 'Espresso with chocolate and steamed milk', 'HOT_COFFEES', 4.95, 5, 5, '["dairy"]'),
('WHITE_MOCHA', 'White Chocolate Mocha', 'Espresso with white chocolate and steamed milk', 'HOT_COFFEES', 5.25, 7, 5, '["dairy"]'),
('CARAMEL_MACCHIATO', 'Caramel Macchiato', 'Vanilla syrup, steamed milk, espresso, and caramel', 'HOT_COFFEES', 5.25, 6, 5, '["dairy"]'),
('FLAT_WHITE', 'Flat White', 'Ristretto shots with steamed milk', 'HOT_COFFEES', 4.65, 9, 4, '["dairy"]'),
('CORTADO', 'Cortado', 'Espresso with warm milk', 'HOT_COFFEES', 4.45, 15, 4, '["dairy"]');

-- Iced Coffee Products  
INSERT OR REPLACE INTO products (sku, name, description, category_id, base_price, popularity_rank, preparation_time_minutes, allergens) VALUES
('ICED_AMERICANO', 'Iced Americano', 'Espresso shots with cold water over ice', 'ICED_COFFEES', 2.85, 11, 3, '[]'),
('ICED_COFFEE', 'Iced Coffee', 'Freshly brewed coffee served over ice', 'ICED_COFFEES', 2.65, 10, 2, '[]'),
('ICED_LATTE', 'Iced Caffè Latte', 'Rich espresso with cold milk over ice', 'ICED_COFFEES', 4.85, 12, 4, '["dairy"]'),
('ICED_CARAMEL_MACCHIATO', 'Iced Caramel Macchiato', 'Vanilla syrup, milk, espresso, and caramel over ice', 'ICED_COFFEES', 5.45, 13, 5, '["dairy"]'),
('COLD_BREW', 'Cold Brew Coffee', '20-hour slow-steeped coffee', 'ICED_COFFEES', 3.25, 14, 1, '[]'),
('NITRO_COLD_BREW', 'Nitro Cold Brew', 'Cold brew infused with nitrogen', 'ICED_COFFEES', 3.65, 20, 2, '[]');

-- Hot Tea Products
INSERT OR REPLACE INTO products (sku, name, description, category_id, base_price, popularity_rank, preparation_time_minutes, allergens) VALUES
('EARL_GREY', 'Earl Grey', 'Classic bergamot-flavored black tea', 'HOT_TEAS', 2.45, 21, 5, '[]'),
('ENGLISH_BREAKFAST', 'English Breakfast', 'Full-bodied black tea blend', 'HOT_TEAS', 2.45, 22, 5, '[]'),
('GREEN_TEA', 'Green Tea', 'Smooth and refreshing green tea', 'HOT_TEAS', 2.45, 23, 5, '[]'),
('CHAI_LATTE', 'Chai Tea Latte', 'Spiced tea blend with steamed milk', 'HOT_TEAS', 4.45, 16, 5, '["dairy"]'),
('MATCHA_LATTE', 'Matcha Green Tea Latte', 'Ceremonial grade matcha with steamed milk', 'HOT_TEAS', 4.75, 25, 5, '["dairy"]');

-- Iced Tea Products
INSERT OR REPLACE INTO products (sku, name, description, category_id, base_price, popularity_rank, preparation_time_minutes, allergens) VALUES
('ICED_BLACK_TEA', 'Iced Black Tea', 'Refreshing black tea over ice', 'ICED_TEAS', 2.65, 26, 3, '[]'),
('ICED_GREEN_TEA', 'Iced Green Tea', 'Light and refreshing green tea over ice', 'ICED_TEAS', 2.65, 27, 3, '[]'),
('ICED_CHAI_LATTE', 'Iced Chai Tea Latte', 'Spiced tea blend with cold milk over ice', 'ICED_TEAS', 4.65, 17, 5, '["dairy"]'),
('GREEN_TEA_LEMONADE', 'Iced Green Tea Lemonade', 'Green tea with lemonade over ice', 'ICED_TEAS', 3.25, 28, 4, '[]');

-- Frappuccino Products
INSERT OR REPLACE INTO products (sku, name, description, category_id, base_price, popularity_rank, preparation_time_minutes, allergens) VALUES
('CARAMEL_FRAPPUCCINO', 'Caramel Frappuccino', 'Coffee blended with caramel flavor and ice', 'FRAPPUCCINOS', 5.25, 18, 6, '["dairy"]'),
('MOCHA_FRAPPUCCINO', 'Mocha Frappuccino', 'Coffee blended with chocolate and ice', 'FRAPPUCCINOS', 5.25, 19, 6, '["dairy"]'),
('VANILLA_FRAPPUCCINO', 'Vanilla Bean Frappuccino', 'Vanilla bean blended with milk and ice', 'FRAPPUCCINOS', 4.95, 29, 6, '["dairy"]'),
('JAVA_CHIP_FRAPPUCCINO', 'Java Chip Frappuccino', 'Mocha frappuccino with chocolate chips', 'FRAPPUCCINOS', 5.45, 24, 6, '["dairy"]');

-- Hot Drinks (Non-Coffee)
INSERT OR REPLACE INTO products (sku, name, description, category_id, base_price, popularity_rank, preparation_time_minutes, allergens) VALUES
('HOT_CHOCOLATE', 'Hot Chocolate', 'Rich chocolate with steamed milk', 'HOT_DRINKS', 3.65, 30, 4, '["dairy"]'),
('WHITE_HOT_CHOCOLATE', 'White Hot Chocolate', 'White chocolate with steamed milk', 'HOT_DRINKS', 3.85, 35, 4, '["dairy"]'),
('STEAMED_MILK', 'Steamed Milk', 'Perfectly steamed milk', 'HOT_DRINKS', 2.25, 40, 3, '["dairy"]');

-- Pastries
INSERT OR REPLACE INTO products (sku, name, description, category_id, base_price, popularity_rank, preparation_time_minutes, allergens) VALUES
('CROISSANT', 'Butter Croissant', 'Flaky, buttery French pastry', 'PASTRIES', 2.95, 31, 1, '["gluten", "dairy"]'),
('CHOCOLATE_CROISSANT', 'Chocolate Croissant', 'Buttery croissant filled with chocolate', 'PASTRIES', 3.45, 32, 1, '["gluten", "dairy"]'),
('BLUEBERRY_MUFFIN', 'Blueberry Muffin', 'Fresh blueberries in a tender muffin', 'PASTRIES', 3.25, 33, 1, '["gluten", "dairy", "eggs"]'),
('BANANA_BREAD', 'Banana Bread', 'Moist banana bread slice', 'PASTRIES', 2.95, 34, 1, '["gluten", "dairy", "eggs"]'),
('SCONE', 'Blueberry Scone', 'Traditional English scone with blueberries', 'PASTRIES', 2.95, 36, 1, '["gluten", "dairy", "eggs"]'),
('DONUT', 'Glazed Donut', 'Classic glazed cake donut', 'PASTRIES', 2.45, 37, 1, '["gluten", "dairy", "eggs"]');

-- Sandwiches & Wraps
INSERT OR REPLACE INTO products (sku, name, description, category_id, base_price, popularity_rank, preparation_time_minutes, allergens) VALUES
('BACON_GOUDA', 'Bacon & Gouda Sandwich', 'Applewood smoked bacon with gouda cheese', 'SANDWICHES', 4.95, 38, 2, '["gluten", "dairy", "pork"]'),
('TURKEY_PESTO', 'Turkey Pesto Panini', 'Sliced turkey with pesto and mozzarella', 'SANDWICHES', 5.95, 39, 3, '["gluten", "dairy"]'),
('VEGGIE_WRAP', 'Veggie Wrap', 'Fresh vegetables in a spinach tortilla', 'SANDWICHES', 4.45, 41, 2, '["gluten"]');

-- Snacks
INSERT OR REPLACE INTO products (sku, name, description, category_id, base_price, popularity_rank, preparation_time_minutes, allergens) VALUES
('PROTEIN_BOX', 'Protein Bistro Box', 'Hard-boiled eggs, cheese, fruit, and nuts', 'SNACKS', 5.45, 42, 1, '["eggs", "dairy", "nuts"]'),
('YOGURT_PARFAIT', 'Greek Yogurt Parfait', 'Greek yogurt with berries and granola', 'SNACKS', 3.45, 43, 1, '["dairy", "gluten"]'),
('FRUIT_CUP', 'Fresh Fruit Cup', 'Seasonal fresh fruit medley', 'SNACKS', 2.95, 44, 1, '[]');
