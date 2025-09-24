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

using PosKernel.Configuration;
using PosKernel.AI.Core;

namespace PosKernel.AI.Services
{
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
        /// Gets the correct path to the AI configuration directory with hierarchical provider/model support.
        /// ARCHITECTURAL PRINCIPLE: FAIL FAST - No silent fallbacks if ai_config directory not found.
        /// </summary>
        private static string GetPromptsPath()
        {
            // ARCHITECTURAL PRINCIPLE: Use POSKERNEL_HOME for hierarchical prompt management
            var posKernelHome = Environment.GetEnvironmentVariable("POSKERNEL_HOME") 
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".poskernel");

            var aiConfigPath = Path.Combine(posKernelHome, "ai_config");
            var aiConfigPromptsPath = Path.Combine(posKernelHome, "ai_config", "prompts");

            // Try multiple possible locations for backward compatibility
            var possiblePaths = new[]
            {
                // New hierarchical structure with prompts subdirectory (current location)
                aiConfigPromptsPath,
                
                // New hierarchical structure (target location)
                aiConfigPath,
                
                // Legacy project source directory (development scenario)
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

            // ARCHITECTURAL PRINCIPLE: FAIL FAST - Create directory structure if missing
            if (!Directory.Exists(aiConfigPath))
            {
                Directory.CreateDirectory(aiConfigPath);
                var basePath = Path.Combine(aiConfigPath, "Base");
                Directory.CreateDirectory(basePath);
                
                // Create default personality directory
                var defaultPersonalityPath = Path.Combine(basePath, "SingaporeanKopitiamUncle");
                Directory.CreateDirectory(defaultPersonalityPath);
                
                // Create a basic default prompt as an example
                var defaultPromptPath = Path.Combine(defaultPersonalityPath, "ordering.md");
                File.WriteAllText(defaultPromptPath, @"# Default AI Cashier Prompt

You are a helpful AI cashier. Process customer orders efficiently and professionally.

Use the available tools to:
- Add items to the transaction
- Process payments  
- Provide helpful information

Be friendly and responsive to customer needs.");
                
                return aiConfigPath;
            }

            // ARCHITECTURAL PRINCIPLE: FAIL FAST - No silent fallbacks
            var searchedPaths = string.Join("\n  - ", possiblePaths);
            throw new DirectoryNotFoundException(
                $"DESIGN DEFICIENCY: AI configuration directory not found. AI personality system requires prompt files to function.\n" +
                $"Searched locations:\n  - {searchedPaths}\n\n" +
                $"Fix: Ensure ai_config directory exists at $POSKERNEL_HOME/ai_config with hierarchical structure:\n" +
                $"  ai_config/Base/{{PersonalityType}}/{{promptType}}.md\n" +
                $"  ai_config/{{Provider}}/{{Model}}/{{PersonalityType}}/{{promptType}}.md\n\n" +
                $"Current directory: {Directory.GetCurrentDirectory()}\n" +
                $"Base directory: {AppDomain.CurrentDomain.BaseDirectory}\n" +
                $"POSKERNEL_HOME: {posKernelHome}");
        }

        /// <summary>
        /// Creates a personality configuration based on the specified type.
        /// ARCHITECTURAL PRINCIPLE: Personality configuration must come from store configuration service.
        /// </summary>
        /// <param name="personalityType">The type of personality to create.</param>
        /// <param name="storeConfig">Store configuration providing currency and culture information.</param>
        /// <returns>A configured personality instance.</returns>
        public static AiPersonalityConfig CreatePersonality(PersonalityType personalityType, StoreConfig storeConfig)
        {
            if (storeConfig == null)
            {
                throw new ArgumentNullException(nameof(storeConfig),
                    "DESIGN DEFICIENCY: Store configuration required for personality creation. " +
                    "Cannot hardcode culture/currency assumptions. " +
                    "Provide StoreConfig from proper configuration service.");
            }

            return personalityType switch
            {
                PersonalityType.SingaporeanKopitiamUncle => CreateSingaporeanKopitiamUncle(storeConfig),
                PersonalityType.AmericanBarista => CreateAmericanBarista(storeConfig),
                PersonalityType.FrenchBoulanger => CreateFrenchBoulanger(storeConfig),
                PersonalityType.JapaneseConbiniClerk => CreateJapaneseConbiniClerk(storeConfig),
                PersonalityType.IndianChaiWala => CreateIndianChaiWala(storeConfig),
                PersonalityType.GenericCashier => CreateGenericCashier(storeConfig),
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
        /// Loads a prompt template from a Markdown file with hierarchical provider/model resolution.
        /// ARCHITECTURAL PRINCIPLE: 
        /// 1. Try provider/model-specific: ai_config/{Provider}/{Model}/{PersonalityType}/{promptType}.md
        /// 2. Fall back to base: ai_config/Base/{PersonalityType}/{promptType}.md  
        /// 3. FAIL FAST if neither exists
        /// </summary>
        /// <param name="personalityFolder">The personality folder name (e.g., "SingaporeanKopitiamUncle").</param>
        /// <param name="promptType">The prompt type ("greeting", "ordering", "tool_acknowledgment", etc.).</param>
        /// <returns>The prompt content as a string.</returns>
        private static string LoadPromptTemplate(string personalityFolder, string promptType)
        {
            var provider = GetCurrentProvider();
            var model = GetCurrentModel();
            var cacheKey = $"{provider}_{model}_{personalityFolder}_{promptType}";

            // ARCHITECTURAL PRINCIPLE: Hierarchical prompt resolution
            var providerModelPath = Path.Combine(PromptsBasePath, provider, model, personalityFolder, $"{promptType}.md");
            var basePath = Path.Combine(PromptsBasePath, "Base", personalityFolder, $"{promptType}.md");
            
            // PROMPT LOADING DEBUG: Use Console.WriteLine so it appears in Terminal.GUI debug logs
            Console.WriteLine($"PROMPT_LOADING: Checking paths for {personalityFolder}/{promptType}:");
            Console.WriteLine($"  Provider-specific: {providerModelPath} (exists: {File.Exists(providerModelPath)})");
            Console.WriteLine($"  Base fallback: {basePath} (exists: {File.Exists(basePath)})");
            Console.WriteLine($"  Provider: {provider}, Model: {model}");
            Console.WriteLine($"  PromptsBasePath: {PromptsBasePath}");
            
            // ARCHITECTURAL FIX: Always check file timestamps, even for cached prompts
            var shouldReload = !_promptCache.ContainsKey(cacheKey);
            DateTime? latestTimestamp = null;
            DateTime? cachedTimestamp = null;
            
            // Get cached timestamp if it exists
            if (_promptFileTimestamps.TryGetValue(cacheKey, out var existingTimestamp))
            {
                cachedTimestamp = existingTimestamp;
            }

            // Check timestamps for both possible files
            if (File.Exists(providerModelPath))
            {
                var providerTimestamp = File.GetLastWriteTime(providerModelPath);
                latestTimestamp = providerTimestamp;
                
                // ARCHITECTURAL FIX: Force reload if file is newer than cached version
                if (cachedTimestamp.HasValue && providerTimestamp > cachedTimestamp.Value)
                {
                    shouldReload = true;
                    Console.WriteLine($"🔍 PROMPT_LOADING: Provider-specific file newer than cache - forcing reload");
                }
            }

            if (File.Exists(basePath))
            {
                var baseTimestamp = File.GetLastWriteTime(basePath);
                if (latestTimestamp == null || baseTimestamp > latestTimestamp)
                {
                    latestTimestamp = baseTimestamp;
                }
                
                // ARCHITECTURAL FIX: Force reload if base file is newer than cached version
                if (cachedTimestamp.HasValue && baseTimestamp > cachedTimestamp.Value)
                {
                    shouldReload = true;
                    Console.WriteLine($"🔍 PROMPT_LOADING: Base file newer than cache - forcing reload");
                }
            }

            // Return cached version if no reload needed
            if (!shouldReload && _promptCache.TryGetValue(cacheKey, out var cachedPrompt))
            {
                Console.WriteLine($"PROMPT_LOADING: Using cached prompt for {cacheKey} (cached: {cachedTimestamp})");
                return cachedPrompt;
            }

            // Try provider/model-specific first
            if (File.Exists(providerModelPath))
            {
                try
                {
                    var content = File.ReadAllText(providerModelPath);
                    _promptCache[cacheKey] = content;
                    if (latestTimestamp.HasValue)
                    {
                        _promptFileTimestamps[cacheKey] = latestTimestamp.Value;
                    }
                    
                    // PROMPT LOADING DEBUG: Log successful provider-specific prompt load
                    Console.WriteLine($"PROMPT_LOADING: SUCCESS Loaded provider-specific prompt from {providerModelPath}");
                    Console.WriteLine($"PROMPT_LOADING: Content length: {content.Length} characters");
                    Console.WriteLine($"PROMPT_LOADING: File timestamp: {File.GetLastWriteTime(providerModelPath)}");
                    Console.WriteLine($"PROMPT_LOADING: First 200 chars: {content.SafeTruncateString(200)}");
                    
                    return content;
                }
                catch (Exception ex)
                {
                    // Log warning but continue to fallback
                    Console.WriteLine($"PROMPT_LOADING: WARNING Failed to read provider-specific prompt {providerModelPath}: {ex.Message}");
                }
            }

            // Try base prompt as fallback
            if (File.Exists(basePath))
            {
                try
                {
                    var content = File.ReadAllText(basePath);
                    _promptCache[cacheKey] = content;
                    if (latestTimestamp.HasValue)
                    {
                        _promptFileTimestamps[cacheKey] = latestTimestamp.Value;
                    }
                    
                    // PROMPT LOADING DEBUG: Log fallback prompt load
                    Console.WriteLine($"PROMPT_LOADING: SUCCESS Loaded base fallback prompt from {basePath}");
                    Console.WriteLine($"PROMPT_LOADING: Content length: {content.Length} characters");
                    Console.WriteLine($"PROMPT_LOADING: File timestamp: {File.GetLastWriteTime(basePath)}");
                    Console.WriteLine($"PROMPT_LOADING: First 200 chars: {content.SafeTruncateString(200)}");
                    Console.WriteLine($"PROMPT_LOADING: Using base fallback for {provider}/{model}/{personalityFolder}/{promptType}");
                    
                    return content;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"PROMPT_LOADING: ERROR Error reading base prompt {basePath}: {ex.Message}");
                }
            }
            
            // ARCHITECTURAL PRINCIPLE: FAIL FAST - No silent fallbacks to other personalities or inline prompts
            var errorMessage = 
                $"DESIGN DEFICIENCY: No prompt found for {personalityFolder}/{promptType}.\n" +
                $"Checked provider-specific: {providerModelPath} (exists: {File.Exists(providerModelPath)})\n" +
                $"Checked base fallback: {basePath} (exists: {File.Exists(basePath)})\n\n" +
                $"Fix: Create a prompt file:\n" +
                $"  - For all models: {basePath}\n" +
                $"  - For {provider}/{model} only: {providerModelPath}\n\n" +
                $"Provider: {provider}, Model: {model}\n" +
                $"Available files: {GetAvailableFiles(personalityFolder)}";
            
            // PROMPT LOADING DEBUG: Log the error before throwing
            Console.WriteLine($"PROMPT_LOADING: ERROR {errorMessage}");
            
            throw new FileNotFoundException(errorMessage);
        }

        /// <summary>
        /// ARCHITECTURAL PRINCIPLE: Get current AI provider from configuration service.
        /// FAIL FAST if not configured - no silent fallbacks to hide design problems.
        /// </summary>
        private static string GetCurrentProvider()
        {
            // ARCHITECTURAL FIX: Use PosKernelConfiguration instead of direct environment variable access
            var config = PosKernelConfiguration.Initialize();
            
            // ARCHITECTURAL BUG FIX: Always use STORE configuration for prompt path resolution
            // The cashier always uses STORE_AI_PROVIDER for prompt loading
            var provider = config.GetValue<string>("STORE_AI_PROVIDER");
            if (!string.IsNullOrEmpty(provider))
            {
                return provider;
            }
            
            throw new InvalidOperationException(
                "DESIGN DEFICIENCY: Store AI Provider not configured. " +
                "AI personality system serves the store cashier and requires store configuration. " +
                "Set STORE_AI_PROVIDER environment variable in ~/.poskernel/.env " +
                "Store cashier cannot operate without proper AI provider configuration.");
        }

        /// <summary>
        /// ARCHITECTURAL PRINCIPLE: Get current AI model from configuration service.
        /// FAIL FAST if not configured - no silent fallbacks to hide design problems.
        /// </summary>
        private static string GetCurrentModel()
        {
            // ARCHITECTURAL FIX: Use PosKernelConfiguration instead of direct environment variable access
            var config = PosKernelConfiguration.Initialize();
            
            // ARCHITECTURAL BUG FIX: Always use STORE configuration for prompt path resolution
            // The cashier always uses STORE_AI_MODEL for prompt loading  
            var model = config.GetValue<string>("STORE_AI_MODEL");
            if (!string.IsNullOrEmpty(model))
            {
                // ARCHITECTURAL FIX: Normalize model name for safe path usage
                return NormalizeModelNameForPath(model);
            }
            
            throw new InvalidOperationException(
                "DESIGN DEFICIENCY: Store AI Model not configured. " +
                "AI personality system serves the store cashier and requires store configuration. " +
                "Set STORE_AI_MODEL environment variable in ~/.poskernel/.env " +
                "Store cashier cannot operate without proper AI model configuration.");
        }

        /// <summary>
        /// Normalizes a model name to be safe for use in file paths.
        /// Replaces characters that are invalid in Windows/Unix paths with safe alternatives.
        /// </summary>
        /// <param name="modelName">The model name to normalize</param>
        /// <returns>A path-safe model name</returns>
        private static string NormalizeModelNameForPath(string modelName)
        {
            if (string.IsNullOrEmpty(modelName))
            {
                return modelName;
            }

            // ARCHITECTURAL FIX: Use Path.GetInvalidFileNameChars() to handle all invalid characters properly
            // This covers both path and filename restrictions across Windows/Unix systems
            var invalidChars = Path.GetInvalidFileNameChars();
            var normalizedName = new string(modelName.Select(c => invalidChars.Contains(c) ? '-' : c).ToArray());
            
            return normalizedName;
        }

        /// <summary>
        /// Loads a prompt template from a Markdown file.
        /// ARCHITECTURAL PRINCIPLE: FAIL FAST - No silent fallbacks if prompt files missing.
        /// </summary>
        /// <param name="personalityFolder">The personality folder name (e.g., "AmericanBarista").</param>
        /// <param name="promptType">The prompt type ("greeting", "ordering", or "post-payment").</param>
        /// <returns>The prompt content as a string.</returns>
        private static string LoadPromptTemplate_old(string personalityFolder, string promptType)
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
            
            // CRITICAL DEBUG: Log exactly what personality is being requested
            Console.WriteLine($"🔍 PROMPT_DEBUG: LoadPrompt called with PersonalityType={personalityType}, PromptType={promptType}");
            Console.WriteLine($"🔍 PROMPT_DEBUG: PersonalityFolder={personalityFolder}");
            
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
        /// ARCHITECTURAL PRINCIPLE: FAIL FAST - No currency fallbacks.
        /// </summary>
        private static string BuildOrderingPrompt(string template, PromptContext context)
        {
            var sb = new System.Text.StringBuilder(template.Length + 500);
            
            // Start with the base template
            sb.AppendLine(template);
            
            // ARCHITECTURAL FIX: Only add context sections if they have meaningful content
            bool hasContext = !string.IsNullOrEmpty(context.ConversationContext) ||
                             !string.IsNullOrEmpty(context.CartItems) ||
                             !string.IsNullOrEmpty(context.CurrentTotal) ||
                             !string.IsNullOrEmpty(context.UserInput);

            if (!hasContext)
            {
                // No meaningful context to add - return template as-is
                return template;
            }

            sb.AppendLine();
            sb.AppendLine("## CURRENT SESSION CONTEXT:");
            
            if (!string.IsNullOrEmpty(context.ConversationContext))
            {
                sb.AppendLine("### Recent Conversation:");
                sb.AppendLine(context.ConversationContext);
                sb.AppendLine();
            }
            
            // Only add order status if there's meaningful order data
            bool hasOrderData = !string.IsNullOrEmpty(context.CartItems) || 
                               !string.IsNullOrEmpty(context.CurrentTotal);
            
            if (hasOrderData)
            {
                sb.AppendLine("### Current Order Status:");
                if (!string.IsNullOrEmpty(context.CartItems))
                {
                    sb.AppendLine($"- Items in cart: {context.CartItems}");
                }
                if (!string.IsNullOrEmpty(context.CurrentTotal))
                {
                    sb.AppendLine($"- Current total: {context.CurrentTotal}");
                }
                
                // ARCHITECTURAL PRINCIPLE: FAIL FAST if currency not provided by store configuration service
                if (string.IsNullOrEmpty(context.Currency))
                {
                    throw new InvalidOperationException(
                        "DESIGN DEFICIENCY: PromptContext requires Currency from store configuration service. " +
                        "Client cannot decide currency defaults. " +
                        "Register ICurrencyFormattingService and ensure store configuration provides currency.");
                }
                sb.AppendLine($"- Currency: {context.Currency}");
                sb.AppendLine();
            }
            
            // Only add customer input if there's actual input
            if (!string.IsNullOrEmpty(context.UserInput))
            {
                sb.AppendLine($"**CUSTOMER JUST SAID:** '{context.UserInput}'");
                sb.AppendLine();
                sb.AppendLine("Process this request using your cultural knowledge and tools.");
            }
            
            return sb.ToString();
        }

        private static AiPersonalityConfig CreateAmericanBarista(StoreConfig storeConfig)
        {
            return new AiPersonalityConfig
            {
                Type = PersonalityType.AmericanBarista,
                StaffTitle = "Barista",
                VenueTitle = "Coffee Shop",
                SupportedLanguages = new List<string> { "English" },
                CultureCode = storeConfig.CultureCode,
                Currency = storeConfig.Currency
            };
        }

        private static AiPersonalityConfig CreateSingaporeanKopitiamUncle(StoreConfig storeConfig)
        {
            return new AiPersonalityConfig
            {
                Type = PersonalityType.SingaporeanKopitiamUncle,
                StaffTitle = "Uncle",
                VenueTitle = "Kopitiam",
                SupportedLanguages = new List<string> { "English", "Mandarin", "Cantonese", "Hokkien", "Hakka", "Teochew", "Malay", "Tamil", "Punjabi", "Bangladeshi" },
                CultureCode = storeConfig.CultureCode,
                Currency = storeConfig.Currency
            };
        }

        private static AiPersonalityConfig CreateFrenchBoulanger(StoreConfig storeConfig)
        {
            return new AiPersonalityConfig
            {
                Type = PersonalityType.FrenchBoulanger,
                StaffTitle = "Boulanger",
                VenueTitle = "Boulangerie",
                SupportedLanguages = new List<string> { "French", "English" },
                CultureCode = storeConfig.CultureCode,
                Currency = storeConfig.Currency
            };
        }

        private static AiPersonalityConfig CreateJapaneseConbiniClerk(StoreConfig storeConfig)
        {
            return new AiPersonalityConfig
            {
                Type = PersonalityType.JapaneseConbiniClerk,
                StaffTitle = "Clerk",
                VenueTitle = "Convenience Store",
                SupportedLanguages = new List<string> { "Japanese", "English" },
                CultureCode = storeConfig.CultureCode,
                Currency = storeConfig.Currency
            };
        }

        private static AiPersonalityConfig CreateIndianChaiWala(StoreConfig storeConfig)
        {
            return new AiPersonalityConfig
            {
                Type = PersonalityType.IndianChaiWala,
                StaffTitle = "Chai Wala",
                VenueTitle = "Chai Stall",
                SupportedLanguages = new List<string> { "Hindi", "English", "Punjabi", "Bengali" },
                CultureCode = storeConfig.CultureCode,
                Currency = storeConfig.Currency
            };
        }

        private static AiPersonalityConfig CreateGenericCashier(StoreConfig storeConfig)
        {
            return new AiPersonalityConfig
            {
                Type = PersonalityType.GenericCashier,
                StaffTitle = "Cashier",
                VenueTitle = "Store",
                SupportedLanguages = new List<string> { "English" },
                CultureCode = storeConfig.CultureCode,
                Currency = storeConfig.Currency
            };
        }
    }
}
