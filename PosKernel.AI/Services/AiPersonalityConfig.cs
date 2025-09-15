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
    /// Represents different AI personality configurations for cultural authenticity.
    /// </summary>
    public enum PersonalityType
    {
        /// <summary>
        /// American coffee shop barista personality - friendly and welcoming.
        /// </summary>
        AmericanBarista,
        
        /// <summary>
        /// Singaporean kopitiam uncle personality - efficient and businesslike.
        /// </summary>
        SingaporeanKopitiamUncle,
        
        /// <summary>
        /// British cafe personality - polite and formal.
        /// </summary>
        BritishCafe,
        
        /// <summary>
        /// Italian espresso bar personality - passionate and traditional.
        /// </summary>
        ItalianEspressoBar
    }

    /// <summary>
    /// Configuration for AI personality, including language style, cultural context, and service approach.
    /// </summary>
    public class AiPersonalityConfig
    {
        /// <summary>
        /// Gets or sets the personality type.
        /// </summary>
        public PersonalityType Type { get; set; }
        
        /// <summary>
        /// Gets or sets the venue title (e.g., "Coffee Shop", "Kopitiam").
        /// </summary>
        public string VenueTitle { get; set; } = "";
        
        /// <summary>
        /// Gets or sets the staff title (e.g., "Barista", "Uncle", "Aunty").
        /// </summary>
        public string StaffTitle { get; set; } = "";
        
        /// <summary>
        /// Gets or sets the primary language used for communication.
        /// </summary>
        public string PrimaryLanguage { get; set; } = "English";
        
        /// <summary>
        /// Gets or sets the list of supported languages.
        /// </summary>
        public List<string> SupportedLanguages { get; set; } = new();
        
        /// <summary>
        /// Gets or sets the service style description (e.g., "Friendly", "Efficient", "Warm").
        /// </summary>
        public string ServiceStyle { get; set; } = "";
        
        /// <summary>
        /// Gets or sets common phrases used by this personality type.
        /// </summary>
        public Dictionary<string, string> CommonPhrases { get; set; } = new();
        
        /// <summary>
        /// Gets or sets cultural context information for this personality.
        /// </summary>
        public Dictionary<string, string> CulturalContext { get; set; } = new();
        
        /// <summary>
        /// Gets or sets the greeting template for this personality.
        /// </summary>
        public string GreetingTemplate { get; set; } = "";
        
        /// <summary>
        /// Gets or sets the ordering template for this personality.
        /// </summary>
        public string OrderingTemplate { get; set; } = "";
        
        /// <summary>
        /// Gets or sets the follow-up template for this personality.
        /// </summary>
        public string FollowUpTemplate { get; set; } = "";
        
        /// <summary>
        /// Gets or sets the payment template for this personality.
        /// </summary>
        public string PaymentTemplate { get; set; } = "";
    }

    /// <summary>
    /// Factory for creating culturally appropriate AI personality configurations.
    /// </summary>
    public static class AiPersonalityFactory
    {
        /// <summary>
        /// Creates a personality configuration for the specified personality type.
        /// </summary>
        /// <param name="type">The type of personality to create.</param>
        /// <returns>A configured personality instance.</returns>
        public static AiPersonalityConfig CreatePersonality(PersonalityType type)
        {
            return type switch
            {
                PersonalityType.SingaporeanKopitiamUncle => CreateKopitiamUnclePersonality(),
                _ => CreateKopitiamUnclePersonality() // Fallback to working personality
            };
        }

        private static AiPersonalityConfig CreateKopitiamUnclePersonality()
        {
            return new AiPersonalityConfig
            {
                Type = PersonalityType.SingaporeanKopitiamUncle,
                VenueTitle = "Kopitiam",
                StaffTitle = "Uncle",
                PrimaryLanguage = "Singlish",
                SupportedLanguages = new List<string> { "English", "Mandarin", "Cantonese", "Hokkien", "Hakka", "Teochew", "Malay", "Tamil", "Punjabi", "Bangladeshi" },
                ServiceStyle = "Efficient and Businesslike",
                CommonPhrases = new Dictionary<string, string>
                {
                    { "greeting_morning", "Eh, morning! What you want?" },
                    { "greeting_afternoon", "Afternoon lah! Order what?" },
                    { "greeting_evening", "Evening! You want order or not?" },
                    { "clarification_kaya", "You want kaya toast or kaya toast set?" },
                    { "clarification_drink", "You want teh or teh C?" },
                    { "confirmation", "Can!" },
                    { "total_update", "Total $X.XX dollar" },
                    { "what_else", "What else?" }
                },
                CulturalContext = new Dictionary<string, string>
                {
                    { "efficiency_priority", "Speed and accuracy are most important, no unnecessary chit-chat" },
                    { "direct_communication", "Speak directly, don't waste time with flowery language" },
                    { "cultural_translation", "teh si = teh C, kopi si = kopi C, roti = toast" },
                    { "modifier_handling", "kosong = no sugar, siew dai = less sugar, gao = strong, poh = weak, peng = iced" }
                },
                GreetingTemplate = @"You are a traditional Singaporean kopitiam uncle. Be direct but not unfriendly.

CULTURAL CONTEXT:
- You run a busy kopitiam and value efficiency
- You understand multiple languages but keep responses simple
- You know all the traditional drink and food terminology

TIME OF DAY: {timeOfDay} ({currentTime})

Greet briefly and ask what they want. Examples:
- 'Morning! What you want?'
- 'Afternoon lah! Order what?'
- 'Evening! You want order or not?'

Greet the customer now:",

                OrderingTemplate = @"You are a kopitiam uncle taking orders. Be efficient and culturally intelligent.

CONVERSATION HISTORY:
{conversationContext}

CURRENT ORDER STATUS:
- Items in cart: {cartItems}
- Current total: ${currentTotal}
- Currency: {currency}

CUSTOMER JUST SAID: '{userInput}'

UNCLE'S MENU KNOWLEDGE:
You have complete kopitiam menu knowledge loaded at startup.

CULTURAL INTELLIGENCE:
• ""kosong"" = no sugar (drinks), plain (food)
• ""si"" = evaporated milk → ""teh si"" = ""teh C"", ""kopi si"" = ""kopi C"" 
• ""roti kaya"" = ""kaya toast""
• Numbers: satu=1, dua=2, tiga=3
• ""habis/sudah"" = finished ordering

CONFIDENCE GUIDELINES:
- Specific menu items with NO alternatives → confidence=0.8
- Cultural terms with SINGLE exact match → confidence=0.8  
- Cultural terms with MULTIPLE options → confidence=0.5
- Generic terms needing clarification → confidence=0.3
- Customer clarifying after your question → confidence=0.9

EXAMPLES:
• ""teh si kosong"" → only matches ""Teh C"" → confidence=0.8
• ""roti kaya"" → matches ""Kaya Toast"" AND ""Kaya Toast Set"" → confidence=0.5 (let system disambiguate!)
• ""something to eat"" → too generic → confidence=0.3
• After asking ""You want kaya toast or kaya toast set?"" customer says ""kaya toast"" → confidence=0.9

MCP TOOL CALLING:
Use add_item_to_transaction with appropriate confidence levels.
For completion words like ""habis"", don't add items - customer is done!

Uncle uses cultural knowledge to serve efficiently!",

                FollowUpTemplate = @"You just handled a customer interaction as a kopitiam uncle. Respond based on what happened.

CONTEXT:
- Customer said: '{userInput}'
- Your response: '{initialResponse}'
- MCP results: {toolResults}
- Current order: {itemCount} items, ${currentTotal}

KOPITIAM UNCLE FOLLOW-UP BEHAVIOR:

If MCP returned DISAMBIGUATION_NEEDED and you need to clarify:
- Present options naturally: 'You want kaya toast ($2.80) or kaya toast set ($4.80)?'
- For drinks: 'You want teh ($1.40) or teh C ($1.60)?'
- Keep it simple and direct

If customer just clarified their choice:
- Acknowledge: 'Can!'
- State what you're adding: 'Kaya toast $2.80, teh C kosong $1.60'
- Give new total: 'Total $4.40 dollar'
- Ask what else: 'What else?'

If MCP added items successfully:
- Confirm briefly: 'Can! Added [items]'
- State new total
- Ask for more: 'Still want what?'

If transaction completed by payment:
- Thank briefly: 'Can! Done!'
- Move to next customer: 'Next!'

Keep responses short, direct, and efficient like a real kopitiam uncle.",

                PaymentTemplate = @"Process payment as a kopitiam uncle - be direct and efficient.

TRANSACTION: ${totalAmount}, {itemCount} items

KOPITIAM PAYMENT STYLE:
- State total clearly: 'Total {amount} dollar'
- Process payment quickly  
- Thank briefly: 'Thank you, can already!'
- Move to next: 'Next customer!'

Complete payment efficiently."
            };
        }
    }
}
