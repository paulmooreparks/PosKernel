-- Restaurant Extension Sample Data
-- Starbucks-inspired coffee shop menu for Restaurant Extension Service
-- All prices stored in cents (e.g. 265 = $2.65)

-- Insert categories first
INSERT OR REPLACE INTO categories (id, name, description, display_order, is_active) VALUES
('HOT_COFFEES', 'Hot Coffees', 'Freshly brewed hot coffee drinks', 1, 1),
('ICED_COFFEES', 'Iced Coffees', 'Refreshing cold coffee beverages', 2, 1),
('HOT_TEAS', 'Hot Teas', 'Premium hot tea selections', 3, 1),
('ICED_TEAS', 'Iced Teas', 'Refreshing iced tea beverages', 4, 1),
('FRAPPUCCINOS', 'Frappuccinos', 'Blended iced coffee drinks', 5, 1),
('HOT_DRINKS', 'Hot Drinks', 'Hot chocolate and specialty drinks', 6, 1),
('PASTRIES', 'Pastries', 'Fresh baked goods and sweets', 7, 1),
('SANDWICHES', 'Sandwiches & Wraps', 'Fresh sandwiches and wraps', 8, 1),
('SNACKS', 'Snacks', 'Quick bites and protein boxes', 9, 1);

-- Hot Coffee Products (prices in cents)
INSERT OR REPLACE INTO products (sku, name, description, category_id, base_price_cents, is_active) VALUES
('AMERICANO', 'Americano', 'Espresso shots topped with hot water', 'HOT_COFFEES', 265, 1),
('PIKE_PLACE', 'Pike Place Roast', 'Our signature medium roast coffee', 'HOT_COFFEES', 245, 1),
('LATTE', 'Caffè Latte', 'Rich espresso with steamed milk', 'HOT_COFFEES', 465, 1),
('CAPPUCCINO', 'Cappuccino', 'Espresso with steamed milk and foam', 'HOT_COFFEES', 445, 1),
('MACCHIATO', 'Caffè Macchiato', 'Espresso marked with steamed milk', 'HOT_COFFEES', 475, 1),
('MOCHA', 'Caffè Mocha', 'Espresso with chocolate and steamed milk', 'HOT_COFFEES', 495, 1),
('WHITE_MOCHA', 'White Chocolate Mocha', 'Espresso with white chocolate and steamed milk', 'HOT_COFFEES', 525, 1),
('CARAMEL_MACCHIATO', 'Caramel Macchiato', 'Vanilla syrup, steamed milk, espresso, and caramel', 'HOT_COFFEES', 525, 1),
('FLAT_WHITE', 'Flat White', 'Ristretto shots with steamed milk', 'HOT_COFFEES', 465, 1),
('CORTADO', 'Cortado', 'Espresso with warm milk', 'HOT_COFFEES', 445, 1);

-- Iced Coffee Products  
INSERT OR REPLACE INTO products (sku, name, description, category_id, base_price_cents, is_active) VALUES
('ICED_AMERICANO', 'Iced Americano', 'Espresso shots with cold water over ice', 'ICED_COFFEES', 285, 1),
('ICED_COFFEE', 'Iced Coffee', 'Freshly brewed coffee served over ice', 'ICED_COFFEES', 265, 1),
('ICED_LATTE', 'Iced Caffè Latte', 'Rich espresso with cold milk over ice', 'ICED_COFFEES', 485, 1),
('ICED_CARAMEL_MACCHIATO', 'Iced Caramel Macchiato', 'Vanilla syrup, milk, espresso, and caramel over ice', 'ICED_COFFEES', 545, 1),
('COLD_BREW', 'Cold Brew Coffee', '20-hour slow-steeped coffee', 'ICED_COFFEES', 325, 1),
('NITRO_COLD_BREW', 'Nitro Cold Brew', 'Cold brew infused with nitrogen', 'ICED_COFFEES', 365, 1);

-- Hot Tea Products
INSERT OR REPLACE INTO products (sku, name, description, category_id, base_price_cents, is_active) VALUES
('EARL_GREY', 'Earl Grey', 'Classic bergamot-flavored black tea', 'HOT_TEAS', 245, 1),
('ENGLISH_BREAKFAST', 'English Breakfast', 'Full-bodied black tea blend', 'HOT_TEAS', 245, 1),
('GREEN_TEA', 'Green Tea', 'Smooth and refreshing green tea', 'HOT_TEAS', 245, 1),
('CHAI_LATTE', 'Chai Tea Latte', 'Spiced tea blend with steamed milk', 'HOT_TEAS', 445, 1),
('MATCHA_LATTE', 'Matcha Green Tea Latte', 'Ceremonial grade matcha with steamed milk', 'HOT_TEAS', 475, 1);

