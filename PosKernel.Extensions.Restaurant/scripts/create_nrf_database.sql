-- NRF Modifications Database Creation Script
-- This creates the database that the Rust kernel will query for modifications

-- Use the existing schema and data from restaurant_catalog_data.sql
-- This script should be run after restaurant_catalog_data.sql to create a proper NRF database

-- For testing, we'll create a simplified version with the key NRF data

-- This database will be queried by the Rust kernel for:
-- 1. Available modifications for products
-- 2. Set meal components for automatic processing
-- 3. Hierarchical modification validation

-- The actual database will be created by running the existing SQL files:
-- 1. restaurant_catalog_schema.sql (schema)
-- 2. restaurant_catalog_data.sql (NRF data)

-- Database path for Rust kernel: PosKernel.Extensions.Restaurant/data/restaurant_modifications.db
