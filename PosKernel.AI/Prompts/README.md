# AI Personality Prompts

This directory contains Markdown files that define the AI personality prompts for different store types and cultures.

## Directory Structure

```
Prompts/
├── AmericanBarista/
│   ├── greeting.md
│   └── ordering.md
├── SingaporeanKopitiamUncle/
│   ├── greeting.md
│   └── ordering.md
├── FrenchBoulanger/
│   ├── greeting.md
│   └── ordering.md
├── JapaneseConbiniClerk/
│   ├── greeting.md
│   └── ordering.md
├── IndianChaiWala/
│   ├── greeting.md
│   └── ordering.md
└── GenericCashier/
    ├── greeting.md
    └── ordering.md
```

## Prompt Types

### greeting.md
Contains the prompt used when an AI personality first greets a customer. This should establish the character's personality, cultural context, and initial interaction style.

**Available variables:**
- `{timeOfDay}` - Time period (Morning, Afternoon, Evening)
- `{currentTime}` - Current time in HH:mm format

### ordering.md
Contains the prompt used during the order-taking process. This includes context about the current conversation and order status.

**Available variables:**
- `{conversationContext}` - Previous conversation history
- `{cartItems}` - Current items in the customer's cart
- `{currentTotal}` - Current order total
- `{currency}` - Store currency (USD, SGD, EUR, JPY, INR)
- `{userInput}` - The customer's latest input

## Editing Prompts

1. **Maintain Character Consistency**: Each personality should have a distinct voice and cultural context
2. **Include Cultural Elements**: Use appropriate language, phrases, and cultural references
3. **Provide Clear Guidelines**: Include confidence levels and processing instructions for the AI
4. **Use Markdown Formatting**: Structure prompts with headers, lists, and emphasis for clarity

## Development Features

### Prompt Caching
Prompts are cached in memory for performance. During development, if you modify prompt files, you may need to restart the application to see changes.

### Cache Clearing
For advanced scenarios, you can call `AiPersonalityFactory.ClearPromptCache()` to force reload prompts from disk without restarting the application.

## Adding New Personalities

1. Create a new directory under `Prompts/` with the personality name
2. Add `greeting.md` and `ordering.md` files
3. Update `PersonalityType` enum in `AiPersonalityConfig.cs`
4. Add a new factory method in `AiPersonalityFactory`

## Fallback Behavior

If a prompt file is missing, the system will:
1. Try to use the GenericCashier equivalent
2. Fall back to a basic inline prompt
3. Cache the fallback for performance

This ensures the application continues to work even if prompt files are accidentally deleted or misconfigured.

## Benefits of Markdown-Based Prompts

- **Easy Editing**: Prompts can be modified without recompiling code
- **Version Control**: Changes to prompts are clearly visible in Git diffs
- **Collaboration**: Non-programmers can easily edit prompts
- **Documentation**: Markdown provides better formatting for complex instructions
- **Separation of Concerns**: Business logic (prompts) separated from code logic
