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

using PosKernel.AI.Models;
using PosKernel.AI.Interfaces;

namespace PosKernel.AI.UI.Console
{
    /// <summary>
    /// Clean console-based chat display.
    /// </summary>
    public class ConsoleChatDisplay : IChatDisplay
    {
        private readonly bool _showDebug;

        /// <summary>
        /// Initializes a new instance of the ConsoleChatDisplay.
        /// </summary>
        /// <param name="showDebug">Whether to show debug messages.</param>
        public ConsoleChatDisplay(bool showDebug = false)
        {
            _showDebug = showDebug;
        }

        /// <inheritdoc/>
        public void ShowMessage(ChatMessage message)
        {
            if (!message.ShowInCleanMode && !_showDebug)
            {
                return;
            }

            var prefix = message.Sender switch
            {
                "Customer" => "👤 You: ",
                _ when message.IsSystem => "🔧 ",
                _ => $"🤖 {message.Sender}: "
            };

            if (message.IsSystem)
            {
                System.Console.ForegroundColor = ConsoleColor.DarkGray;
            }

            System.Console.WriteLine($"{prefix}{message.Content}");

            if (message.IsSystem)
            {
                System.Console.ResetColor();
            }
        }

        /// <inheritdoc/>
        public async Task<string?> GetUserInputAsync(string prompt = "You: ")
        {
            System.Console.Write($"👤 {prompt}");
            return await Task.FromResult(System.Console.ReadLine()?.Trim());
        }

        /// <inheritdoc/>
        public void ShowStatus(string message)
        {
            System.Console.ForegroundColor = ConsoleColor.Green;
            System.Console.WriteLine($"✅ {message}");
            System.Console.ResetColor();
        }

        /// <inheritdoc/>
        public void ShowError(string message)
        {
            System.Console.ForegroundColor = ConsoleColor.Red;
            System.Console.WriteLine($"❌ {message}");
            System.Console.ResetColor();
        }

        /// <inheritdoc/>
        public void Clear()
        {
            System.Console.Clear();
        }
    }

    /// <summary>
    /// Clean console-based receipt display.
    /// </summary>
    public class ConsoleReceiptDisplay : IReceiptDisplay
    {
        private readonly int _width;

        /// <summary>
        /// Initializes a new instance of the ConsoleReceiptDisplay.
        /// </summary>
        /// <param name="width">Width of the receipt display.</param>
        public ConsoleReceiptDisplay(int width = 35)
        {
            _width = width;
        }

        /// <inheritdoc/>
        public void UpdateReceipt(Receipt receipt)
        {
            // Move cursor to receipt area (right side of screen)
            var left = System.Console.WindowWidth - _width - 2;
            var top = 3;

            try
            {
                // Save current cursor position
                var currentLeft = System.Console.CursorLeft;
                var currentTop = System.Console.CursorTop;

                // Draw receipt box
                DrawReceiptBox(receipt, left, top);

                // Restore cursor position
                System.Console.SetCursorPosition(currentLeft, currentTop);
            }
            catch
            {
                // Fallback for terminals that don't support cursor positioning
                DrawSimpleReceipt(receipt);
            }
        }

        /// <inheritdoc/>
        public void ShowPaymentStatus(string status)
        {
            System.Console.ForegroundColor = ConsoleColor.Yellow;
            System.Console.WriteLine($"💳 {status}");
            System.Console.ResetColor();
        }

        /// <inheritdoc/>
        public void Clear()
        {
            // Clear receipt area if possible
            try
            {
                var left = System.Console.WindowWidth - _width - 2;
                for (int i = 3; i < 20; i++)
                {
                    System.Console.SetCursorPosition(left, i);
                    System.Console.Write(new string(' ', _width));
                }
            }
            catch
            {
                // Fallback - can't clear specific area
            }
        }

