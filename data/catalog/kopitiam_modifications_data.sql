-- Kopitiam-specific modifications and localizations
-- This demonstrates the general system with a specific store type

-- Kopitiam modifications (no charge, inherits tax treatment)
INSERT INTO modifications (id, name, category, price_adjustment, tax_treatment) VALUES
('no_sugar', 'No Sugar', 'sweetness', 0.00, 'inherit'),
('less_sugar', 'Less Sugar', 'sweetness', 0.00, 'inherit'),
('extra_strong', 'Extra Strong', 'strength', 0.00, 'inherit'),
('less_strong', 'Less Strong', 'strength', 0.00, 'inherit');

-- Modification groups for kopitiam drinks
INSERT INTO modification_groups (id, name, selection_type, max_selections) VALUES
('drink_sweetness', 'Sweetness Options', 'single', 1),
('drink_strength', 'Strength Options', 'single', 1);

-- Link modifications to groups
INSERT INTO modification_group_items (group_id, modification_id) VALUES
('drink_sweetness', 'no_sugar'),
('drink_sweetness', 'less_sugar'),
('drink_strength', 'extra_strong'),
('drink_strength', 'less_strong');

-- Apply modification groups to hot drink categories
INSERT INTO product_modification_groups (category_id, modification_group_id) VALUES
('KOPI_HOT', 'drink_sweetness'),
('KOPI_HOT', 'drink_strength'),
('TEH_HOT', 'drink_sweetness'),
('TEH_HOT', 'drink_strength');

-- Singapore localizations for modifications
INSERT INTO localizations (localization_key, locale_code, text_value) VALUES
-- No Sugar localizations
('mod.no_sugar', 'en-SG', 'No Sugar'),
('mod.no_sugar', 'zh-Hans-SG', '无糖'),
('mod.no_sugar', 'ms-SG', 'Tiada Gula'),
('mod.no_sugar', 'ta-SG', 'சர்க்கரை இல்லை'),

-- Less Sugar localizations  
('mod.less_sugar', 'en-SG', 'Less Sugar'),
('mod.less_sugar', 'zh-Hans-SG', '少糖'),
('mod.less_sugar', 'ms-SG', 'Kurang Gula'),
('mod.less_sugar', 'ta-SG', 'குறைந்த சர்க்கரை'),

-- Extra Strong localizations
('mod.extra_strong', 'en-SG', 'Extra Strong'),
('mod.extra_strong', 'zh-Hans-SG', '特浓'),
('mod.extra_strong', 'ms-SG', 'Sangat Kuat'),
('mod.extra_strong', 'ta-SG', 'கூடுதல் வலிமை'),

-- Less Strong localizations
('mod.less_strong', 'en-SG', 'Less Strong'),
('mod.less_strong', 'zh-Hans-SG', '淡'),
('mod.less_strong', 'ms-SG', 'Kurang Kuat'),
('mod.less_strong', 'ta-SG', 'குறைந்த வலிமை'),

-- Modification group localizations
('modgroup.drink_sweetness', 'en-SG', 'Sweetness Options'),
('modgroup.drink_sweetness', 'zh-Hans-SG', '甜度选择'),
('modgroup.drink_sweetness', 'ms-SG', 'Pilihan Kemanisan'),
('modgroup.drink_sweetness', 'ta-SG', 'இனிப்பு விருப்பங்கள்'),

('modgroup.drink_strength', 'en-SG', 'Strength Options'),
('modgroup.drink_strength', 'zh-Hans-SG', '浓度选择'),
('modgroup.drink_strength', 'ms-SG', 'Pilihan Kekuatan'),
('modgroup.drink_strength', 'ta-SG', 'வலிமை விருப்பங்கள்');

-- Update modifications to reference localization keys
UPDATE modifications SET localization_key = 'mod.' || id WHERE localization_key IS NULL;
UPDATE modification_groups SET localization_key = 'modgroup.' || id WHERE localization_key IS NULL;
