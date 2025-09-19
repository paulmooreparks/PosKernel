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
        private static readonly string PromptsBasePath = GetPromptsPath();
        private static readonly Dictionary<string, string> _promptCache = new();
        private static readonly Dictionary<string, DateTime> _promptFileTimestamps = new();

        /// <summary>
        /// Gets the correct path to the Prompts directory.
        /// ARCHITECTURAL PRINCIPLE: FAIL FAST - No silent fallbacks if prompts directory not found.
        /// </summary>
        private static string GetPromptsPath()
        {
            // Try multiple possible locations for the Prompts directory
            var possiblePaths = new[]
            {
                // In the project source directory (development scenario)
                Path.Combine(Directory.GetCurrentDirectory(), "Prompts"),
                
                // From solution root
                Path.Combine(Directory.GetCurrentDirectory(), "PosKernel.AI", "Prompts"),
                
                // In the output directory (if prompts were copied)
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts")
            };

            foreach (var path in possiblePaths)
            {
                if (Directory.Exists(path))
                {
                    return path;
                }
            }

            // ARCHITECTURAL PRINCIPLE: FAIL FAST - No silent fallbacks
            var searchedPaths = string.Join("\n  - ", possiblePaths);
            throw new DirectoryNotFoundException(
                $"DESIGN DEFICIENCY: Prompts directory not found. AI personality system requires prompt files to function.\n" +
                $"Searched locations:\n  - {searchedPaths}\n\n" +
                $"Fix: Ensure Prompts directory exists with personality markdown files.\n" +
                $"Current directory: {Directory.GetCurrentDirectory()}\n" +
                $"Base directory: {AppDomain.CurrentDomain.BaseDirectory}");
        }

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
            _promptFileTimestamps.Clear();
        }

        /// <summary>
        /// Loads a prompt template from a Markdown file.
        /// ARCHITECTURAL PRINCIPLE: FAIL FAST - No silent fallbacks if prompt files missing.
        /// </summary>
        /// <param name="personalityFolder">The personality folder name (e.g., "AmericanBarista").</param>
        /// <param name="promptType">The prompt type ("greeting", "ordering", or "post-payment").</param>
        /// <returns>The prompt content as a string.</returns>
        private static string LoadPromptTemplate(string personalityFolder, string promptType)
        {
            var cacheKey = $"{personalityFolder}_{promptType}";
            var promptPath = Path.Combine(PromptsBasePath, personalityFolder, $"{promptType}.md");
            
            // Check if file exists and get timestamp
            if (File.Exists(promptPath))
            {
                var fileTimestamp = File.GetLastWriteTime(promptPath);
                
                // Check if we need to reload due to file change
                var shouldReload = !_promptCache.ContainsKey(cacheKey) || 
                                   !_promptFileTimestamps.TryGetValue(cacheKey, out var cachedTimestamp) ||
                                   fileTimestamp > cachedTimestamp;
                
                if (shouldReload)
                {
                    var content = File.ReadAllText(promptPath);
                    _promptCache[cacheKey] = content;
                    _promptFileTimestamps[cacheKey] = fileTimestamp;
                    return content;
                }
                
                // Return cached version if file hasn't changed
                if (_promptCache.TryGetValue(cacheKey, out var cachedPrompt))
                {
                    return cachedPrompt;
                }
            }
            
            // ARCHITECTURAL PRINCIPLE: FAIL FAST - No silent fallbacks to other personalities or inline prompts
            throw new FileNotFoundException(
                $"DESIGN DEFICIENCY: Personality prompt file not found: {promptPath}\n" +
                $"AI personality system requires specific prompt files for each personality type.\n\n" +
                $"Fix: Create the missing prompt file:\n" +
                $"  - Personality: {personalityFolder}\n" +
                $"  - Prompt type: {promptType}\n" +
                $"  - Expected path: {promptPath}\n\n" +
                $"Prompts directory: {PromptsBasePath}\n" +
                $"Directory exists: {Directory.Exists(Path.Combine(PromptsBasePath, personalityFolder))}\n" +
                $"Available files: {GetAvailableFiles(personalityFolder)}");
        }

        /// <summary>
        /// Helper method to list available files for debugging.
        /// </summary>
        private static string GetAvailableFiles(string personalityFolder)
        {
            try
            {
                var personalityPath = Path.Combine(PromptsBasePath, personalityFolder);
                if (Directory.Exists(personalityPath))
                {
                    var files = Directory.GetFiles(personalityPath, "*.md");
                    return files.Length > 0 ? string.Join(", ", files.Select(Path.GetFileName)) : "No .md files found";
                }
                return "Directory does not exist";
            }
            catch (Exception ex)
            {
                return $"Error listing files: {ex.Message}";
            }
        }

        /// <summary>
        /// Loads a prompt template from a Markdown file for the specified personality and prompt type.
        /// ARCHITECTURAL PRINCIPLE: FAIL FAST - No silent fallbacks.
        /// </summary>
        /// <param name="personalityType">The personality type.</param>
        /// <param name="promptType">The prompt type ("greeting", "ordering", or "post-payment").</param>
        /// <returns>The prompt content as a string.</returns>
        public static string LoadPrompt(PersonalityType personalityType, string promptType)
        {
            var personalityFolder = personalityType.ToString();
            return LoadPromptTemplate(personalityFolder, promptType);
        }

        /// <summary>
        /// Builds a complete prompt by combining the template with dynamic context.
        /// Uses StringBuilder for efficiency, avoiding expensive string replacement.
        /// ARCHITECTURAL PRINCIPLE: FAIL FAST - No silent fallbacks.
        /// </summary>
        /// <param name="personalityType">The personality type.</param>
        /// <param name="promptType">The prompt type ("greeting", "ordering", or "post-payment").</param>
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
            
            // For greeting prompts, use StringBuilder instead of string replacement
            if (promptType == "greeting" && context != null)
            {
                return BuildGreetingPrompt(template, context);
            }
            
            // For post-payment prompts, use simple context injection
            if (promptType == "post-payment" && context != null)
            {
                return BuildPostPaymentPrompt(template, context);
            }
            
            return template;
        }

        /// <summary>
        /// Efficiently builds a greeting prompt by appending dynamic context.
        /// Avoids expensive string replacement operations.
        /// </summary>
        private static string BuildGreetingPrompt(string template, PromptContext context)
        {
            var sb = new System.Text.StringBuilder(template.Length + 200);
            
            // Start with the base template
            sb.AppendLine(template);
            sb.AppendLine();
            
            // Append dynamic context
            sb.AppendLine("## CURRENT CONTEXT:");
            if (!string.IsNullOrEmpty(context.TimeOfDay))
            {
                sb.AppendLine($"- Time of day: {context.TimeOfDay}");
            }
            if (!string.IsNullOrEmpty(context.CurrentTime))
            {
                sb.AppendLine($"- Current time: {context.CurrentTime}");
            }
            
            return sb.ToString();
        }

        /// <summary>
        /// Efficiently builds a post-payment prompt by appending dynamic context.
        /// Avoids expensive string replacement operations.
        /// </summary>
        private static string BuildPostPaymentPrompt(string template, PromptContext context)
        {
            var sb = new System.Text.StringBuilder(template.Length + 200);
            
            // Start with the base template
            sb.AppendLine(template);
            sb.AppendLine();
            
            // Append dynamic context
            sb.AppendLine("## CURRENT CONTEXT:");
            if (!string.IsNullOrEmpty(context.TimeOfDay))
            {
                sb.AppendLine($"- Time of day: {context.TimeOfDay}");
            }
            if (!string.IsNullOrEmpty(context.CurrentTime))
            {
                sb.AppendLine($"- Current time: {context.CurrentTime}");
            }
            
            return sb.ToString();
        }

        /// <summary>
        /// Efficiently builds an ordering prompt by prepending dynamic context.
        /// This avoids expensive string replacement operations.
        /// </summary>
        private static string BuildOrderingPrompt(string template, PromptContext context)
        {
            var sb = new System.Text.StringBuilder(template.Length + 500);
            
            // Start with the base template
            sb.AppendLine(template);
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
            sb.AppendLine($"- Current total: {context.CurrentTotal ?? "0.00"}");
            sb.AppendLine($"- Currency: {context.Currency ?? "USD"}");
            sb.AppendLine();
            
            sb.AppendLine($"**CUSTOMER JUST SAID:** '{context.UserInput ?? ""}'");
            sb.AppendLine();
            sb.AppendLine("Process this request using your cultural knowledge and tools.");
            
            return sb.ToString();
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
