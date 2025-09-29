# AI Template System - Developer Guide

**Target Audience**: Developers working on POS Kernel AI integration
**Purpose**: Practical guide for using and extending the template-based prompt system
**Prerequisites**: Familiarity with C# development and AI integration concepts

## Quick Start

### 1. Understanding the Template System

The POS Kernel uses a template-based prompt system to ensure cultural intelligence remains configurable rather than hardcoded in C# code.

**Core Principle**: **NEVER** hardcode cultural assumptions in C# - use templates instead.

```csharp
// ❌ WRONG - Hardcoded cultural assumption
if (userInput.Contains("roti bakar")) {
    searchTerm = "toast";
}

// ✅ RIGHT - Template-driven cultural intelligence
var prompt = AiPersonalityFactory.BuildPrompt(_personality.Type, "ordering", context);
// Template contains cultural translation rules
```

### 2. Template Directory Structure

```
~/.poskernel/ai_config/prompts/OpenAI/gpt-4o/
├── SingaporeanKopitiamUncle/
│   ├── ordering.md               # Main ordering intelligence
│   ├── greeting.md               # Store greeting
│   ├── tool-analysis-context.md  # Tool execution context (NEW)
│   ├── fast-interpret.md         # Quick phrase processing (NEW)
│   └── discovery-results.md      # Search result presentation (NEW)
├── GenericCashier/
│   ├── ordering.md
│   ├── tool-analysis-context.md
│   ├── fast-interpret.md
│   └── discovery-results.md
└── [Other personalities...]
```

### 3. Key Template Types

**Core Personality Templates**:
- `ordering.md` - Main conversation intelligence and cultural translations
- `greeting.md` - Store-specific greeting behavior
- `payment_complete.md` - Post-payment responses

**Technical Context Templates (NEW)**:
- `tool-analysis-context.md` - Structured context for AI tool execution
- `fast-interpret.md` - Context for quick phrase interpretation
- `discovery-results.md` - Format for presenting search results

## Working with Templates

### Loading Templates in C# Code

**Pattern**: Always use template loading with fail-fast fallbacks:

```csharp
private string BuildPromptWithTemplate(string templateName, PromptContext context)
{
    try
    {
        // Load template-based prompt
        var templatePrompt = AiPersonalityFactory.BuildPrompt(
            _personality.Type, templateName, context);
        return templatePrompt;
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Failed to load {Template} template, using fallback", templateName);
        // Minimal fallback - NO cultural assumptions
        return "Follow your personality-specific rules for this interaction.";
    }
}
```

**Key Methods**:
- `BuildToolAnalysisPrompt()` → Uses `tool-analysis-context.md`
- `BuildFastInterpretPrompt()` → Uses `fast-interpret.md`
- `BuildDiscoveryPrompt()` → Uses `discovery-results.md`

### Template Variables

Templates support variable substitution for dynamic content:

```markdown
# In template file
## Customer said: "{{UserInput}}"
## Current order: {{CartItems}}
## Total: {{CurrentTotal}}
```

```csharp
// In C# code
var context = new PromptContext
{
    UserInput = userInput,
    CartItems = string.Join(", ", _receipt.Items.Select(i => $"{i.Quantity}x {i.ProductName}")),
    CurrentTotal = FormatCurrency(_receipt.Total)
};
```

## Creating New Templates

### For New Personalities

**Step 1**: Create personality directory
```bash
mkdir ~/.poskernel/ai_config/prompts/OpenAI/gpt-4o/[PersonalityName]
```

**Step 2**: Copy base templates from `GenericCashier`
```bash
cp ~/.poskernel/ai_config/prompts/OpenAI/gpt-4o/GenericCashier/*.md \
   ~/.poskernel/ai_config/prompts/OpenAI/gpt-4o/[PersonalityName]/
```

**Step 3**: Customize cultural rules and personality traits

**Step 4**: Test with representative customer inputs

### For New Template Types

**Step 1**: Identify hardcoded prompt text in C# code
```csharp
// Find patterns like this:
prompt += "Hardcoded cultural rule here...";
```

**Step 2**: Extract to template file with clear name
```markdown
# new-template-type.md
Extracted cultural rule here with {{Variable}} support
```

**Step 3**: Update C# code to load template
```csharp
var templatePrompt = AiPersonalityFactory.BuildPrompt(_personality.Type, "new-template-type", context);
```

**Step 4**: Create template for each existing personality

## Cultural Translation Setup

### MenuLanguage Configuration

**Purpose**: Tell AI what language the database product names use.

```csharp
// In StoreConfig.cs
public class StoreConfig
{
    public string MenuLanguage { get; set; } = "en"; // Database language
    public string CultureCode { get; set; } = "en-SG"; // Cultural formatting
}
```

**Usage**:
- `MenuLanguage = "en"` → AI translates cultural terms TO English for database searches
- `CultureCode = "en-SG"` → Singapore formatting for currency, dates, etc.

### Template-Based Cultural Rules

**In `ordering.md`**:
```markdown
**Common Food Translations (Singapore Kopitiam)**:
- "roti bakar kaya set" = search for "kaya toast set" or "Traditional Kaya Toast Set"
- "teh si kosong" = "Teh C" + MOD_NO_SUGAR modification
- "kopi peng" = search for "iced coffee" variations

**MANDATORY PROCESSING RULE**:
If customer input contains ANY cultural terms, use cultural knowledge to translate
them IMMEDIATELY and proceed with translated terms. Do NOT search for original
untranslated terms first.
```

