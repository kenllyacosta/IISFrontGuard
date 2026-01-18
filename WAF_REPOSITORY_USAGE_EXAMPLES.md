# WAF Rule Repository Usage Examples

## Overview

This guide provides practical examples of how to create, query, and manage WAF rules using both the new group-based schema and legacy flat schema.

## Creating Rules

### Example 1: Simple Block Rule (One Group, Multiple Conditions)

Block requests from a specific country with suspicious user agents.

```sql
-- Step 1: Create the rule
INSERT INTO WafRuleEntity (Nombre, ActionId, AppId, Prioridad, Habilitado, CreationDate)
VALUES (
    'Block Bots from High-Risk Country', 
    2, -- Block action
    '12345678-1234-1234-1234-123456789012', -- Your AppId
    10, 
    1, 
    GETUTCDATE()
);

DECLARE @RuleId INT = SCOPE_IDENTITY();

-- Step 2: Create a group
INSERT INTO WafGroups (WafRuleId, GroupOrder, CreationDate)
VALUES (@RuleId, 1, GETUTCDATE());

DECLARE @GroupId INT = SCOPE_IDENTITY();

-- Step 3: Add conditions to the group
INSERT INTO WafConditionEntity (FieldId, OperatorId, Valor, FieldName, WafGroupId, Negate, CreationDate)
VALUES 
    (21, 1, 'XX', NULL, @GroupId, 0, GETUTCDATE()),  -- Country ISO2 equals "XX"
    (9, 3, 'bot', NULL, @GroupId, 0, GETUTCDATE());  -- UserAgent contains "bot"

-- Result: Blocks if (Country = "XX" AND UserAgent contains "bot")
```

### Example 2: Multi-Group Challenge Rule

Issue a managed challenge for multiple suspicious patterns.

```sql
DECLARE @AppId UNIQUEIDENTIFIER = '12345678-1234-1234-1234-123456789012';

-- Create rule
INSERT INTO WafRuleEntity (Nombre, ActionId, AppId, Prioridad, Habilitado, CreationDate)
VALUES ('Managed Challenge for Suspicious Activity', 3, @AppId, 20, 1, GETUTCDATE());

DECLARE @RuleId INT = SCOPE_IDENTITY();

-- Group 1: Suspicious login attempts from untrusted IPs
INSERT INTO WafGroups (WafRuleId, GroupOrder, CreationDate) 
VALUES (@RuleId, 1, GETUTCDATE());
DECLARE @Group1 INT = SCOPE_IDENTITY();

INSERT INTO WafConditionEntity (FieldId, OperatorId, Valor, FieldName, WafGroupId, CreationDate)
VALUES 
    (14, 7, '/login', NULL, @Group1, GETUTCDATE()),     -- Path starts with "/login"
    (7, 1, 'POST', NULL, @Group1, GETUTCDATE()),        -- Method equals "POST"
    (15, 16, '10.0.0.0/8', NULL, @Group1, GETUTCDATE()); -- IP NOT in trusted range

-- Group 2: High request rate from single IP
INSERT INTO WafGroups (WafRuleId, GroupOrder, CreationDate) 
VALUES (@RuleId, 2, GETUTCDATE());
DECLARE @Group2 INT = SCOPE_IDENTITY();

INSERT INTO WafConditionEntity (FieldId, OperatorId, Valor, FieldName, WafGroupId, CreationDate)
VALUES 
    (19, 17, '10000', NULL, @Group2, GETUTCDATE());     -- Body length > 10000

-- Group 3: Scraper user agents
INSERT INTO WafGroups (WafRuleId, GroupOrder, CreationDate) 
VALUES (@RuleId, 3, GETUTCDATE());
DECLARE @Group3 INT = SCOPE_IDENTITY();

INSERT INTO WafConditionEntity (FieldId, OperatorId, Valor, FieldName, WafGroupId, CreationDate)
VALUES 
    (9, 5, '.*(curl|wget|scrapy).*', NULL, @Group3, GETUTCDATE()); -- UserAgent matches regex

-- Result: Challenge if Group1 OR Group2 OR Group3 matches
```

### Example 3: Admin Protection with Negation

Protect admin area but allow access from office IPs.

