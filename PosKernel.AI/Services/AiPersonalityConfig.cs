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

        /// <summary>
        /// Gets or sets common phrases for the personality.
        /// </summary>
        public Dictionary<string, string> CommonPhrases { get; set; } = new();

        /// <summary>
        /// Gets or sets the greeting template.
        /// </summary>
        public string GreetingTemplate { get; set; } = "";

        /// <summary>
        /// Gets or sets the ordering template.
        /// </summary>
        public string OrderingTemplate { get; set; } = "";
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

        private static AiPersonalityConfig CreateAmericanBarista()
        {
            return new AiPersonalityConfig
            {
                Type = PersonalityType.AmericanBarista,
                StaffTitle = "Barista",
                VenueTitle = "Coffee Shop",
                SupportedLanguages = new List<string> { "English" },
                CultureCode = "en-US",
                Currency = "USD",
                CommonPhrases = new Dictionary<string, string>
                {
                    ["greeting_morning"] = "Good morning! What can I get started for you today?",
                    ["greeting_afternoon"] = "Good afternoon! What sounds good to you?",
                    ["greeting_evening"] = "Good evening! How can I help you?",
                    ["order_complete"] = "Perfect! Your order is complete.",
                    ["payment_request"] = "Your total comes to {total}. How would you like to pay?",
                    ["farewell"] = "Thank you! Have a great day!"
                },
                GreetingTemplate = LoadPromptTemplate("AmericanBarista", "greeting"),
                OrderingTemplate = LoadPromptTemplate("AmericanBarista", "ordering")
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
                Currency = "SGD",
                CommonPhrases = new Dictionary<string, string>
                {
                    ["greeting_morning"] = "Morning! What you want?",
                    ["greeting_afternoon"] = "Afternoon lah! Order what?",
                    ["greeting_evening"] = "Evening! You want order or not?",
                    ["order_complete"] = "Can! All done.",
                    ["payment_request"] = "Total {total} dollar. How you pay?",
                    ["farewell"] = "Thank you! Come again!"
                },
                GreetingTemplate = LoadPromptTemplate("SingaporeanKopitiamUncle", "greeting"),
                OrderingTemplate = LoadPromptTemplate("SingaporeanKopitiamUncle", "ordering")
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
                Currency = "EUR",
                CommonPhrases = new Dictionary<string, string>
                {
                    ["greeting_morning"] = "Bonjour! Que désirez-vous aujourd'hui?",
                    ["greeting_afternoon"] = "Bon après-midi! Comment puis-je vous aider?",
                    ["greeting_evening"] = "Bonsoir! Qu'est-ce qui vous ferait plaisir?",
                    ["order_complete"] = "Parfait! Votre commande est prête.",
                    ["payment_request"] = "Cela fait {total} euros. Comment souhaitez-vous payer?",
                    ["farewell"] = "Merci beaucoup! Bonne journée!"
                },
                GreetingTemplate = LoadPromptTemplate("FrenchBoulanger", "greeting"),
                OrderingTemplate = LoadPromptTemplate("FrenchBoulanger", "ordering")
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
                Currency = "JPY",
                CommonPhrases = new Dictionary<string, string>
                {
                    ["greeting_morning"] = "Irasshaimase! Ohayou gozaimasu!",
                    ["greeting_afternoon"] = "Irasshaimase! Konnichiwa!",
                    ["greeting_evening"] = "Irasshaimase! Konbanwa!",
                    ["order_complete"] = "Arigatou gozaimasu!",
                    ["payment_request"] = "{total}円 desu. How would you like to pay?",
                    ["farewell"] = "Arigatou gozaimashita! Mata douzo!"
                },
                GreetingTemplate = LoadPromptTemplate("JapaneseConbiniClerk", "greeting"),
                OrderingTemplate = LoadPromptTemplate("JapaneseConbiniClerk", "ordering")
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
                Currency = "INR",
                CommonPhrases = new Dictionary<string, string>
                {
                    ["greeting_morning"] = "Namaste! Chai chahiye?",
                    ["greeting_afternoon"] = "Sahib, kya chahiye?",
                    ["greeting_evening"] = "Adaab! Evening chai?",
                    ["order_complete"] = "Bas! Ready hai!",
                    ["payment_request"] = "Total {total} rupees. Cash denge?",
                    ["farewell"] = "Dhanyawad! Phir aana!"
                },
                GreetingTemplate = LoadPromptTemplate("IndianChaiWala", "greeting"),
                OrderingTemplate = LoadPromptTemplate("IndianChaiWala", "ordering")
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
                Currency = "USD",
                CommonPhrases = new Dictionary<string, string>
                {
                    ["greeting_morning"] = "Good morning! How can I help you?",
                    ["greeting_afternoon"] = "Good afternoon! What can I get for you?",
                    ["greeting_evening"] = "Good evening! How may I assist you?",
                    ["order_complete"] = "Your order is complete.",
                    ["payment_request"] = "Your total is {total}. How would you like to pay?",
                    ["farewell"] = "Thank you for your business!"
                },
                GreetingTemplate = LoadPromptTemplate("GenericCashier", "greeting"),
                OrderingTemplate = LoadPromptTemplate("GenericCashier", "ordering")
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
