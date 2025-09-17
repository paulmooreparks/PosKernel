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

namespace PosKernel.AI.Services
{
    /// <summary>
    /// Configuration for AI personality behavior and responses.
    /// Contains metadata about personality characteristics, not prompt content.
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
        /// </summary>
        public string CultureCode { get; set; } = "en-US";

        /// <summary>
        /// Gets or sets the currency (e.g., "USD", "SGD").
        /// </summary>
        public string Currency { get; set; } = "USD";
    }

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

    /// <summary>
    /// Factory for creating different AI personalities for various store types and cultures.
    /// Loads personality prompts from Markdown files for better maintainability.
    /// </summary>
    public static class AiPersonalityFactory
    {
        private static readonly string PromptsBasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts");
        private static readonly Dictionary<string, string> _promptCache = new();

        /// <summary>
        /// Creates a personality configuration based on the specified type.
        /// </summary>
        /// <param name="personalityType">The type of personality to create.</param>
        /// <returns>A configured personality instance.</returns>
        public static AiPersonalityConfig CreatePersonality(PersonalityType personalityType)
        {
            return personalityType switch
            {
                PersonalityType.SingaporeanKopitiamUncle => CreateSingaporeanKopitiamUncle(),
                PersonalityType.AmericanBarista => CreateAmericanBarista(),
                PersonalityType.FrenchBoulanger => CreateFrenchBoulanger(),
                PersonalityType.JapaneseConbiniClerk => CreateJapaneseConbiniClerk(),
                PersonalityType.IndianChaiWala => CreateIndianChaiWala(),
                PersonalityType.GenericCashier => CreateGenericCashier(),
                _ => throw new ArgumentOutOfRangeException(nameof(personalityType), personalityType, "Unknown personality type")
            };
        }

        /// <summary>
        /// Clears the prompt cache, forcing prompts to be reloaded from disk on next access.
        /// Useful for development when modifying prompt files.
        /// </summary>
        public static void ClearPromptCache()
        {
            _promptCache.Clear();
        }

        /// <summary>
        /// Loads a prompt template from a Markdown file with caching.
        /// </summary>
        /// <param name="personalityFolder">The personality folder name (e.g., "AmericanBarista").</param>
        /// <param name="promptType">The prompt type ("greeting" or "ordering").</param>
        /// <returns>The prompt content as a string.</returns>
        private static string LoadPromptTemplate(string personalityFolder, string promptType)
        {
            var cacheKey = $"{personalityFolder}_{promptType}";
            
            if (_promptCache.TryGetValue(cacheKey, out var cachedPrompt))
            {
                return cachedPrompt;
            }

            var promptPath = Path.Combine(PromptsBasePath, personalityFolder, $"{promptType}.md");
            
            if (!File.Exists(promptPath))
            {
                // Fallback to a generic prompt if the specific one doesn't exist
                var fallbackPath = Path.Combine(PromptsBasePath, "GenericCashier", $"{promptType}.md");
                if (File.Exists(fallbackPath))
                {
                    var fallbackContent = File.ReadAllText(fallbackPath);
                    _promptCache[cacheKey] = fallbackContent;
                    return fallbackContent;
                }
                
                // Ultimate fallback - inline prompt
                var inlinePrompt = promptType == "greeting" 
                    ? "You are a helpful assistant. Greet the customer and ask how you can help them."
                    : "You are processing a customer order. Help them with their request: '{userInput}'";
                    
                _promptCache[cacheKey] = inlinePrompt;
                return inlinePrompt;
            }

            var content = File.ReadAllText(promptPath);
            _promptCache[cacheKey] = content;
            return content;
        }

        /// <summary>
        /// Loads a prompt template from a Markdown file for the specified personality and prompt type.
        /// </summary>
        /// <param name="personalityType">The personality type.</param>
        /// <param name="promptType">The prompt type ("greeting" or "ordering").</param>
        /// <returns>The prompt content as a string.</returns>
        public static string LoadPrompt(PersonalityType personalityType, string promptType)
        {
            var personalityFolder = personalityType.ToString();
            return LoadPromptTemplate(personalityFolder, promptType);
        }

        /// <summary>
        /// Builds a complete prompt by combining the template with dynamic context.
        /// This is more efficient than string replacement for dynamic content.
        /// </summary>
        /// <param name="personalityType">The personality type.</param>
        /// <param name="promptType">The prompt type ("greeting" or "ordering").</param>
        /// <param name="context">Dynamic context to inject into the prompt.</param>
        /// <returns>The complete prompt with context.</returns>
        public static string BuildPrompt(PersonalityType personalityType, string promptType, PromptContext context)
        {
            var template = LoadPrompt(personalityType, promptType);
            
            // For ordering prompts, inject dynamic context efficiently
            if (promptType == "ordering" && context != null)
            {
                return BuildOrderingPrompt(template, context);
            }
            
            // For greeting prompts, simple replacement is fine since it's only done once
            if (promptType == "greeting" && context != null)
            {
                return template
                    .Replace("{timeOfDay}", context.TimeOfDay ?? "")
                    .Replace("{currentTime}", context.CurrentTime ?? "");
            }
            
            return template;
        }

        /// <summary>
        /// Efficiently builds an ordering prompt by prepending dynamic context.
        /// This avoids expensive string replacement operations.
        /// </summary>
        private static string BuildOrderingPrompt(string template, PromptContext context)
        {
            var sb = new System.Text.StringBuilder(template.Length + 500); // Pre-allocate reasonable size
            
            // Start with the base template (remove placeholder sections)
            var cleanTemplate = RemovePlaceholderSections(template);
            sb.AppendLine(cleanTemplate);
            sb.AppendLine();
            
            // Append dynamic context header
            sb.AppendLine("## CURRENT SESSION CONTEXT:");
            
            if (!string.IsNullOrEmpty(context.ConversationContext))
            {
                sb.AppendLine("### Recent Conversation:");
                sb.AppendLine(context.ConversationContext);
                sb.AppendLine();
            }
            
            sb.AppendLine("### Current Order Status:");
            sb.AppendLine($"- Items in cart: {context.CartItems ?? "none"}");
            sb.AppendLine($"- Current total: ${context.CurrentTotal ?? "0.00"}");
            sb.AppendLine($"- Currency: {context.Currency ?? "USD"}");
            sb.AppendLine();
            
            sb.AppendLine($"**CUSTOMER JUST SAID:** '{context.UserInput ?? ""}'");
            sb.AppendLine();
            sb.AppendLine("Process this request using your cultural knowledge and tools.");
            
            return sb.ToString();
        }

        /// <summary>
        /// Removes placeholder sections from the template since we're building context programmatically.
        /// </summary>
        private static string RemovePlaceholderSections(string template)
        {
            // Remove the placeholder sections that we'll replace with programmatic context
            var lines = template.Split('\n');
            var result = new System.Text.StringBuilder();
            bool skipSection = false;
            
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                
                // Skip sections that contain placeholders we're replacing programmatically
                if (trimmedLine.Contains("{conversationContext}") || 
                    trimmedLine.Contains("{cartItems}") ||
                    trimmedLine.Contains("{currentTotal}") ||
                    trimmedLine.Contains("{currency}") ||
                    trimmedLine.Contains("{userInput}") ||
                    trimmedLine == "## Conversation History:" ||
                    trimmedLine == "## Current Order Status:")
                {
                    skipSection = true;
                    continue;
                }
                
                // End skipping when we hit the next major section
                if (trimmedLine.StartsWith("## ") && !trimmedLine.Contains("Conversation History") && !trimmedLine.Contains("Current Order Status"))
                {
                    skipSection = false;
                }
                
                if (!skipSection && !trimmedLine.Contains("{"))
                {
                    result.AppendLine(line);
                }
            }
            
            return result.ToString();
        }

        private static AiPersonalityConfig CreateAmericanBarista()
        {
            return new AiPersonalityConfig
            {
                Type = PersonalityType.AmericanBarista,
                StaffTitle = "Barista",
                VenueTitle = "Coffee Shop",
                SupportedLanguages = new List<string> { "English" },
                CultureCode = "en-US",
                Currency = "USD"
            };
        }

        private static AiPersonalityConfig CreateSingaporeanKopitiamUncle()
        {
            return new AiPersonalityConfig
            {
                Type = PersonalityType.SingaporeanKopitiamUncle,
                StaffTitle = "Uncle",
                VenueTitle = "Kopitiam",
                SupportedLanguages = new List<string> { "English", "Mandarin", "Cantonese", "Hokkien", "Hakka", "Teochew", "Malay", "Tamil", "Punjabi", "Bangladeshi" },
                CultureCode = "en-SG",
                Currency = "SGD"
            };
        }

        private static AiPersonalityConfig CreateFrenchBoulanger()
        {
            return new AiPersonalityConfig
            {
                Type = PersonalityType.FrenchBoulanger,
                StaffTitle = "Boulanger",
                VenueTitle = "Boulangerie",
                SupportedLanguages = new List<string> { "French", "English" },
                CultureCode = "fr-FR",
                Currency = "EUR"
            };
        }

        private static AiPersonalityConfig CreateJapaneseConbiniClerk()
        {
            return new AiPersonalityConfig
            {
                Type = PersonalityType.JapaneseConbiniClerk,
                StaffTitle = "Clerk",
                VenueTitle = "Convenience Store",
                SupportedLanguages = new List<string> { "Japanese", "English" },
                CultureCode = "ja-JP",
                Currency = "JPY"
            };
        }

        private static AiPersonalityConfig CreateIndianChaiWala()
        {
            return new AiPersonalityConfig
            {
                Type = PersonalityType.IndianChaiWala,
                StaffTitle = "Chai Wala",
                VenueTitle = "Chai Stall",
                SupportedLanguages = new List<string> { "Hindi", "English", "Punjabi", "Bengali" },
                CultureCode = "hi-IN",
                Currency = "INR"
            };
        }

        private static AiPersonalityConfig CreateGenericCashier()
        {
            return new AiPersonalityConfig
            {
                Type = PersonalityType.GenericCashier,
                StaffTitle = "Cashier",
                VenueTitle = "Store",
                SupportedLanguages = new List<string> { "English" },
                CultureCode = "en-US",
                Currency = "USD"
            };
        }
    }

    /// <summary>
    /// Available personality types for different store cultures and contexts.
    /// </summary>
    public enum PersonalityType
    {
        /// <summary>
        /// American coffee shop barista - friendly, enthusiastic, professional.
        /// </summary>
        AmericanBarista,
        
        /// <summary>
        /// Singaporean kopitiam uncle - efficient, culturally intelligent, multilingual.
        /// </summary>
        SingaporeanKopitiamUncle,
        
        /// <summary>
        /// French baker - artisanal pride, warm hospitality, quality-focused.
        /// </summary>
        FrenchBoulanger,
        
        /// <summary>
        /// Japanese convenience store clerk - extremely polite, efficient, courteous.
        /// </summary>
        JapaneseConbiniClerk,
        
        /// <summary>
        /// Indian chai seller - warm, conversational, chai-focused.
        /// </summary>
        IndianChaiWala,
        
        /// <summary>
        /// Generic cashier - professional, helpful, culturally neutral.
        /// </summary>
        GenericCashier
    }
}