```sql
DECLARE @AppId UNIQUEIDENTIFIER = '12345678-1234-1234-1234-123456789012';

INSERT INTO WafRuleEntity (Nombre, ActionId, AppId, Prioridad, Habilitado, CreationDate)
VALUES ('Admin Area Protection', 4, @AppId, 5, 1, GETUTCDATE());

DECLARE @RuleId INT = SCOPE_IDENTITY();

-- Single group with negated condition
INSERT INTO WafGroups (WafRuleId, GroupOrder, CreationDate) 
VALUES (@RuleId, 1, GETUTCDATE());
DECLARE @GroupId INT = SCOPE_IDENTITY();

INSERT INTO WafConditionEntity (FieldId, OperatorId, Valor, FieldName, WafGroupId, Negate, CreationDate)
VALUES 
    (14, 7, '/admin', NULL, @GroupId, 0, GETUTCDATE()),         -- Path starts with "/admin"
    (15, 15, '192.168.1.0/24', NULL, @GroupId, 1, GETUTCDATE()); -- IP NOT in office range (negated)

-- Result: Challenge if (Path = /admin AND IP is NOT in 192.168.1.0/24)
```

### Example 4: Complex Cookie-Based Rule

Check for specific cookies and their values.

```sql
DECLARE @AppId UNIQUEIDENTIFIER = '12345678-1234-1234-1234-123456789012';

INSERT INTO WafRuleEntity (Nombre, ActionId, AppId, Prioridad, Habilitado, CreationDate)
VALUES ('Cookie Security Check', 2, @AppId, 15, 1, GETUTCDATE());

DECLARE @RuleId INT = SCOPE_IDENTITY();

-- Group 1: Missing authentication cookie
INSERT INTO WafGroups (WafRuleId, GroupOrder, CreationDate) 
VALUES (@RuleId, 1, GETUTCDATE());
DECLARE @Group1 INT = SCOPE_IDENTITY();

INSERT INTO WafConditionEntity (FieldId, OperatorId, Valor, FieldName, WafGroupId, CreationDate)
VALUES 
    (14, 7, '/api/', NULL, @Group1, GETUTCDATE()),            -- Path starts with "/api/"
    (1, 22, NULL, 'auth_token', @Group1, GETUTCDATE());       -- Cookie "auth_token" not present

-- Group 2: Suspicious session cookie value
INSERT INTO WafGroups (WafRuleId, GroupOrder, CreationDate) 
VALUES (@RuleId, 2, GETUTCDATE());
DECLARE @Group2 INT = SCOPE_IDENTITY();

INSERT INTO WafConditionEntity (FieldId, OperatorId, Valor, FieldName, WafGroupId, CreationDate)
VALUES 
    (1, 5, '.*<script.*', 'session_id', @Group2, GETUTCDATE()); -- Cookie "session_id" matches XSS pattern

-- Result: Block if (API path AND no auth cookie) OR (session cookie has XSS)
```

## Querying Rules

### View All Rules with Their Type

```sql
SELECT 
    r.Id,
    r.Nombre,
    r.Prioridad,
    r.Habilitado,
    a.Name as ActionName,
    CASE 
        WHEN EXISTS(SELECT 1 FROM WafGroups WHERE WafRuleId = r.Id) 
        THEN 'Group-Based (New)'
        ELSE 'Flat List (Legacy)'
    END as RuleType,
    (SELECT COUNT(*) FROM WafGroups WHERE WafRuleId = r.Id) as GroupCount,
    (SELECT COUNT(*) 
     FROM WafConditionEntity 
     WHERE WafRuleEntityId = r.Id) as TotalConditions
FROM WafRuleEntity r
INNER JOIN [Action] a ON r.ActionId = a.Id
INNER JOIN AppEntity app ON r.AppId = app.Id
WHERE app.Host = 'localhost'
ORDER BY r.Prioridad;
```

### View Rule Details with Groups

```sql
DECLARE @RuleId INT = 1;

SELECT 
    r.Nombre as RuleName,
    g.GroupOrder,
    c.Id as ConditionId,
    f.Name as FieldName,
    o.Name as OperatorName,
    c.Valor as Value,
    c.FieldName as CustomFieldName,
    c.Negate as IsNegated
FROM WafRuleEntity r
INNER JOIN WafGroups g ON g.WafRuleId = r.Id
INNER JOIN WafConditionEntity c ON c.WafGroupId = g.Id
INNER JOIN Field f ON c.FieldId = f.Id
INNER JOIN Operator o ON c.OperatorId = o.Id
WHERE r.Id = @RuleId
ORDER BY g.GroupOrder, c.Id;
```

