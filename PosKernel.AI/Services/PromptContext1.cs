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

namespace PosKernel.AI.Services
{
    /// <summary>
    /// Context information used to build dynamic prompts efficiently.
    /// </summary>
    public class PromptContext
    {
        /// <summary>
        /// Recent conversation history formatted for display.
        /// </summary>
        public string? ConversationContext { get; set; }

        /// <summary>
        /// Summary of items currently in the cart.
        /// </summary>
        public string? CartItems { get; set; }

        /// <summary>
        /// Current order total as a formatted string.
        /// </summary>
        public string? CurrentTotal { get; set; }

        /// <summary>
        /// Store currency code.
        /// </summary>
        public string? Currency { get; set; }

        /// <summary>
        /// The customer's latest input.
        /// </summary>
        public string? UserInput { get; set; }

        /// <summary>
        /// Time of day for greeting prompts (morning, afternoon, evening).
        /// </summary>
        public string? TimeOfDay { get; set; }

        /// <summary>
        /// Current time formatted as HH:mm for greeting prompts.
        /// </summary>
        public string? CurrentTime { get; set; }
    }
}
