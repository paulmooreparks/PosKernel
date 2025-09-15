using PosKernel.AI.Tools;
using PosKernel.AI.Services;

namespace PosKernel.AI
{
    /// <summary>
    /// Test the menu context loading functionality.
    /// </summary>
    public static class MenuContextTest
    {
        /// <summary>
        /// Tests the load_menu_context tool execution.
        /// </summary>
        public static async Task TestMenuContextLoadingAsync()
        {
            Console.WriteLine("📖 MENU CONTEXT LOADING TEST");
            Console.WriteLine("=============================");
            
            // Setup services
            var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<PosToolsProvider>.Instance;
            var catalog = new KopitiamProductCatalogService();
            var toolsProvider = new PosToolsProvider(catalog, logger);
            
            // Create a test transaction
            var transaction = new PosKernel.Abstractions.Transaction
            {
                Id = PosKernel.Abstractions.TransactionId.New(),
                State = PosKernel.Abstractions.TransactionState.Building,
                Currency = "SGD"
            };
            
            Console.WriteLine("🔧 Testing load_menu_context tool...");
            
            // Create MCP tool call for loading menu context
            var toolCall = new McpToolCall
            {
                Id = "test-menu-" + Guid.NewGuid().ToString()[..8],
                FunctionName = "load_menu_context",
                Arguments = new Dictionary<string, System.Text.Json.JsonElement>
                {
                    ["include_categories"] = System.Text.Json.JsonSerializer.SerializeToElement(true)
                }
            };
            
            // Execute the tool
            var result = await toolsProvider.ExecuteToolAsync(toolCall, transaction);
            Console.WriteLine("📋 Uncle's Menu Knowledge:");
            Console.WriteLine(result);
            
            if (result.Contains("KOPITIAM MENU CONTEXT") && result.Contains("CULTURAL TRANSLATIONS"))
            {
                Console.WriteLine("\n✅ SUCCESS: Menu context loaded successfully!");
                Console.WriteLine("Uncle now has complete knowledge of:");
                Console.WriteLine("  • All available products and prices");
                Console.WriteLine("  • Product categories and descriptions");  
                Console.WriteLine("  • Cultural translations (teh si → teh c, etc.)");
                Console.WriteLine("  • Preparation instructions (kosong, siew dai, etc.)");
            }
            else
            {
                Console.WriteLine("\n❌ FAILED: Menu context incomplete or malformed");
            }
        }
    }
}