### View Legacy Rule Details

```sql
DECLARE @RuleId INT = 1;

SELECT 
    r.Nombre as RuleName,
    c.ConditionOrder,
    f.Name as FieldName,
    o.Name as OperatorName,
    c.Valor as Value,
    c.FieldName as CustomFieldName,
    CASE c.LogicOperator
        WHEN 1 THEN 'AND'
        WHEN 2 THEN 'OR'
        ELSE 'Unknown'
    END as LogicOperator
FROM WafRuleEntity r
INNER JOIN WafConditionEntity c ON c.WafRuleEntityId = r.Id
INNER JOIN Field f ON c.FieldId = f.Id
INNER JOIN Operator o ON c.OperatorId = o.Id
WHERE r.Id = @RuleId 
  AND c.WafGroupId IS NULL
ORDER BY c.ConditionOrder;
```

## Modifying Rules

### Add a New Group to Existing Rule

```sql
DECLARE @RuleId INT = 1;

-- Add new group at the end
INSERT INTO WafGroups (WafRuleId, GroupOrder, CreationDate)
VALUES (@RuleId, 
        (SELECT ISNULL(MAX(GroupOrder), 0) + 1 FROM WafGroups WHERE WafRuleId = @RuleId),
        GETUTCDATE());

DECLARE @NewGroupId INT = SCOPE_IDENTITY();

-- Add conditions to the new group
INSERT INTO WafConditionEntity (FieldId, OperatorId, Valor, WafGroupId, CreationDate)
VALUES (21, 1, 'CN', @NewGroupId, GETUTCDATE());  -- Country equals "CN"
```

### Modify Existing Condition

```sql
-- Update condition value
UPDATE WafConditionEntity
SET Valor = 'updated_value',
    Negate = 1  -- Also negate it
WHERE Id = 123;

-- Change operator
UPDATE WafConditionEntity
SET OperatorId = 5  -- Change to regex match
WHERE Id = 123;
```

### Reorder Groups

```sql
DECLARE @RuleId INT = 1;

-- Swap group orders (make group 3 become group 1)
UPDATE WafGroups
SET GroupOrder = CASE 
    WHEN GroupOrder = 1 THEN 3
    WHEN GroupOrder = 3 THEN 1
    ELSE GroupOrder
END
WHERE WafRuleId = @RuleId 
  AND GroupOrder IN (1, 3);
```

### Delete a Group

```sql
-- Delete group (conditions will be cascade deleted)
DELETE FROM WafGroups
WHERE Id = 123;
```

## Migration Examples

### Migrate Legacy Rule to Group-Based

```sql
DECLARE @RuleId INT = 1;

BEGIN TRANSACTION;

    -- Step 1: Create a group for all existing conditions
    INSERT INTO WafGroups (WafRuleId, GroupOrder, CreationDate)
    VALUES (@RuleId, 1, GETUTCDATE());
    
    DECLARE @GroupId INT = SCOPE_IDENTITY();
    
    -- Step 2: Move all conditions to the group
    UPDATE WafConditionEntity
    SET WafGroupId = @GroupId,
        Negate = 0  -- Set default negate
    WHERE WafRuleEntityId = @RuleId 
      AND WafGroupId IS NULL;
    
    -- Step 3: Verify migration
    IF NOT EXISTS (
        SELECT 1 FROM WafConditionEntity 
        WHERE WafRuleEntityId = @RuleId AND WafGroupId IS NULL
    )
    BEGIN
        COMMIT TRANSACTION;
        PRINT 'Migration successful';
    END
    ELSE
    BEGIN
        ROLLBACK TRANSACTION;
        PRINT 'Migration failed - conditions remain';
    END
```

### Split OR Conditions into Separate Groups