        private void DrawReceiptBox(Receipt receipt, int left, int top)
        {
            var lines = new List<string>();
            var contentWidth = _width - 4;
            
            // Header
            lines.Add("┌─" + new string('─', _width - 4) + "─┐");
            lines.Add($"│ {receipt.Store.Name.PadRight(contentWidth)} │");
            lines.Add($"│ Transaction #{receipt.TransactionId.PadRight(contentWidth - 14)} │");
            lines.Add("├─" + new string('─', _width - 4) + "─┤");
            
            // Items
            if (receipt.Items.Any())
            {
                foreach (var item in receipt.Items)
                {
                    var itemLine = $"{item.Quantity}x {item.ProductName}";
                    if (itemLine.Length > _width - 8)
                    {
                        itemLine = itemLine.Substring(0, _width - 11) + "...";
                    }
                    var priceLine = $"${item.LineTotal:F2}";
                    var spacing = _width - 4 - itemLine.Length - priceLine.Length;
                    lines.Add($"│ {itemLine}{new string(' ', Math.Max(1, spacing))}{priceLine} │");
                    
                    if (!string.IsNullOrEmpty(item.PreparationNotes))
                    {
                        var noteLine = $"  ({item.PreparationNotes})";
                        if (noteLine.Length > _width - 4)
                        {
                            noteLine = noteLine.Substring(0, _width - 7) + "...";
                        }
                        lines.Add($"│ {noteLine.PadRight(contentWidth)} │");
                    }
                }
                
                lines.Add("├─" + new string('─', _width - 4) + "─┤");
                
                // Total
                var totalLine = $"TOTAL: ${receipt.Total:F2}";
                var totalSpacing = _width - 4 - totalLine.Length;
                lines.Add($"│{new string(' ', totalSpacing)}{totalLine} │");
            }
            else
            {
                lines.Add($"│ {"(Empty Order)".PadRight(contentWidth)} │");
            }
            
            lines.Add("└─" + new string('─', _width - 4) + "─┘");
            
            // Status
            var status = receipt.Status switch
            {
                PaymentStatus.Building => "Building Order",
                PaymentStatus.ReadyForPayment => "Ready for Payment",
                PaymentStatus.Completed => "PAID",
                _ => receipt.Status.ToString()
            };
            lines.Add($"Status: {status}");

            // Draw all lines
            for (int i = 0; i < lines.Count; i++)
            {
                if (top + i < System.Console.WindowHeight - 1)
                {
                    System.Console.SetCursorPosition(left, top + i);
                    System.Console.Write(lines[i]);
                }
            }
        }

        private void DrawSimpleReceipt(Receipt receipt)
        {
            System.Console.WriteLine();
            System.Console.WriteLine($"💰 Current Order ({receipt.Store.Currency}):");
            System.Console.WriteLine("─────────────────────────────");
            
            if (receipt.Items.Any())
            {
                foreach (var item in receipt.Items)
                {
                    var prep = !string.IsNullOrEmpty(item.PreparationNotes) ? $" ({item.PreparationNotes})" : "";
                    System.Console.WriteLine($"{item.Quantity}x {item.ProductName}{prep} - ${item.LineTotal:F2}");
                }
                System.Console.WriteLine("─────────────────────────────");
                System.Console.WriteLine($"TOTAL: ${receipt.Total:F2}");
            }
            else
            {
                System.Console.WriteLine("(Empty Order)");
            }
            System.Console.WriteLine();
        }
    }

    /// <summary>
    /// Clean console-based user interface.
    /// </summary>
    public class ConsoleUserInterface : IUserInterface
    {
        /// <inheritdoc/>
        public IChatDisplay Chat { get; private set; }
        
        /// <inheritdoc/>
        public IReceiptDisplay Receipt { get; private set; }
        
        private bool _showDebug;

        /// <summary>
        /// Initializes a new instance of the ConsoleUserInterface.
        /// </summary>
        /// <param name="showDebug">Whether to show debug information.</param>
        public ConsoleUserInterface(bool showDebug = false)
        {
            _showDebug = showDebug;
            Chat = new ConsoleChatDisplay(showDebug);
            Receipt = new ConsoleReceiptDisplay();
        }

        /// <inheritdoc/>
        public async Task InitializeAsync()
        {
            System.Console.Clear();
            System.Console.OutputEncoding = System.Text.Encoding.UTF8;
            
            if (System.Console.WindowWidth > 100)
            {
                // Wide screen - show receipt on the right
                ShowHeader();
            }
            else
            {
                // Narrow screen - simpler layout
                ShowSimpleHeader();
            }
            
            await Task.CompletedTask;
        }

        /// <inheritdoc/>
        public async Task RunAsync()
        {
            // This would be implemented by the main program
            // The UI just provides the display interfaces
            await Task.CompletedTask;
        }

        /// <inheritdoc/>
        public async Task ShutdownAsync()
        {
            System.Console.ResetColor();
            await Task.CompletedTask;
        }

        private void ShowHeader()
        {
            System.Console.WriteLine("🤖 POS Kernel AI Demo - Clean Interface");
            System.Console.WriteLine(new string('═', System.Console.WindowWidth - 40));
            System.Console.WriteLine();
        }

        private void ShowSimpleHeader()
        {
            System.Console.WriteLine("🤖 POS Kernel AI Demo");
            System.Console.WriteLine("══════════════════════");
            System.Console.WriteLine();
        }
    }
}
