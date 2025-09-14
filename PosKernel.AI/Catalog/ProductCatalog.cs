//
// Copyright 2025 Paul Moore Parks and contributors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

using System;
using System.Collections.Generic;
using System.Linq;

namespace PosKernel.AI.Catalog
{
    /// <summary>
    /// Comprehensive product catalog for POS demonstrations.
    /// Represents a modern coffee shop/cafe with realistic items, pricing, and categories.
    /// </summary>
    public static class ProductCatalog
    {
        /// <summary>
        /// Complete product inventory with SKUs, names, prices, and metadata.
        /// </summary>
        public static readonly List<Product> Products = new()
        {
            // Hot Beverages
            new Product("COFFEE_SM", "Small Coffee", "Hot Beverages", 2.99m, "Classic drip coffee, 8oz", true),
            new Product("COFFEE_MD", "Medium Coffee", "Hot Beverages", 3.49m, "Classic drip coffee, 12oz", true),
            new Product("COFFEE_LG", "Large Coffee", "Hot Beverages", 3.99m, "Classic drip coffee, 16oz", true),
            new Product("ESPRESSO_SG", "Single Espresso", "Hot Beverages", 2.49m, "Single shot of espresso", true),
            new Product("ESPRESSO_DB", "Double Espresso", "Hot Beverages", 3.49m, "Double shot of espresso", true),
            new Product("CAPPUCCINO", "Cappuccino", "Hot Beverages", 4.49m, "Espresso with steamed milk and foam", true),
            new Product("LATTE", "Caffe Latte", "Hot Beverages", 4.99m, "Espresso with steamed milk", true),
            new Product("MOCHA", "Caffe Mocha", "Hot Beverages", 5.49m, "Espresso with chocolate and steamed milk", true),
            new Product("AMERICANO", "Americano", "Hot Beverages", 3.99m, "Espresso with hot water", true),
            new Product("MACCHIATO", "Caramel Macchiato", "Hot Beverages", 5.99m, "Espresso with caramel and steamed milk", true),
            new Product("TEA_BLACK", "Black Tea", "Hot Beverages", 2.49m, "Premium black tea", true),
            new Product("TEA_GREEN", "Green Tea", "Hot Beverages", 2.49m, "Premium green tea", true),
            new Product("TEA_HERBAL", "Herbal Tea", "Hot Beverages", 2.79m, "Caffeine-free herbal blend", true),
            new Product("CHAI_LATTE", "Chai Latte", "Hot Beverages", 4.79m, "Spiced tea with steamed milk", true),
            new Product("HOT_CHOC", "Hot Chocolate", "Hot Beverages", 3.99m, "Rich hot chocolate with whipped cream", true),

            // Cold Beverages
            new Product("ICED_COFFEE", "Iced Coffee", "Cold Beverages", 3.49m, "Cold-brewed coffee over ice", true),
            new Product("COLD_BREW", "Cold Brew", "Cold Beverages", 3.99m, "Slow-steeped cold brew concentrate", true),
            new Product("FRAPPUCCINO", "Frappuccino", "Cold Beverages", 5.99m, "Blended coffee drink with ice", true),
            new Product("ICED_TEA", "Iced Tea", "Cold Beverages", 2.99m, "Fresh brewed iced tea", true),
            new Product("LEMONADE", "Fresh Lemonade", "Cold Beverages", 3.49m, "Made-to-order fresh lemonade", true),
            new Product("SMOOTHIE_BERRY", "Berry Smoothie", "Cold Beverages", 6.49m, "Mixed berry smoothie with yogurt", true),
            new Product("SMOOTHIE_MANGO", "Mango Smoothie", "Cold Beverages", 6.49m, "Fresh mango smoothie", true),
            new Product("WATER_BOTTLE", "Bottled Water", "Cold Beverages", 1.99m, "16oz premium spring water", false),
            new Product("SPARKLING_WATER", "Sparkling Water", "Cold Beverages", 2.49m, "Naturally sparkling mineral water", false),
            new Product("SODA_COKE", "Coca-Cola", "Cold Beverages", 2.79m, "Classic Coca-Cola", false),
            new Product("SODA_PEPSI", "Pepsi", "Cold Beverages", 2.79m, "Pepsi cola", false),
            new Product("JUICE_ORANGE", "Orange Juice", "Cold Beverages", 3.99m, "Fresh-squeezed orange juice", false),
            new Product("JUICE_APPLE", "Apple Juice", "Cold Beverages", 3.79m, "Pure apple juice", false),

            // Bakery Items
            new Product("MUFFIN_BLUEBERRY", "Blueberry Muffin", "Bakery", 2.49m, "Fresh blueberry muffin", false),
            new Product("MUFFIN_CHOCOLATE", "Chocolate Chip Muffin", "Bakery", 2.49m, "Double chocolate chip muffin", false),
            new Product("MUFFIN_BRAN", "Bran Muffin", "Bakery", 2.29m, "Healthy bran muffin with raisins", false),
            new Product("CROISSANT_PLAIN", "Plain Croissant", "Bakery", 2.99m, "Buttery French croissant", false),
            new Product("CROISSANT_CHOC", "Chocolate Croissant", "Bakery", 3.49m, "Pain au chocolat", false),
            new Product("CROISSANT_ALMOND", "Almond Croissant", "Bakery", 3.99m, "Croissant with almond cream", false),
            new Product("DANISH_CHEESE", "Cheese Danish", "Bakery", 3.29m, "Sweet cheese-filled pastry", false),
            new Product("DANISH_FRUIT", "Fruit Danish", "Bakery", 3.29m, "Seasonal fruit Danish", false),
            new Product("BAGEL_PLAIN", "Plain Bagel", "Bakery", 1.99m, "Fresh-baked plain bagel", false),
            new Product("BAGEL_SESAME", "Sesame Bagel", "Bakery", 2.19m, "Bagel with sesame seeds", false),
            new Product("BAGEL_EVERYTHING", "Everything Bagel", "Bakery", 2.39m, "Bagel with everything seasoning", false),
            new Product("SCONE_VANILLA", "Vanilla Scone", "Bakery", 2.79m, "Classic vanilla scone", false),
            new Product("SCONE_CRANBERRY", "Cranberry Scone", "Bakery", 2.99m, "Cranberry orange scone", false),

            // Breakfast Items
            new Product("TOAST_AVOCADO", "Avocado Toast", "Breakfast", 8.99m, "Multigrain toast with fresh avocado", false),
            new Product("BREAKFAST_BURRITO", "Breakfast Burrito", "Breakfast", 7.99m, "Eggs, cheese, and potatoes wrapped in tortilla", false),
            new Product("OATMEAL", "Steel-Cut Oatmeal", "Breakfast", 4.99m, "Hearty steel-cut oats with toppings", false),
            new Product("YOGURT_PARFAIT", "Greek Yogurt Parfait", "Breakfast", 5.99m, "Greek yogurt with granola and berries", false),
            new Product("BREAKFAST_SANDWICH", "Breakfast Sandwich", "Breakfast", 6.49m, "Egg, cheese, and bacon on English muffin", false),

            // Lunch Items
            new Product("SANDWICH_TURKEY", "Turkey Club Sandwich", "Lunch", 9.99m, "Turkey, bacon, lettuce, tomato on sourdough", false),
            new Product("SANDWICH_HAM", "Ham & Swiss Sandwich", "Lunch", 8.99m, "Honey ham and Swiss cheese on rye", false),
            new Product("SANDWICH_VEGGIE", "Veggie Sandwich", "Lunch", 7.99m, "Fresh vegetables with hummus on whole grain", false),
            new Product("WRAP_CHICKEN", "Chicken Caesar Wrap", "Lunch", 9.49m, "Grilled chicken with Caesar dressing in tortilla", false),
            new Product("WRAP_VEGGIE", "Veggie Wrap", "Lunch", 8.49m, "Fresh vegetables and hummus in spinach tortilla", false),
            new Product("SALAD_CAESAR", "Caesar Salad", "Lunch", 8.99m, "Romaine lettuce with Caesar dressing and croutons", false),
            new Product("SALAD_GREEK", "Greek Salad", "Lunch", 9.49m, "Mixed greens with feta, olives, and Greek dressing", false),
            new Product("SOUP_TOMATO", "Tomato Basil Soup", "Lunch", 5.99m, "Creamy tomato soup with fresh basil", false),
            new Product("SOUP_CHICKEN", "Chicken Noodle Soup", "Lunch", 6.49m, "Classic chicken noodle soup", false),

            // Snacks & Treats
            new Product("COOKIE_CHOC", "Chocolate Chip Cookie", "Snacks", 1.99m, "Fresh-baked chocolate chip cookie", false),
            new Product("COOKIE_OATMEAL", "Oatmeal Cookie", "Snacks", 1.99m, "Oatmeal raisin cookie", false),
            new Product("BROWNIE", "Fudge Brownie", "Snacks", 2.99m, "Rich chocolate fudge brownie", false),
            new Product("CAKE_SLICE", "Cake Slice", "Snacks", 4.49m, "Daily selection of cake slice", false),
            new Product("CHIPS_KETTLE", "Kettle Chips", "Snacks", 2.49m, "Artisanal kettle-cooked chips", false),
            new Product("PRETZELS", "Soft Pretzel", "Snacks", 3.99m, "Warm soft pretzel with salt", false),
            new Product("NUTS_MIXED", "Mixed Nuts", "Snacks", 3.49m, "Premium mixed nuts", false),
            new Product("GRANOLA_BAR", "Granola Bar", "Snacks", 2.29m, "Organic granola bar", false),

            // Retail Items
            new Product("COFFEE_BEANS_1LB", "Coffee Beans (1lb)", "Retail", 12.99m, "House blend coffee beans", false),
            new Product("COFFEE_BEANS_ESPRESSO", "Espresso Beans (1lb)", "Retail", 14.99m, "Dark roast espresso beans", false),
            new Product("TEA_BOX", "Tea Box", "Retail", 8.99m, "Selection of premium tea bags", false),
            new Product("MUG_CERAMIC", "Ceramic Mug", "Retail", 9.99m, "12oz ceramic coffee mug", false),
            new Product("TUMBLER", "Travel Tumbler", "Retail", 15.99m, "Insulated 16oz travel tumbler", false),

            // Gift Items
            new Product("GIFT_CARD_25", "$25 Gift Card", "Gift Cards", 25.00m, "Electronic gift card worth $25", false),
            new Product("GIFT_CARD_50", "$50 Gift Card", "Gift Cards", 50.00m, "Electronic gift card worth $50", false),
            new Product("GIFT_CARD_100", "$100 Gift Card", "Gift Cards", 100.00m, "Electronic gift card worth $100", false),
        };

