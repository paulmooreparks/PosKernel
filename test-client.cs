using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PosKernel.Extensions.Restaurant.Client;

class TestClient
{
    static async Task Main()
    {
        try
        {
            Console.WriteLine("Testing Restaurant Extension Client connection...");

            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<TestClient>();

            var client = new RestaurantExtensionClient(logger);

            Console.WriteLine("Client created, testing connection...");

            var products = await client.SearchProductsAsync("kopi", 5);

            Console.WriteLine($"Success! Found {products.Count} products:");
            foreach (var product in products)
            {
                Console.WriteLine($"  - {product.Name}: ${product.Price}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Connection failed: {ex.Message}");
            Console.WriteLine($"Full exception: {ex}");
        }
    }
}
