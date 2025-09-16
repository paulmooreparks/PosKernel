-- Kopitiam-specific product data
-- Traditional Singapore kopitiam menu with authentic items

-- Categories for kopitiam
INSERT INTO categories (id, name, description, tax_category, requires_preparation, average_prep_time_minutes, display_order, is_active) VALUES
('KOPI_HOT', 'Kopi & Hot Drinks', 'Traditional coffee and hot beverages', 'STANDARD', true, 3, 1, true),
('TEH_HOT', 'Teh & Hot Tea', 'Traditional tea varieties', 'STANDARD', true, 3, 2, true),
('COLD_DRINKS', 'Cold Drinks', 'Iced beverages and soft drinks', 'STANDARD', true, 2, 3, true),
('TOAST_BREAD', 'Toast & Bread', 'Traditional toast and bread items', 'STANDARD', true, 5, 4, true),
('LOCAL_FOOD', 'Local Food', 'Traditional kopitiam food items', 'STANDARD', true, 8, 5, true),
('EGGS', 'Egg Items', 'Traditional egg preparations', 'STANDARD', true, 6, 6, true);

-- Traditional Kopi (Coffee) varieties - using decimal pricing
INSERT INTO products (sku, name, description, category_id, base_price, is_active, requires_preparation, preparation_time_minutes, popularity_rank) VALUES
('KOPI', 'Kopi', 'Traditional coffee with condensed milk', 'KOPI_HOT', 1.40, true, true, 3, 1),
('KOPI_O', 'Kopi O', 'Black coffee with sugar', 'KOPI_HOT', 1.20, true, true, 3, 2),
('KOPI_C', 'Kopi C', 'Coffee with evaporated milk and sugar', 'KOPI_HOT', 1.40, true, true, 3, 3),


-- Traditional Teh (Tea) varieties  
('TEH', 'Teh', 'Traditional tea with condensed milk', 'TEH_HOT', 1.40, true, true, 3, 4),
('TEH_O', 'Teh O', 'Black tea with sugar', 'TEH_HOT', 1.20, true, true, 3, 5),
('TEH_C', 'Teh C', 'Tea with evaporated milk and sugar', 'TEH_HOT', 1.40, true, true, 3, 6),


-- Cold drinks (Peng stays - different preparation/pricing)
('KOPI_PENG', 'Kopi Peng', 'Iced coffee with condensed milk', 'COLD_DRINKS', 1.60, true, true, 3, 7),
('TEH_PENG', 'Teh Peng', 'Iced tea with condensed milk', 'COLD_DRINKS', 1.60, true, true, 3, 8),
('MILO_PENG', 'Milo Peng', 'Iced Milo chocolate drink', 'COLD_DRINKS', 1.80, true, true, 2, 9),
('BANDUNG', 'Bandung', 'Rose syrup with milk', 'COLD_DRINKS', 1.50, true, true, 2, 10),


-- Traditional toast and bread
('KAYA_TOAST', 'Kaya Toast', 'Traditional toast with kaya (coconut egg jam) and butter', 'TOAST_BREAD', 1.80, true, true, 5, 11),
('BUTTER_SUGAR_TOAST', 'Butter Sugar Toast', 'Toast with butter and sugar', 'TOAST_BREAD', 1.60, true, true, 4, 12),
('PEANUT_BUTTER_TOAST', 'Peanut Butter Toast', 'Toast with peanut butter', 'TOAST_BREAD', 1.70, true, true, 4, 13),
('FRENCH_TOAST', 'French Toast', 'Thick toast with kaya and butter', 'TOAST_BREAD', 2.20, true, true, 6, 14),


-- Traditional egg items
('SOFT_BOILED_EGGS', 'Soft Boiled Eggs', 'Two soft-boiled eggs with dark soy sauce and pepper', 'EGGS', 1.60, true, true, 6, 15),
('HALF_BOILED_EGGS', 'Half Boiled Eggs', 'Traditional half-boiled eggs', 'EGGS', 1.60, true, true, 6, 16),


-- Local food specialties
('MEE_GORENG', 'Mee Goreng', 'Fried yellow noodles with vegetables and egg', 'LOCAL_FOOD', 4.50, true, true, 8, 17),
('MAGGI_GORENG', 'Maggi Goreng', 'Fried instant noodles with egg', 'LOCAL_FOOD', 3.80, true, true, 7, 18),
('ROTI_JOHN', 'Roti John', 'French loaf with egg and minced meat', 'LOCAL_FOOD', 5.20, true, true, 10, 19);
