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

namespace PosKernel.AI.Models
{
    /// <summary>
    /// Represents a message in the chat conversation.
    /// </summary>
    public class ChatMessage
    {
        /// <summary>
        /// Gets or sets the sender of the message.
        /// </summary>
        public string Sender { get; set; } = "";
        
        /// <summary>
        /// Gets or sets the content of the message.
        /// </summary>
        public string Content { get; set; } = "";
        
        /// <summary>
        /// Gets or sets when the message was sent.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;
        
        /// <summary>
        /// Gets or sets whether this is a system/debug message.
        /// </summary>
        public bool IsSystem { get; set; } = false;
        
        /// <summary>
        /// Gets or sets whether this message should be shown in clean mode.
        /// </summary>
        public bool ShowInCleanMode { get; set; } = true;
    }
}