        /// <summary>
        /// Gets products by category.
        /// </summary>
        public static List<Product> GetByCategory(string category)
        {
            return Products.Where(p => p.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        /// <summary>
        /// Finds products by SKU pattern or name search.
        /// </summary>
        public static List<Product> Search(string searchTerm)
        {
            var term = searchTerm.ToLowerInvariant();
            return Products.Where(p => 
                p.Sku.ToLowerInvariant().Contains(term) ||
                p.Name.ToLowerInvariant().Contains(term) ||
                p.Description.ToLowerInvariant().Contains(term)
            ).ToList();
        }

        /// <summary>
        /// Gets a random selection of products for demonstrations.
        /// </summary>
        public static List<Product> GetRandomSelection(int count = 5)
        {
            var random = new Random();
            return Products.OrderBy(x => random.Next()).Take(count).ToList();
        }

        /// <summary>
        /// Gets popular/frequently ordered items.
        /// </summary>
        public static List<Product> GetPopularItems()
        {
            return new List<Product>
            {
                Products.First(p => p.Sku == "COFFEE_LG"),      // Large Coffee - most popular
                Products.First(p => p.Sku == "LATTE"),          // Latte - second most popular  
                Products.First(p => p.Sku == "MUFFIN_BLUEBERRY"), // Blueberry Muffin
                Products.First(p => p.Sku == "BREAKFAST_SANDWICH"), // Breakfast Sandwich
                Products.First(p => p.Sku == "SANDWICH_TURKEY"),  // Turkey Club
                Products.First(p => p.Sku == "ICED_COFFEE"),     // Iced Coffee
                Products.First(p => p.Sku == "CROISSANT_PLAIN"),  // Plain Croissant
                Products.First(p => p.Sku == "CAPPUCCINO"),      // Cappuccino
                Products.First(p => p.Sku == "BAGEL_EVERYTHING"), // Everything Bagel
                Products.First(p => p.Sku == "COOKIE_CHOC")      // Chocolate Chip Cookie
            };
        }

        /// <summary>
        /// Gets high-value items for fraud detection demos.
        /// </summary>
        public static List<Product> GetHighValueItems()
        {
            return Products.Where(p => p.Price > 10.00m).ToList();
        }
    }

    /// <summary>
    /// Represents a product in the catalog.
    /// </summary>
    public record Product(
        string Sku,
        string Name, 
        string Category,
        decimal Price,
        string Description,
        bool RequiresPreparation
    );
}
