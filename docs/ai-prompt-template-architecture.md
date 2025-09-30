# AI Prompt Template System Architecture

**System**: POS Kernel v0.7.5+ with Template-Driven AI Prompts
**Status**: Production Ready - Template System Complete
**Date**: September 2025
**Achievement**: Complete elimination of hardcoded cultural assumptions in C# code

## Overview

The POS Kernel AI system uses a sophisticated template-based prompt system that ensures cultural intelligence and business rules remain in configurable files, not hardcoded in C# code. This architectural pattern prevents cultural assumptions from sneaking into the kernel and enables easy customization per store/personality.

## Core Architectural Principle

**FAIL-FAST ON CULTURAL ASSUMPTIONS**: The system is designed to fail fast when cultural knowledge is missing, rather than providing "helpful" defaults that hide design problems.

```csharp
// ❌ NEVER DO THIS - Silent fallback hides design problems
if (culturalService == null) {
    return "Thank you for your order!"; // BAD - hardcoded cultural assumption
}

// ✅ ALWAYS DO THIS - Fail fast reveals design problems
if (culturalService == null) {
    throw new InvalidOperationException(
        "DESIGN DEFICIENCY: Cultural service not registered. " +
        "Register ICulturalResponseService in DI container.");
}
```

## Template Directory Structure

```
~/.poskernel/ai_config/prompts/
├── OpenAI/
│   └── gpt-4o/
│       ├── SingaporeanKopitiamUncle/
│       │   ├── ordering.md               # Core ordering personality
│       │   ├── greeting.md               # Store greeting
│       │   ├── payment_complete.md       # Post-payment responses
│       │   ├── tool-analysis-context.md  # Tool execution context
│       │   ├── fast-interpret.md         # Quick phrase interpretation
│       │   ├── discovery-results.md      # Search result presentation
│       │   ├── cultural-reference.md     # Cultural translations
│       │   └── core-rules.md            # Fundamental business rules
│       ├── GenericCashier/
│       │   ├── ordering.md
│       │   ├── tool-analysis-context.md
│       │   ├── fast-interpret.md
│       │   └── discovery-results.md
│       └── JapaneseConbiniClerk/
│           ├── ordering.md
│           └── greeting.md
└── Anthropic/
    └── claude/
        └── [personality-folders...]
```

## Template Types and Usage

### 1. Core Personality Templates

**Purpose**: Define the fundamental personality and cultural intelligence for each AI cashier type.

**Key Templates**:
- `ordering.md` - Main ordering conversation intelligence
- `greeting.md` - Store-specific greeting behavior
- `payment_complete.md` - Post-payment responses
- `cultural-reference.md` - Cultural term translations

**Example**: Singapore Kopitiam Uncle cultural translations:
```markdown
# Cultural Food Translations
- "roti bakar kaya set" = search for "kaya toast set" or "Traditional Kaya Toast Set"
- "teh si kosong" = "Teh C" + MOD_NO_SUGAR modification
- "kopi peng" = search for "iced coffee" variations
```

### 2. Technical Context Templates

**Purpose**: Provide structured context for AI tool execution without hardcoding business logic in C# code.

**Key Templates**:
- `tool-analysis-context.md` - Context for tool execution phases
- `fast-interpret.md` - Quick phrase interpretation context
- `discovery-results.md` - Search result presentation format

**Benefits**:
- Eliminates hardcoded prompts in orchestration code
- Prevents cultural assumptions from sneaking into C# code
- Enables per-store customization of AI behavior
- Provides clear separation between technical and cultural concerns

### 3. Template Loading Architecture

**C# Implementation**:
```csharp
// ARCHITECTURAL PATTERN: Template-based prompts with fail-fast fallbacks
private string BuildToolAnalysisPrompt(PromptContext context, string contextHint)
{
    var basePrompt = AiPersonalityFactory.BuildPrompt(_personality.Type, "ordering", context);

    try
    {
        // Load template-based context instead of hardcoded strings
        var toolPrompt = AiPersonalityFactory.BuildPrompt(_personality.Type, "tool-analysis-context", context);
        return basePrompt + "\n\n" + toolPrompt;
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Failed to load tool-analysis-context template, using fallback");
        // Minimal fallback - no cultural assumptions
        return basePrompt + "\n\nExecute appropriate tools following personality rules.";
    }
}
```

