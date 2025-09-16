using PosKernel.Abstractions;
using PosKernel.AI.Services;
using PosKernel.AI.Tools;

namespace PosKernel.AI
{
    /// <summary>
    /// Test the MCP tool execution directly to verify the fix.
    /// </summary>
    public static class McpToolTest
    {
        /// <summary>
        /// Tests the MCP add_item_to_transaction tool with kopitiam terms.
        /// </summary>
        public static async Task TestMcpToolExecutionAsync()
        {
            Console.WriteLine("🛠️  MCP TOOL EXECUTION TEST");
            Console.WriteLine("===========================");
            
            // Setup services
            var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<PosToolsProvider>.Instance;
            var catalog = new KopitiamProductCatalogService();
            var toolsProvider = new PosToolsProvider(catalog, logger);
            
            // Create a test transaction
            var transaction = new Transaction
            {
                Id = TransactionId.New(),
                State = TransactionState.Building,
                Currency = "SGD"
            };
            
            // Test cases with different confidence levels
            var testCases = new[]
            {
                new { Input = "kaya toast", Expected = "DISAMBIGUATION", Confidence = 0.3 }, // Low confidence should disambiguate
                new { Input = "roti kaya", Expected = "DISAMBIGUATION", Confidence = 0.3 }, // Cultural translation should still disambiguate 
                new { Input = "teh si kosong", Expected = "Teh C", Confidence = 0.8 }, // High confidence cultural term
                new { Input = "kaya toast", Expected = "Kaya Toast", Confidence = 0.8 }, // High confidence should auto-add
                new { Input = "roti kaya", Expected = "Kaya Toast", Confidence = 0.8 }, // High confidence cultural should auto-add
                new { Input = "coffee", Expected = "DISAMBIGUATION", Confidence = 0.3 } // Generic term needs clarification
            };
            
            foreach (var testCase in testCases)
            {
                Console.WriteLine($"\n🧪 Testing: '{testCase.Input}' (confidence: {testCase.Confidence})");
                
                // Create MCP tool call
                var toolCall = new McpToolCall
                {
                    Id = "test-" + Guid.NewGuid().ToString()[..8],
                    FunctionName = "add_item_to_transaction",
                    Arguments = new Dictionary<string, System.Text.Json.JsonElement>
                    {
                        ["item_description"] = System.Text.Json.JsonSerializer.SerializeToElement(testCase.Input),
                        ["quantity"] = System.Text.Json.JsonSerializer.SerializeToElement(1),
                        ["confidence"] = System.Text.Json.JsonSerializer.SerializeToElement(testCase.Confidence),
                        ["context"] = System.Text.Json.JsonSerializer.SerializeToElement("initial_order")
                    }
                };
                
                // Execute the tool
                var result = await toolsProvider.ExecuteToolAsync(toolCall, transaction);
                Console.WriteLine($"Result: {result}");
                
                // Check if it was added or requires disambiguation
                if (result.StartsWith("ADDED:") && testCase.Expected != "DISAMBIGUATION")
                {
                    if (result.Contains(testCase.Expected))
                    {
                        Console.WriteLine($"✅ SUCCESS: Auto-added '{testCase.Expected}' as expected");
                    }
                    else
                    {
                        Console.WriteLine($"⚠️  PARTIAL: Auto-added but expected '{testCase.Expected}', got different product");
                    }
                }
                else if (result.StartsWith("DISAMBIGUATION_NEEDED:"))
                {
                    if (testCase.Expected == "DISAMBIGUATION")
                    {
                        Console.WriteLine($"✅ SUCCESS: Required disambiguation as expected");
                    }
                    else
                    {
                        Console.WriteLine($"❌ FAILED: Required disambiguation instead of auto-adding '{testCase.Expected}'");
                    }
                }
                else
                {
                    Console.WriteLine($"⚠️  UNEXPECTED: {result}");
                }
            }
            
            // Show final transaction
            Console.WriteLine($"\n📋 Final Transaction:");
            Console.WriteLine($"Items: {transaction.Lines.Count}");
            foreach (var line in transaction.Lines)
            {
                Console.WriteLine($"  - {line.ProductId} x{line.Quantity} @ ${line.UnitPrice.ToDecimal():F2}");
            }
            Console.WriteLine($"Total: ${transaction.Total.ToDecimal():F2}");
        }
    }
}