```sql
DECLARE @RuleId INT = 1;

-- Assume we have:
-- Condition 1: Country=XX (LogicOperator=1, AND)
-- Condition 2: Country=YY (LogicOperator=2, OR)  <- Should be in Group 2
-- Condition 3: UserAgent=bot (LogicOperator=1, AND)

BEGIN TRANSACTION;

    -- Group 1: Condition 1 and 3
    INSERT INTO WafGroups (WafRuleId, GroupOrder, CreationDate) 
    VALUES (@RuleId, 1, GETUTCDATE());
    DECLARE @Group1 INT = SCOPE_IDENTITY();
    
    UPDATE WafConditionEntity
    SET WafGroupId = @Group1
    WHERE WafRuleEntityId = @RuleId 
      AND Id IN (
          SELECT Id FROM WafConditionEntity 
          WHERE WafRuleEntityId = @RuleId 
            AND ConditionOrder IN (1, 3)
      );
    
    -- Group 2: Condition 2
    INSERT INTO WafGroups (WafRuleId, GroupOrder, CreationDate) 
    VALUES (@RuleId, 2, GETUTCDATE());
    DECLARE @Group2 INT = SCOPE_IDENTITY();
    
    UPDATE WafConditionEntity
    SET WafGroupId = @Group2
    WHERE WafRuleEntityId = @RuleId 
      AND ConditionOrder = 2;
    
    COMMIT TRANSACTION;

-- Result: (Country=XX AND UserAgent=bot) OR (Country=YY)
```

## Testing Rules

### Test Rule Evaluation (Simulation)

```sql
-- Simulated test: Would this request match the rule?
DECLARE @RuleId INT = 1;
DECLARE @TestCountry CHAR(2) = 'XX';
DECLARE @TestUserAgent NVARCHAR(1024) = 'Mozilla/5.0 bot scanner';

-- Find all groups and conditions for the rule
WITH RuleGroups AS (
    SELECT g.Id as GroupId, g.GroupOrder
    FROM WafGroups g
    WHERE g.WafRuleId = @RuleId
),
GroupConditions AS (
    SELECT 
        c.WafGroupId,
        c.FieldId,
        c.OperatorId,
        c.Valor,
        c.Negate,
        f.Name as FieldName
    FROM WafConditionEntity c
    INNER JOIN Field f ON c.FieldId = f.Id
    WHERE c.WafGroupId IN (SELECT GroupId FROM RuleGroups)
)
SELECT * FROM GroupConditions
ORDER BY WafGroupId;

-- Manual evaluation:
-- Group 1: Country=XX (21,1,'xx') ? YES AND UserAgent contains 'bot' (9,3,'bot') ? YES
-- Group matches: YES
-- Rule matches: YES (at least one group matched)
```

### Performance Testing

```sql
-- Measure rule loading performance
SET STATISTICS TIME ON;
SET STATISTICS IO ON;

DECLARE @Host NVARCHAR(255) = 'localhost';

SELECT 
    r.Id,
    r.Nombre,
    r.ActionId,
    g.GroupOrder,
    c.FieldId,
    c.OperatorId,
    c.Valor
FROM WafRuleEntity r
INNER JOIN AppEntity a ON r.AppId = a.Id
LEFT JOIN WafGroups g ON g.WafRuleId = r.Id
LEFT JOIN WafConditionEntity c ON c.WafGroupId = g.Id
WHERE a.Host = @Host 
  AND r.Habilitado = 1
ORDER BY r.Prioridad, g.GroupOrder, c.Id;

SET STATISTICS TIME OFF;
SET STATISTICS IO OFF;
```

## Common Patterns

### Pattern 1: Geographic Blocking

Block specific countries or regions:

```sql
DECLARE @RuleId INT;
INSERT INTO WafRuleEntity (Nombre, ActionId, AppId, Prioridad, Habilitado, CreationDate)
VALUES ('Block High-Risk Countries', 2, @AppId, 10, 1, GETUTCDATE());
SET @RuleId = SCOPE_IDENTITY();

INSERT INTO WafGroups (WafRuleId, GroupOrder, CreationDate) 
VALUES (@RuleId, 1, GETUTCDATE());
DECLARE @GroupId INT = SCOPE_IDENTITY();

-- Block list of countries
INSERT INTO WafConditionEntity (FieldId, OperatorId, Valor, WafGroupId, CreationDate)
VALUES (21, 13, 'XX,YY,ZZ', @GroupId, GETUTCDATE());  -- Country in list
```

### Pattern 2: Rate Limiting by Body Size

Challenge large POST requests:

```sql
INSERT INTO WafRuleEntity (Nombre, ActionId, AppId, Prioridad, Habilitado, CreationDate)
VALUES ('Large POST Challenge', 3, @AppId, 30, 1, GETUTCDATE());
DECLARE @RuleId INT = SCOPE_IDENTITY();

INSERT INTO WafGroups (WafRuleId, GroupOrder, CreationDate) 
VALUES (@RuleId, 1, GETUTCDATE());
DECLARE @GroupId INT = SCOPE_IDENTITY();

INSERT INTO WafConditionEntity (FieldId, OperatorId, Valor, WafGroupId, CreationDate)
VALUES 
    (7, 1, 'POST', @GroupId, GETUTCDATE()),      -- Method = POST
    (19, 17, '50000', @GroupId, GETUTCDATE());   -- Body length > 50000
```

