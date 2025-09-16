-- Add localization support to existing catalog tables
-- This makes any existing store database support multi-language

-- Add localization support to products
-- Note: Ignore errors if columns already exist
-- In production, check column existence first

-- Add localization support to products
ALTER TABLE products ADD COLUMN name_localization_key VARCHAR(100);
ALTER TABLE products ADD COLUMN description_localization_key VARCHAR(100);

-- Add localization support to categories  
ALTER TABLE categories ADD COLUMN name_localization_key VARCHAR(100);
ALTER TABLE categories ADD COLUMN description_localization_key VARCHAR(100);

-- Convert price storage from cents to flexible decimal
-- This handles different currency decimal place requirements
ALTER TABLE products ADD COLUMN base_price DECIMAL(15,6);

-- Migrate existing cent-based prices to decimal (if they exist)
-- This will convert 140 cents to 1.40 dollars, etc.
UPDATE products SET base_price = base_price_cents / 100.0 WHERE base_price_cents IS NOT NULL AND base_price IS NULL;
