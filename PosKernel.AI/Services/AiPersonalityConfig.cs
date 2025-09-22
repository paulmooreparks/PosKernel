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

using PosKernel.Abstractions;
using PosKernel.Abstractions.Services;
using PosKernel.AI.Services;

namespace PosKernel.AI.Services
{
    /// <summary>
    /// Configuration for AI personality behavior and responses.
    /// Contains metadata about personality characteristics, not prompt content.
    /// ARCHITECTURAL PRINCIPLE: Configuration values come from services, not hardcoded defaults.
    /// </summary>
    public class AiPersonalityConfig
    {
        /// <summary>
        /// Gets or sets the personality type.
        /// </summary>
        public PersonalityType Type { get; set; }

        /// <summary>
        /// Gets or sets the staff title (e.g., "Barista", "Uncle", "Clerk").
        /// </summary>
        public string StaffTitle { get; set; } = "";

        /// <summary>
        /// Gets or sets the venue title (e.g., "Coffee Shop", "Kopitiam", "Store").
        /// </summary>
        public string VenueTitle { get; set; } = "";

        /// <summary>
        /// Gets or sets the supported languages.
        /// </summary>
        public List<string> SupportedLanguages { get; set; } = new();

        /// <summary>
        /// Gets or sets the culture code (e.g., "en-US", "en-SG").
        /// ARCHITECTURAL PRINCIPLE: No hardcoded defaults - must come from store configuration service.
        /// </summary>
        public string CultureCode { get; set; } = "";

        /// <summary>
        /// Gets or sets the currency (e.g., "USD", "SGD").
        /// ARCHITECTURAL PRINCIPLE: No hardcoded currency assumptions - must come from store configuration service.
        /// </summary>
        public string Currency { get; set; } = "";
    }
}