### Pattern 3: Path-Based Protection

Protect specific paths with different rules:

```sql
-- Admin path requires office IP
INSERT INTO WafRuleEntity (Nombre, ActionId, AppId, Prioridad, Habilitado, CreationDate)
VALUES ('Admin IP Restriction', 2, @AppId, 1, 1, GETUTCDATE());
DECLARE @AdminRuleId INT = SCOPE_IDENTITY();

INSERT INTO WafGroups (WafRuleId, GroupOrder, CreationDate) 
VALUES (@AdminRuleId, 1, GETUTCDATE());
DECLARE @AdminGroupId INT = SCOPE_IDENTITY();

INSERT INTO WafConditionEntity (FieldId, OperatorId, Valor, WafGroupId, Negate, CreationDate)
VALUES 
    (14, 7, '/admin', @AdminGroupId, 0, GETUTCDATE()),              -- Path starts with /admin
    (15, 15, '192.168.1.0/24', @AdminGroupId, 1, GETUTCDATE());     -- IP NOT in office range

-- API path requires valid API key cookie
INSERT INTO WafRuleEntity (Nombre, ActionId, AppId, Prioridad, Habilitado, CreationDate)
VALUES ('API Key Validation', 2, @AppId, 2, 1, GETUTCDATE());
DECLARE @ApiRuleId INT = SCOPE_IDENTITY();

INSERT INTO WafGroups (WafRuleId, GroupOrder, CreationDate) 
VALUES (@ApiRuleId, 1, GETUTCDATE());
DECLARE @ApiGroupId INT = SCOPE_IDENTITY();

INSERT INTO WafConditionEntity (FieldId, OperatorId, Valor, FieldName, WafGroupId, CreationDate)
VALUES 
    (14, 7, '/api/', @ApiGroupId, GETUTCDATE()),              -- Path starts with /api/
    (1, 22, NULL, 'api_key', @ApiGroupId, GETUTCDATE());      -- Cookie "api_key" not present
```

## Troubleshooting

### Find Rules That Never Match

```sql
-- Rules with no request logs in the last 7 days
SELECT 
    r.Id,
    r.Nombre,
    r.Prioridad,
    DATEDIFF(day, r.CreationDate, GETUTCDATE()) as DaysSinceCreated
FROM WafRuleEntity r
WHERE r.Habilitado = 1
  AND NOT EXISTS (
      SELECT 1 
      FROM RequestContext req 
      WHERE req.RuleId = r.Id 
        AND req.CreatedAt >= DATEADD(day, -7, GETUTCDATE())
  )
ORDER BY r.Prioridad;
```

### Find Orphaned Conditions

```sql
-- Conditions not linked to any group or rule
SELECT c.*
FROM WafConditionEntity c
LEFT JOIN WafGroups g ON c.WafGroupId = g.Id
WHERE c.WafGroupId IS NOT NULL 
  AND g.Id IS NULL;
```

### Find Duplicate Rules

```sql
-- Rules with same name and action
SELECT Nombre, ActionId, COUNT(*) as RuleCount
FROM WafRuleEntity
GROUP BY Nombre, ActionId
HAVING COUNT(*) > 1;
```

## Best Practices

1. **Group Ordering**: Lower `GroupOrder` = evaluated first (fail-fast principle)
2. **Condition Ordering**: Within a group, put fastest conditions first
3. **Negation**: Use sparingly - structure conditions positively when possible
4. **Testing**: Always test rules in "Log" mode before enabling "Block"
5. **Naming**: Use descriptive names that explain the rule's purpose
6. **Priority**: Reserve low priorities (1-10) for critical security rules
7. **Documentation**: Add comments in SQL scripts explaining complex logic

## Summary

This guide shows how to:
- ? Create group-based rules from scratch
- ? Query and view rules effectively
- ? Modify existing rules safely
- ? Migrate from legacy to group-based schema
- ? Test and troubleshoot rules
- ? Implement common security patterns

The new group-based schema provides maximum flexibility while maintaining full backward compatibility with existing rules.