## Cultural Intelligence Implementation

### Problem: Translation Without Hardcoding

**Challenge**: AI needs to translate "roti bakar kaya set" → "kaya toast set" to find database products, but we cannot hardcode cultural mappings in C# code.

**Solution**: Store configuration + template-driven translation:

```csharp
// StoreConfig.cs - Configuration, not hardcoded logic
public class StoreConfig
{
    public string MenuLanguage { get; set; } = "en"; // Database product language
    public string CultureCode { get; set; } = "en-SG"; // Cultural formatting
}
```

**Template Implementation** (`ordering.md`):
```markdown
**Common Food Translations (Singapore Kopitiam)**:
- "roti bakar kaya set" = search for "kaya toast set" or "Traditional Kaya Toast Set"
- "roti bakar" = search for "toast"
- "kaya" = preserve as "kaya"
- "teh si" = "Teh C"
- "kopi kosong" = "Kopi O"

**Translation Process**:
1. Check cultural vocabulary BEFORE any searches
2. Translate using cultural knowledge immediately
3. Search with translated terms, not original terms
4. Present results using personality-specific language
```

### MenuLanguage Configuration

**Purpose**: Tell the AI what language the database product names use, enabling proper translation targeting.

**Implementation**:
```csharp
// Singapore Kopitiam - products stored in English
MenuLanguage = "en"  // AI translates TO English for database searches
CultureCode = "en-SG" // Cultural formatting for currency, dates, etc.

// Future: Japanese Convenience Store
MenuLanguage = "ja"  // AI translates TO Japanese for database searches
CultureCode = "ja-JP" // Japanese cultural formatting
```

## Architectural Benefits

### 1. Eliminates Code-Level Cultural Assumptions

**Before** (Problematic):
```csharp
// WRONG - Cultural assumptions in C# code
if (userInput.Contains("roti bakar")) {
    searchTerm = userInput.Replace("roti bakar", "toast");
}
```

**After** (Correct):
```csharp
// RIGHT - Cultural intelligence in templates
var prompt = AiPersonalityFactory.BuildPrompt(_personality.Type, "ordering", context);
// Template contains: "roti bakar" → "toast" translation rules
```

### 2. Prevents Template Overrides

**Problem**: Previous orchestration code was adding hardcoded prompt text that overrode well-designed templates.

**Solution**: Template-first architecture with minimal C# prompt building:

```csharp
// OLD - Hardcoded prompt text interfering with templates
prompt += "TRANSLATION TARGET: Product database uses English language for product names.\n" +
          "When the user mentions an item, FIRST call search_products with their words...";

// NEW - Delegate to templates
var toolPrompt = AiPersonalityFactory.BuildPrompt(_personality.Type, "tool-analysis-context", context);
prompt += toolPrompt; // Template handles all cultural rules
```

### 3. Enables Store-Specific Customization

**Flexibility**: Different stores can have different AI personalities and cultural rules without code changes:

```bash
# Singapore Kopitiam
~/.poskernel/ai_config/prompts/OpenAI/gpt-4o/SingaporeanKopitiamUncle/ordering.md
# Contains: "roti bakar" → "toast" translations

# American Diner
~/.poskernel/ai_config/prompts/OpenAI/gpt-4o/AmericanDinerServer/ordering.md
# Contains: American slang and menu terminology

# Japanese Convenience Store
~/.poskernel/ai_config/prompts/OpenAI/gpt-4o/JapaneseConbiniClerk/ordering.md
# Contains: Japanese cultural terms and service patterns
```

## Translation Flow Implementation

### Successful Cultural Translation Flow

1. **Customer Input**: "set roti bakar kaya"
2. **AI Personality Loading**: Load `SingaporeanKopitiamUncle/ordering.md`
3. **Cultural Recognition**: Template recognizes "roti bakar kaya set"
4. **Translation**: Template rule: search for "kaya toast set"
5. **Database Search**: Find "Traditional Kaya Toast Set" (TSET001)
6. **Response**: Add set and ask for drink customization

