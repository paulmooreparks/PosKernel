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

namespace PosKernel.AI.Interfaces
{
    /// <summary>
    /// Interface for displaying chat messages.
    /// </summary>
    public interface IChatDisplay
    {
        /// <summary>
        /// Shows a chat message.
        /// </summary>
        void ShowMessage(ChatMessage message);
        
        /// <summary>
        /// Gets user input.
        /// </summary>
        Task<string?> GetUserInputAsync(string prompt = "You: ");
        
        /// <summary>
        /// Shows a system status message.
        /// </summary>
        void ShowStatus(string message);
        
        /// <summary>
        /// Shows an error message.
        /// </summary>
        void ShowError(string message);
        
        /// <summary>
        /// Clears the display.
        /// </summary>
        void Clear();
    }
    
    /// <summary>
    /// Interface for displaying receipt information.
    /// </summary>
    public interface IReceiptDisplay
    {
        /// <summary>
        /// Updates the receipt display.
        /// </summary>
        void UpdateReceipt(Receipt receipt);
        
        /// <summary>
        /// Shows payment processing status.
        /// </summary>
        void ShowPaymentStatus(string status);
        
        /// <summary>
        /// Clears the receipt display.
        /// </summary>
        void Clear();
    }
    
    /// <summary>
    /// Interface for the overall UI layout.
    /// </summary>
    public interface IUserInterface
    {
        /// <summary>
        /// Gets the chat display component.
        /// </summary>
        IChatDisplay Chat { get; }
        
        /// <summary>
        /// Gets the receipt display component.
        /// </summary>
        IReceiptDisplay Receipt { get; }
        
        /// <summary>
        /// Initializes the UI.
        /// </summary>
        Task InitializeAsync();
        
        /// <summary>
        /// Runs the main UI loop.
        /// </summary>
        Task RunAsync();
        
        /// <summary>
        /// Shuts down the UI.
        /// </summary>
        Task ShutdownAsync();
    }
}
