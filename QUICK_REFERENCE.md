# Quick Reference: Group-Based WAF Rules

## Schema at a Glance

```
???????????????????????????????????????????????????????????????????
?                        WafRuleEntity                            ?
?  ??????????????????????????????????????????????????????????    ?
?  ? Id, Nombre, ActionId, AppId, Prioridad, Habilitado    ?    ?
?  ??????????????????????????????????????????????????????????    ?
???????????????????????????????????????????????????????????????????
                  ?                            ?
         ????????????????????        ?????????????????????
         ?   WafGroups      ?        ? WafConditionEntity ?
         ?  (NEW SCHEMA)    ?        ?  (LEGACY SCHEMA)   ?
         ????????????????????        ?????????????????????
         ? Id               ?        ? WafRuleEntityId   ?
         ? WafRuleId (FK)   ?        ? LogicOperator     ?
         ? GroupOrder       ?        ? ConditionOrder    ?
         ? CreationDate     ?        ? WafGroupId=NULL   ?
         ????????????????????        ?????????????????????
                  ?
         ????????????????????
         ?WafConditionEntity?
         ?  (NEW SCHEMA)    ?
         ????????????????????
         ? WafGroupId (FK)  ?
         ? FieldId          ?
         ? OperatorId       ?
         ? Valor            ?
         ? Negate           ?
         ????????????????????
```

## Rule Evaluation Logic

### New Schema (Group-Based)
```
Rule Matches IF:
    (Group 1: Cond1 AND Cond2 AND Cond3)
    OR
    (Group 2: Cond4 AND Cond5)
    OR
    (Group 3: Cond6)
```

### Legacy Schema (Flat)
```
Rule Matches based on:
    Cond1 LogicOp1 Cond2 LogicOp2 Cond3 ...
    (where LogicOp = AND or OR)
```

## Quick SQL Snippets

### Create Group-Based Rule
```sql
-- 1. Create rule
INSERT INTO WafRuleEntity (Nombre, ActionId, AppId, Prioridad, Habilitado)
VALUES ('My Rule', 2, @AppId, 10, 1);
DECLARE @RuleId INT = SCOPE_IDENTITY();

-- 2. Create group
INSERT INTO WafGroups (WafRuleId, GroupOrder, CreationDate)
VALUES (@RuleId, 1, GETUTCDATE());
DECLARE @GroupId INT = SCOPE_IDENTITY();

-- 3. Add conditions
INSERT INTO WafConditionEntity (FieldId, OperatorId, Valor, WafGroupId)
VALUES (21, 1, 'XX', @GroupId);  -- Country = XX
```

### Check Rule Type
```sql
SELECT 
    r.Id,
    r.Nombre,
    CASE 
        WHEN EXISTS(SELECT 1 FROM WafGroups WHERE WafRuleId = r.Id)
        THEN 'Group-Based'
        ELSE 'Legacy'
    END as Type
FROM WafRuleEntity r;
```

### View Rule with Groups
```sql
SELECT 
    r.Nombre,
    g.GroupOrder as [Group],
    f.Name as Field,
    o.Name as Operator,
    c.Valor as Value,
    c.Negate
FROM WafRuleEntity r
INNER JOIN WafGroups g ON g.WafRuleId = r.Id
INNER JOIN WafConditionEntity c ON c.WafGroupId = g.Id
INNER JOIN Field f ON c.FieldId = f.Id
INNER JOIN Operator o ON c.OperatorId = o.Id
WHERE r.Id = @RuleId
ORDER BY g.GroupOrder, c.Id;
```

## Field IDs Quick Reference

| ID | Field | Example |
|----|-------|---------|
| 1 | cookie | "session_id" |
| 2 | hostname | "example.com" |
| 3 | ip | "192.168.1.1" |
| 7 | method | "POST" |
| 9 | user-agent | "Mozilla/5.0..." |
| 13 | absolute path | "/admin/login" |
| 14 | path and query | "/api?key=123" |
| 21 | country-iso2 | "US" |

## Operator IDs Quick Reference

| ID | Operator | Example |
|----|----------|---------|
| 1 | equals | value == "exact" |
| 3 | contains | value.Contains("substring") |
| 5 | matches regex | Regex.IsMatch(value, pattern) |
| 7 | starts with | value.StartsWith("prefix") |
| 13 | is in list | value in ["A","B","C"] |
| 15 | ip in range | IP in "10.0.0.0/8" |
| 17 | greater than | value > 100 |
| 21 | is present | !string.IsNullOrEmpty(value) |

## Action IDs

| ID | Action | Description |
|----|--------|-------------|
| 1 | Skip | Allow, no further processing |
| 2 | Block | Return 403 page |
| 3 | Managed Challenge | Auto-verify after 3s |
| 4 | Interactive Challenge | User must click checkbox |
| 5 | Log | Log only, allow request |

## Common Patterns

### 1. Block by Country
```sql
-- Block: Country in [XX, YY, ZZ]
INSERT INTO WafConditionEntity (FieldId, OperatorId, Valor, WafGroupId)
VALUES (21, 13, 'XX,YY,ZZ', @GroupId);
```