### Template-Driven Process

```markdown
# From ordering.md template
**PRIORITY 2: SINGAPORE KOPITIAM EXPERT VOCABULARY**
Check and translate cultural terms BEFORE any tool calls.

**Common Food Translations**:
- "roti bakar kaya set" = search for "kaya toast set" or "Traditional Kaya Toast Set"

**MANDATORY PROCESSING RULE**:
If customer input contains ANY cultural terms, use cultural knowledge to translate
them IMMEDIATELY and proceed with translated terms. Do NOT search for original
untranslated terms first.
```

## Development Guidelines

### 1. Never Add Hardcoded Prompts to C# Code

**Rule**: All prompt text must be in template files, not C# strings.

**Enforcement**: Code reviews must check for hardcoded cultural assumptions:
```csharp
// ❌ VIOLATION - Will be rejected in code review
prompt += "When user says 'roti bakar', search for 'toast'";

// ✅ CORRECT - Use template system
var prompt = AiPersonalityFactory.BuildPrompt(_personality.Type, "ordering", context);
```

### 2. Template Creation Process

**For New Personalities**:
1. Create personality directory: `~/.poskernel/ai_config/prompts/[Provider]/[Model]/[PersonalityName]/`
2. Copy from `GenericCashier` templates as base
3. Customize cultural rules and personality traits
4. Test with representative customer inputs
5. Document cultural mappings clearly

**For New Template Types**:
1. Identify hardcoded prompt text in C# code
2. Extract to template file with clear name
3. Update C# code to load template with fallback
4. Create template for each existing personality
5. Test that fallbacks work when templates missing

### 3. Cultural Knowledge Documentation

**Template Requirements**:
- Document cultural translations explicitly
- Provide examples of expected inputs/outputs
- Include priority rules (completion detection, cultural vocab, etc.)
- Specify tool execution guidelines
- Cover edge cases and error handling

**Example Documentation**:
```markdown
**Cultural Translation Examples**:
- Input: "set roti bakar kaya" → Search: "kaya toast set" → Find: "Traditional Kaya Toast Set"
- Input: "teh si kosong" → Base: "Teh C" + Modification: MOD_NO_SUGAR
- Input: "kopi peng" → Search: "iced coffee" → Find: coffee + ice modifications
```

## Quality Assurance

### Template Validation

**Automated Testing**: Templates should be tested with representative cultural inputs:
```csharp
[Test]
public void SingaporeanKopitiam_Should_Translate_RotiBarakKaya()
{
    var result = TestCulturalTranslation("set roti bakar kaya", PersonalityType.SingaporeanKopitiamUncle);
    Assert.That(result, Should.Contain("Traditional Kaya Toast Set"));
}
```

### Cultural Assumption Detection

**Code Review Checklist**:
- [ ] No hardcoded cultural terms in C# code
- [ ] No hardcoded currency symbols ($, €, ¥)
- [ ] No hardcoded time formats or cultural mappings
- [ ] All prompts loaded from templates with fallbacks
- [ ] Cultural intelligence documented in templates only

## Migration Path

### From Hardcoded to Template-Based

**Step 1**: Identify hardcoded prompt strings in C# code
**Step 2**: Extract to template files by personality
**Step 3**: Update C# code to load templates with fallbacks
**Step 4**: Test cultural translation functionality
**Step 5**: Document cultural mappings in templates

### Future Enhancements

**Dynamic Templates**: Templates could be loaded from databases for runtime customization
**Template Inheritance**: Base templates with personality-specific overrides
**A/B Testing**: Multiple template versions for optimization
**Analytics**: Track template effectiveness and cultural translation success rates

## Conclusion

The template-based prompt system ensures that cultural intelligence remains configurable and maintainable while preventing architectural violations. By keeping cultural knowledge in template files rather than C# code, the system maintains the fail-fast principle and enables easy customization per store and culture.

**Key Achievement**: Complete elimination of hardcoded cultural assumptions in C# code while maintaining full cultural intelligence functionality through a robust template system.