-- Iced Tea Products
INSERT OR REPLACE INTO products (sku, name, description, category_id, base_price_cents, is_active) VALUES
('ICED_BLACK_TEA', 'Iced Black Tea', 'Refreshing black tea over ice', 'ICED_TEAS', 265, 1),
('ICED_GREEN_TEA', 'Iced Green Tea', 'Light and refreshing green tea over ice', 'ICED_TEAS', 265, 1),
('ICED_CHAI_LATTE', 'Iced Chai Tea Latte', 'Spiced tea blend with cold milk over ice', 'ICED_TEAS', 465, 1),
('GREEN_TEA_LEMONADE', 'Iced Green Tea Lemonade', 'Green tea with lemonade over ice', 'ICED_TEAS', 325, 1);

-- Frappuccino Products
INSERT OR REPLACE INTO products (sku, name, description, category_id, base_price_cents, is_active) VALUES
('CARAMEL_FRAPPUCCINO', 'Caramel Frappuccino', 'Coffee blended with caramel flavor and ice', 'FRAPPUCCINOS', 525, 1),
('MOCHA_FRAPPUCCINO', 'Mocha Frappuccino', 'Coffee blended with chocolate and ice', 'FRAPPUCCINOS', 525, 1),
('VANILLA_FRAPPUCCINO', 'Vanilla Bean Frappuccino', 'Vanilla bean blended with milk and ice', 'FRAPPUCCINOS', 495, 1),
('JAVA_CHIP_FRAPPUCCINO', 'Java Chip Frappuccino', 'Mocha frappuccino with chocolate chips', 'FRAPPUCCINOS', 545, 1);

-- Hot Drinks (Non-Coffee)
INSERT OR REPLACE INTO products (sku, name, description, category_id, base_price_cents, is_active) VALUES
('HOT_CHOCOLATE', 'Hot Chocolate', 'Rich chocolate with steamed milk', 'HOT_DRINKS', 365, 1),
('WHITE_HOT_CHOCOLATE', 'White Hot Chocolate', 'White chocolate with steamed milk', 'HOT_DRINKS', 385, 1),
('STEAMED_MILK', 'Steamed Milk', 'Perfectly steamed milk', 'HOT_DRINKS', 225, 1);

-- Pastries
INSERT OR REPLACE INTO products (sku, name, description, category_id, base_price_cents, is_active) VALUES
('CROISSANT', 'Butter Croissant', 'Flaky, buttery French pastry', 'PASTRIES', 295, 1),
('CHOCOLATE_CROISSANT', 'Chocolate Croissant', 'Buttery croissant filled with chocolate', 'PASTRIES', 345, 1),
('BLUEBERRY_MUFFIN', 'Blueberry Muffin', 'Fresh blueberries in a tender muffin', 'PASTRIES', 325, 1),
('BANANA_BREAD', 'Banana Bread', 'Moist banana bread slice', 'PASTRIES', 295, 1),
('SCONE', 'Blueberry Scone', 'Traditional English scone with blueberries', 'PASTRIES', 295, 1),
('DONUT', 'Glazed Donut', 'Classic glazed cake donut', 'PASTRIES', 245, 1);

-- Sandwiches & Wraps
INSERT OR REPLACE INTO products (sku, name, description, category_id, base_price_cents, is_active) VALUES
('BACON_GOUDA', 'Bacon & Gouda Sandwich', 'Applewood smoked bacon with gouda cheese', 'SANDWICHES', 495, 1),
('TURKEY_PESTO', 'Turkey Pesto Panini', 'Sliced turkey with pesto and mozzarella', 'SANDWICHES', 595, 1),
('VEGGIE_WRAP', 'Veggie Wrap', 'Fresh vegetables in a spinach tortilla', 'SANDWICHES', 445, 1);

-- Snacks
INSERT OR REPLACE INTO products (sku, name, description, category_id, base_price_cents, is_active) VALUES
('PROTEIN_BOX', 'Protein Bistro Box', 'Hard-boiled eggs, cheese, fruit, and nuts', 'SNACKS', 545, 1),
('YOGURT_PARFAIT', 'Greek Yogurt Parfait', 'Greek yogurt with berries and granola', 'SNACKS', 345, 1),
('FRUIT_CUP', 'Fresh Fruit Cup', 'Seasonal fresh fruit medley', 'SNACKS', 295, 1);