### 2. Challenge Admin Access from Outside Office
```sql
-- Challenge: Path = /admin AND IP NOT in office range
INSERT INTO WafConditionEntity (FieldId, OperatorId, Valor, WafGroupId, Negate)
VALUES 
    (14, 7, '/admin', @GroupId, 0),                    -- Path starts /admin
    (15, 15, '192.168.1.0/24', @GroupId, 1);           -- IP NOT in range (negated)
```

### 3. Block Bots
```sql
-- Block: UserAgent matches bot pattern
INSERT INTO WafConditionEntity (FieldId, OperatorId, Valor, WafGroupId)
VALUES (9, 5, '.*(bot|crawler|spider).*', @GroupId);
```

### 4. Protect API Endpoints
```sql
-- Block: Path = /api AND Cookie "api_key" not present
INSERT INTO WafConditionEntity (FieldId, OperatorId, Valor, FieldName, WafGroupId)
VALUES 
    (14, 7, '/api/', NULL, @GroupId),           -- Path
    (1, 22, NULL, 'api_key', @GroupId);         -- Cookie not present
```

## Migration Cheat Sheet

### Legacy to Group (Single Group)
```sql
-- Move all conditions to one group
INSERT INTO WafGroups (WafRuleId, GroupOrder, CreationDate)
VALUES (@RuleId, 1, GETUTCDATE());

UPDATE WafConditionEntity
SET WafGroupId = SCOPE_IDENTITY()
WHERE WafRuleEntityId = @RuleId AND WafGroupId IS NULL;
```

### Split OR Conditions to Groups
```sql
-- Condition with LogicOperator=2 (OR) ? New group
-- Before: A AND B OR C
-- After: (A AND B) OR (C)

INSERT INTO WafGroups (WafRuleId, GroupOrder, CreationDate)
VALUES (@RuleId, 1, GETUTCDATE());
DECLARE @G1 INT = SCOPE_IDENTITY();

INSERT INTO WafGroups (WafRuleId, GroupOrder, CreationDate)
VALUES (@RuleId, 2, GETUTCDATE());
DECLARE @G2 INT = SCOPE_IDENTITY();

UPDATE WafConditionEntity SET WafGroupId = @G1 WHERE Id IN (1,2);
UPDATE WafConditionEntity SET WafGroupId = @G2 WHERE Id = 3;
```

## Troubleshooting

### Rule Not Matching?
1. Check `Habilitado = 1`
2. Check `Prioridad` (lower = higher priority)
3. Verify conditions: `SELECT * FROM WafConditionEntity WHERE WafGroupId = @GroupId`
4. Test each condition individually

### Legacy Rule Not Loading?
1. Verify `WafGroupId IS NULL`
2. Check `WafRuleEntityId` is set
3. Check cache: Clear with `HttpRuntime.Cache.Remove("WAF_RULES_hostname")`

### Performance Slow?
1. Add indexes:
   ```sql
   CREATE INDEX IX_WafGroups_WafRuleId ON WafGroups(WafRuleId);
   CREATE INDEX IX_WafConditionEntity_WafGroupId ON WafConditionEntity(WafGroupId);
   ```
2. Check rule count: Aim for < 50 rules per host
3. Simplify regex patterns

## Code Examples

### C# - Create Rule Programmatically
```csharp
var rule = new WafRule
{
    Nombre = "Block Bad Bots",
    ActionId = 2,  // Block
    Prioridad = 10,
    Groups = new List<WafGroup>
    {
        new WafGroup
        {
            Conditions = new List<WafCondition>
            {
                new WafCondition { FieldId = 9, OperatorId = 5, Valor = ".*bot.*" }
            }
        }
    }
};
```

### C# - Evaluate Rule
```csharp
bool matches = _module.EvaluateRule(rule, request);
if (matches)
{
    _module.HandleRuleAction(rule, request, response, rayId, iso2);
}
```

## Performance Tips

? **Order groups by likelihood** - Put most common matches first  
? **Order conditions by speed** - Fast checks (equals) before slow (regex)  
? **Use caching** - Rules cached for 1 minute  
? **Limit regex** - Use simple operators when possible  
? **Index properly** - See indexes in schema docs  
? **Monitor** - Track rule evaluation time  

## Best Practices

? Test in Log mode first (ActionId=5)  
? Use descriptive rule names  
? Document complex regex patterns  
? Reserve priorities 1-10 for critical rules  
? Keep groups simple (3-5 conditions max)  
? Use negation sparingly  
? Monitor false positives  

## Support

?? Full documentation in:
- `DATABASE_SCHEMA_MIGRATION.md`
- `WAF_REPOSITORY_USAGE_EXAMPLES.md`
- `WAF_EVALUATION_FLOW.md`

?? Quick Start: Use examples in `WAF_REPOSITORY_USAGE_EXAMPLES.md`

---
**Version**: 2025.1.1.1  
**Last Updated**: 2025-01-XX  
**Status**: ? Production Ready