### Translation Flow

1. **Customer**: "set roti bakar kaya"
2. **Template Rule**: "roti bakar kaya set" → search for "kaya toast set"
3. **Database Search**: Find "Traditional Kaya Toast Set" (TSET001)
4. **AI Response**: "OK, Traditional Kaya Toast Set S$7.40. What drink you want?"

## Testing Templates

### Manual Testing

**Test Cultural Translations**:
```bash
# Start demo
cd PosKernel.AI.Demo && dotnet run

# Test inputs
"set roti bakar kaya"     # Should find Traditional Kaya Toast Set
"teh si kosong"           # Should add Teh C with no sugar modification
"kopi peng"               # Should find iced coffee options
```

### Automated Testing

**Template Validation Tests**:
```csharp
[Test]
public void SingaporeanKopitiam_Should_Translate_RotiBarakKaya()
{
    var orchestrator = CreateTestOrchestrator(PersonalityType.SingaporeanKopitiamUncle);
    var response = await orchestrator.ProcessUserInputAsync("set roti bakar kaya");

    Assert.That(response.Content, Should.Contain("Traditional Kaya Toast Set"));
    Assert.That(_receipt.Items, Should.HaveCount(1));
    Assert.That(_receipt.Items[0].ProductSku, Should.Equal("TSET001"));
}
```

## Common Issues and Solutions

### Issue: Cultural Translation Not Working

**Symptoms**: AI doesn't find products for cultural terms like "roti bakar kaya"

**Causes**:
1. Template not loading properly
2. Hardcoded C# prompt overriding template rules
3. AI discovery fallback interfering with translation

**Solutions**:
1. Check template file exists and has correct cultural rules
2. Remove any hardcoded prompt text in C# code
3. Verify discovery fallback only triggers when AI makes zero tool calls

**Debug Steps**:
```csharp
// Add logging to see if template loads
_logger.LogInformation("Loading template: {Template} for personality: {Personality}",
    templateName, _personality.Type);

// Check if cultural rules are in the prompt
_logger.LogDebug("Generated prompt contains cultural rules: {HasRules}",
    prompt.Contains("roti bakar"));
```

### Issue: Template Not Found

**Symptoms**: `LogWarning` about failed template loading, fallback prompt used

**Solution**: Create missing template file for the personality

```bash
# Check what templates exist
ls ~/.poskernel/ai_config/prompts/OpenAI/gpt-4o/[PersonalityName]/

# Create missing template from GenericCashier
cp ~/.poskernel/ai_config/prompts/OpenAI/gpt-4o/GenericCashier/[template-name].md \
   ~/.poskernel/ai_config/prompts/OpenAI/gpt-4o/[PersonalityName]/
```

### Issue: Cultural Assumptions Creeping Into C# Code

**Prevention**: Code review checklist
- [ ] No hardcoded cultural terms in C# strings
- [ ] No hardcoded currency symbols ($, €, ¥)
- [ ] No hardcoded time formats or cultural mappings
- [ ] All prompts loaded from templates with fallbacks
- [ ] Cultural intelligence documented in templates only

**Detection**: Search for anti-patterns
```bash
# Find potential hardcoded cultural assumptions
grep -r "roti\|bakar\|kopi\|teh" PosKernel.AI/Core/*.cs
grep -r "\$[0-9]" PosKernel.AI/Core/*.cs  # Find hardcoded currency
grep -r "morning\|afternoon" PosKernel.AI/Core/*.cs  # Find time assumptions
```

## Best Practices

### 1. Template Design

**Do**:
- Document cultural translations explicitly with examples
- Include priority rules (completion detection, cultural vocab, contextual references)
- Provide fallback instructions for edge cases
- Use clear variable names (`{{UserInput}}`, `{{CartItems}}`)

**Don't**:
- Hardcode specific product names or SKUs in templates
- Make assumptions about store configuration
- Include technical implementation details in personality templates

### 2. C# Integration

**Do**:
- Always use template loading with fallback
- Log template loading success/failure for debugging
- Handle missing templates gracefully
- Keep C# prompt building minimal

**Don't**:
- Add cultural assumptions to C# code "temporarily"
- Override template behavior with hardcoded strings
- Make template loading failures silent
- Build complex prompts in C# when templates would be better

### 3. Cultural Intelligence

**Do**:
- Test cultural translations with native speakers
- Document expected input/output examples
- Handle regional variations (e.g., Singapore vs Malaysia terms)
- Provide clear escalation paths for unknown terms

**Don't**:
- Assume cultural terms have one-to-one mappings
- Ignore cultural context (time of day, formality level)
- Hardcode cultural assumptions based on location
- Make the AI guess when cultural knowledge is missing

## Future Enhancements

### Template Inheritance
```markdown
# base-ordering.md (shared rules)
{{include:cultural-translations.md}}
{{include:completion-detection.md}}

# singaporean-kopitiam-ordering.md (personality-specific)
{{inherit:base-ordering.md}}
# Personality-specific overrides here
```

### Dynamic Template Loading
```csharp
// Load templates from database for runtime customization
var template = await _templateService.LoadTemplateAsync(storeId, personalityType, templateName);
```

### A/B Testing
```csharp
// Test different template versions for optimization
var templateVariant = _abTestingService.GetTemplateVariant(storeId, templateName);
```

This template system ensures maintainable, customizable AI cultural intelligence while preventing architectural violations through hardcoded assumptions.
