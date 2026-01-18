# WAF Rule Evaluation Refactoring

## Summary
Refactored the WAF rule evaluation logic to properly support grouping and precedence with the pattern: `(A AND B AND C) OR (D AND E) OR (F)`

## Changes Made

### 1. Updated `Context_BeginRequest` Method
- **Before**: Used `EvaluateConditions(rule.Conditions, request)` - flat condition list
- **After**: Uses `EvaluateRule(rule, request)` - proper group-based evaluation

### 2. Refactored `EvaluateRule` Method
Now implements proper group-based evaluation:
- **Primary Path**: If `rule.Groups` exists and has items, evaluates using group logic
- **Fallback Path**: If only `rule.Conditions` exists (legacy), uses backward-compatible evaluation
- **No Match**: Returns `false` if neither Groups nor Conditions exist

**Evaluation Logic**:
```
Rule Matches = Group1 OR Group2 OR Group3 OR ...
Group Matches = Condition1 AND Condition2 AND Condition3 AND ...
```

### 3. Improved `EvaluateGroup` Method
- Changed from `private` to `private` (kept internal)
- Handles null/empty groups properly
- Implements AND logic across all conditions in a group
- Supports negation via `condition.Negate`
- Uses fail-fast pattern (returns `false` immediately when a condition fails)

### 4. Deprecated `EvaluateConditions` Method
- Renamed internal implementation to public `EvaluateConditions`
- Marked with `[Obsolete]` attribute
- Kept for backward compatibility with legacy rule configurations
- Updated interface to reflect deprecation

### 5. Updated `IFrontGuardModule` Interface
- Added `EvaluateRule(WafRule rule, HttpRequest request)` method signature
- Marked `EvaluateConditions` as `[Obsolete]` in the interface
- Maintains backward compatibility for existing code

## Evaluation Logic Example

### Example 1: Simple Rule with Groups
```csharp
Rule: "Block requests from bad countries or suspicious IPs"
Groups:
  Group 1: Country = "XX" AND User-Agent contains "bot"
  Group 2: IP in range "10.0.0.0/8" AND Method = "POST"

Evaluation: (Group1) OR (Group2)
Result: Matches if EITHER group matches
```

### Example 2: Complex Rule
```csharp
Rule: "Challenge suspicious requests"
Groups:
  Group 1: Path starts with "/admin" AND Cookie "session" is not present
  Group 2: User-Agent matches regex ".*bot.*" AND Referrer is not present
  Group 3: IP not in whitelist AND Rate > 100 req/min

Evaluation: (Group1) OR (Group2) OR (Group3)
Result: Matches if ANY group matches (OR logic between groups)
        Each group matches if ALL conditions match (AND logic within group)
```

## Backward Compatibility

The refactoring maintains full backward compatibility:

1. **Legacy Rules**: Rules using flat `Conditions` list will still work via the fallback path
2. **Test Compatibility**: Existing unit tests continue to work with `EvaluateConditions` (with obsolete warnings)
3. **Interface Compatibility**: The interface still exposes `EvaluateConditions` for existing consumers

## Migration Guide

To migrate existing code to the new pattern:

### Before (Legacy):
```csharp
var conditions = new List<WafCondition>
{
    new WafCondition { FieldId = 3, OperatorId = 1, Valor = "1.2.3.4", LogicOperator = 1 },
    new WafCondition { FieldId = 9, OperatorId = 3, Valor = "bot", LogicOperator = 1 }
};
var rule = new WafRule { Conditions = conditions };
bool matches = module.EvaluateConditions(conditions, request); // Deprecated
```

### After (Group-based):
```csharp
var group = new WafGroup
{
    Conditions = new List<WafCondition>
    {
        new WafCondition { FieldId = 3, OperatorId = 1, Valor = "1.2.3.4" },
        new WafCondition { FieldId = 9, OperatorId = 3, Valor = "bot" }
    }
};
var rule = new WafRule 
{ 
    Groups = new List<WafGroup> { group }
};
bool matches = module.EvaluateRule(rule, request); // Recommended
```

## Benefits

1. **Clear Logic**: The OR-of-ANDs pattern is explicit and easy to understand
2. **Flexibility**: Supports complex rule combinations like Cloudflare/AWS WAF
3. **Performance**: Fail-fast evaluation stops processing as soon as a match is found
4. **Maintainability**: Cleaner separation between group logic and condition logic
5. **Testability**: Each level (Rule ? Group ? Condition) can be tested independently

## Testing

All existing tests pass with this refactoring. The unit tests that use `EvaluateConditions` will show obsolete warnings but continue to function correctly.

## Future Enhancements

Potential improvements for future versions:
1. Add support for configurable group join operators (AND/OR between groups)
2. Add support for nested groups for even more complex logic
3. Add performance metrics for rule evaluation
4. Add caching for frequently evaluated rules
