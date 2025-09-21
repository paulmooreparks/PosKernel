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
Contains the static prompt content used when an AI personality first greets a customer. Dynamic content (time, etc.) is injected programmatically.

### ordering.md
Contains the static personality instructions for order processing. Dynamic content (conversation history, cart status, user input) is injected efficiently as a context header.

## Efficient Architecture

### Performance-Optimized Building
Instead of expensive string replacement operations, prompts are built efficiently:

```csharp
var context = new PromptContext 
{
    ConversationContext = recentHistory,
    CartItems = currentItems,
    UserInput = customerRequest
};

var prompt = AiPersonalityFactory.BuildPrompt(PersonalityType.SingaporeanKopitiamUncle, "ordering", context);
```

### How It Works
1. **Static Content**: Loaded once from Markdown files and cached
2. **Dynamic Context**: Built programmatically using StringBuilder for efficiency
3. **No String Replacement**: Avoids expensive `.Replace()` operations on growing content
4. **Pre-allocated Buffers**: StringBuilder pre-allocated with reasonable capacity

### Context Injection
Dynamic content is prepended as a structured header:

```
## CURRENT SESSION CONTEXT:
### Recent Conversation:
Customer: I want kopi
Uncle: What kind of kopi?

### Current Order Status:
- Items in cart: none
- Current total: $0.00
- Currency: SGD

**CUSTOMER JUST SAID:** 'kopi si kosong'

[Static personality content follows...]
```

## Benefits of This Architecture

- **Performance**: No expensive string operations on growing conversation history
- **Scalability**: Performance doesn't degrade as conversation length increases
- **Memory Efficient**: Single StringBuilder allocation instead of multiple string copies
- **Clean Separation**: Static personality rules separate from dynamic session data
- **Easy Editing**: Markdown files contain only the essential personality instructions
- **Maintainable**: Dynamic content structure is consistent across all personalities

## Editing Prompts

1. **Focus on Personality**: Markdown files should contain cultural knowledge and behavior rules
2. **Avoid Dynamic Placeholders**: Don't use `{variables}` - dynamic content is injected automatically
3. **Include Processing Guidelines**: Add confidence levels and cultural processing instructions
4. **Use Clear Structure**: Headers and lists for easy AI comprehension

## Development Features

### Prompt Caching
Static templates are cached for performance. Dynamic context is built fresh each time.

### Cache Clearing
Call `AiPersonalityFactory.ClearPromptCache()` to reload templates during development.

## Adding New Personalities

1. Create directory under `Prompts/` with personality name
2. Add `greeting.md` and `ordering.md` files (no placeholders needed)
3. Update `PersonalityType` enum 
4. Add factory method - use `BuildPrompt()` for efficiency

## Migration from String Replacement

Old approach (slow):
```csharp
var template = LoadPrompt(type, "ordering")
    .Replace("{conversationContext}", history)
    .Replace("{cartItems}", items)
    .Replace("{userInput}", input);
```

New approach (fast):
```csharp
var context = new PromptContext { /* populate fields */ };
var prompt = AiPersonalityFactory.BuildPrompt(type, "ordering", context);
