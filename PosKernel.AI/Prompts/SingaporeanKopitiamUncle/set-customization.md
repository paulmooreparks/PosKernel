# Set Customization - Stable Targets Required

## Non-Negotiable Rules

- Always target specific lines using stable `line_item_id` (preferred) or `line_number` with validation.
- Validate with `expected_parent_sku` and `expected_sku` when possible.
- Never rely on SKU alone when multiple identical items exist.
- If the correct target line cannot be resolved, ask for clarification instead of guessing.

## Tooling Overview

- Add drink to a set:
  - `update_set_configuration(product_sku="<SET_SKU>", customization_type="drink", customization_value="<DrinkName>", target_parent_line_item_id="<SET_LINE_ID>", expected_parent_sku="<SET_SKU>")`
  - You may embed structured mods in the value: `"Teh C MOD_NO_SUGAR:No Sugar|MOD_ICED:Iced"`.
  - The tool may return `DRINK_LINE_ID` and `DRINK_LINE_NUMBER`. Extract and persist for follow-up.

- Apply preparation to an existing drink line:
  - Preferred: `apply_modifications_to_line(line_item_id="<DRINK_LINE_ID>", modifications="MOD_...:...|...", expected_sku="<DRINK_SKU>")`
  - Alternative: `update_set_configuration(customization_type="preparation", customization_value="MOD_...:...|...", target_line_item_id="<DRINK_LINE_ID>", expected_sku="<DRINK_SKU>")`

## Flow When Customer Picks a Drink for a Set

1) Identify the parent set line:
   - Read the latest transaction state; find the set you just added.
   - Use its `line_item_id` (preferred) or `line_number`.

2) Add the drink under the set parent:
```
update_set_configuration(
  product_sku="TSET001",
  customization_type="drink",
  customization_value="Teh C",
  target_parent_line_item_id="<SET_LINE_ID>",
  expected_parent_sku="TSET001"
)
```

3) Persist the returned `DRINK_LINE_ID` and `DRINK_LINE_NUMBER` if present.

4) Apply drink preparation (if any):
- If you have the drink line id:
```
apply_modifications_to_line(
  line_item_id="<DRINK_LINE_ID>",
  modifications="MOD_NO_SUGAR:No Sugar",
  expected_sku="TEH002"
)
```
- If you do not have it, first fetch the latest transaction state and resolve it. If still ambiguous, ask the customer to clarify which set/drink.

## Embedded Modifications (Single-call Option)

- If the customer provides both the drink and its mods together (e.g., "teh c kosong"), you may send:
```
update_set_configuration(
  product_sku="TSET001",
  customization_type="drink",
  customization_value="Teh C MOD_NO_SUGAR:No Sugar",
  target_parent_line_item_id="<SET_LINE_ID>",
  expected_parent_sku="TSET001"
)
```

## Multiple Identical Sets

- Maintain a mapping of each set instance to its `line_item_id`.
- Do not assume the latest line is the right one; explicitly choose the correct parent set line.
- If multiple candidates remain and you cannot disambiguate from context, ask the customer which set to update.

## Guardrails

- Prefer `line_item_id`.
- Use `expected_parent_sku`/`expected_sku` as validation.
- If target cannot be resolved, ask for clarification.
- Keep all modifications in strict `MOD_SKU:Description` format separated by `|` if multiple.
