# Database Schema Migration Guide

## Overview

This guide documents the database schema changes required to support the new group-based WAF rule evaluation model while maintaining backward compatibility with existing rules.

## Schema Changes

### 1. New `WafGroups` Table

The `WafGroups` table represents logical groups of conditions within a WAF rule.

```sql
CREATE TABLE WafGroups (
    Id INT PRIMARY KEY IDENTITY(1,1),
    WafRuleId INT NOT NULL,
    GroupOrder INT NOT NULL,  -- Determines the order of group evaluation
    CreationDate DATETIME2 NOT NULL,
    CONSTRAINT FK_WafGroups_WafRules FOREIGN KEY (WafRuleId) 
        REFERENCES WafRuleEntity(Id) ON DELETE CASCADE
)
```

**Columns:**
- `Id`: Primary key, auto-incrementing
- `WafRuleId`: Foreign key to `WafRuleEntity` - which rule this group belongs to
- `GroupOrder`: Integer defining evaluation order (lower numbers evaluated first)
- `CreationDate`: Timestamp of group creation

**Indexes Recommended:**
```sql
CREATE INDEX IX_WafGroups_WafRuleId ON WafGroups(WafRuleId);
CREATE INDEX IX_WafGroups_RuleId_GroupOrder ON WafGroups(WafRuleId, GroupOrder);
```

### 2. Updated `WafConditionEntity` Table

Added new columns to support group-based evaluation:

```sql
ALTER TABLE WafConditionEntity
ADD WafGroupId INT NULL,  -- Foreign key to WafGroups
    Negate BIT NOT NULL DEFAULT 0, -- Support for negating conditions
    CONSTRAINT FK_WafConditions_WafGroups FOREIGN KEY (WafGroupId)
        REFERENCES WafGroups(Id) ON DELETE CASCADE
```

**New Columns:**
- `WafGroupId`: Nullable foreign key to `WafGroups` (NULL for legacy rules)
- `Negate`: Boolean flag to invert condition result (default: false)

**Legacy Columns (retained for backward compatibility):**
- `WafRuleEntityId`: Direct link to rule (used when `WafGroupId` IS NULL)
- `LogicOperator`: AND/OR operator (used when `WafGroupId` IS NULL)
- `ConditionOrder`: Evaluation order (used when `WafGroupId` IS NULL)

**Indexes Recommended:**
```sql
CREATE INDEX IX_WafConditionEntity_WafGroupId ON WafConditionEntity(WafGroupId);
CREATE INDEX IX_WafConditionEntity_WafRuleEntityId_Legacy 
    ON WafConditionEntity(WafRuleEntityId) WHERE WafGroupId IS NULL;
```

## Data Model Relationships

```
WafRuleEntity (1) ???> (N) WafGroups (1) ??> (N) WafConditionEntity
                   ?
                   ??> (N) WafConditionEntity [Legacy flat conditions]
```

- **New Schema**: `WafRuleEntity` ? `WafGroups` ? `WafConditionEntity`
- **Legacy Schema**: `WafRuleEntity` ? `WafConditionEntity` (direct)

## Backward Compatibility Strategy

### How the Repository Handles Both Schemas

The `WafRuleRepository` automatically detects which schema a rule uses:

```csharp
// Pseudo-code logic
if (rule.hasGroups()) {
    // Use new group-based evaluation
    LoadGroupsAndConditions();
} else {
    // Use legacy flat condition list
    LoadLegacyConditions();
}
```

**Detection Logic:**
1. Query `WafGroups` table for groups belonging to the rule
2. If groups exist ? Load groups and their conditions
3. If no groups exist ? Load flat conditions from `WafConditionEntity` where `WafGroupId IS NULL`

## Migration Scenarios

### Scenario 1: Fresh Installation

For new installations, use the group-based schema exclusively:

```sql
-- Example: Create a rule with two groups
DECLARE @RuleId INT = 1;

-- Group 1: (Country = "XX" AND UserAgent contains "bot")
INSERT INTO WafGroups (WafRuleId, GroupOrder, CreationDate) 
VALUES (@RuleId, 1, GETUTCDATE());
DECLARE @Group1Id INT = SCOPE_IDENTITY();

INSERT INTO WafConditionEntity (FieldId, OperatorId, Valor, WafGroupId, CreationDate)
VALUES 
    (21, 1, 'XX', @Group1Id, GETUTCDATE()),  -- Country equals XX
    (9, 3, 'bot', @Group1Id, GETUTCDATE());  -- UserAgent contains bot

-- Group 2: (IP in range "10.0.0.0/8")
INSERT INTO WafGroups (WafRuleId, GroupOrder, CreationDate) 
VALUES (@RuleId, 2, GETUTCDATE());
DECLARE @Group2Id INT = SCOPE_IDENTITY();

INSERT INTO WafConditionEntity (FieldId, OperatorId, Valor, WafGroupId, CreationDate)
VALUES (15, 15, '10.0.0.0/8', @Group2Id, GETUTCDATE());  -- IP in range

-- Result: (Country=XX AND UserAgent has bot) OR (IP in 10.0.0.0/8)
```

### Scenario 2: Existing Installation with Legacy Rules

Existing rules continue to work without modification:

**Before Migration:**
```sql
-- Legacy rule stored in WafConditionEntity
RuleId=1, Conditions:
  - FieldId=3, OperatorId=1, Valor="1.2.3.4", LogicOperator=1 (AND)
  - FieldId=9, OperatorId=3, Valor="bot", LogicOperator=1 (AND)
```

**After Schema Update:**
```sql
-- Same rule still works (WafGroupId remains NULL)
-- Repository detects no groups and uses legacy evaluation
```

### Scenario 3: Migrating Legacy Rules to Groups

To migrate existing rules to the new schema:

```sql
-- Migration script example
DECLARE @RuleId INT = 1;

-- Create a single group for all existing conditions
INSERT INTO WafGroups (WafRuleId, GroupOrder, CreationDate)
VALUES (@RuleId, 1, GETUTCDATE());
DECLARE @GroupId INT = SCOPE_IDENTITY();

-- Update all conditions to belong to the new group
UPDATE WafConditionEntity
SET WafGroupId = @GroupId
WHERE WafRuleEntityId = @RuleId AND WafGroupId IS NULL;

-- Result: All conditions now in one group with AND logic
```

### Scenario 4: Splitting Conditions into Multiple Groups

To convert OR logic to groups:

```sql
-- Original legacy rule with OR logic:
-- Condition 1: Country=XX (AND)
-- Condition 2: Country=YY (OR)  <- This should be in separate group
-- Condition 3: IP in range (AND)

DECLARE @RuleId INT = 1;

-- Group 1: Country=XX AND IP in range
INSERT INTO WafGroups (WafRuleId, GroupOrder, CreationDate) 
VALUES (@RuleId, 1, GETUTCDATE());
DECLARE @Group1Id INT = SCOPE_IDENTITY();

UPDATE WafConditionEntity
SET WafGroupId = @Group1Id
WHERE WafRuleEntityId = @RuleId 
  AND Id IN (1, 3);  -- Condition IDs 1 and 3

-- Group 2: Country=YY
INSERT INTO WafGroups (WafRuleId, GroupOrder, CreationDate) 
VALUES (@RuleId, 2, GETUTCDATE());
DECLARE @Group2Id INT = SCOPE_IDENTITY();

UPDATE WafConditionEntity
SET WafGroupId = @Group2Id
WHERE WafRuleEntityId = @RuleId 
  AND Id = 2;  -- Condition ID 2

-- Result: (Country=XX AND IP) OR (Country=YY)
```

## Repository Loading Logic

### Loading Process

```
FetchWafRules(host)
  ?
For each rule:
  ?
LoadRuleGroupsAndConditions(rule)
  ?
  ??> Check: Does rule have groups?
  ?     ??> YES: FetchWafGroups(ruleId)
  ?     ?         ?
  ?     ?       For each group:
  ?     ?         FetchGroupConditions(groupId)
  ?     ?           ?
  ?     ?         rule.Groups populated
  ?     ?         rule.Conditions = empty
  ?     ?
  ?     ??> NO: FetchWafConditionsLegacy(ruleId)
  ?               ?
  ?             Load WHERE WafGroupId IS NULL
  ?               ?
  ?             rule.Conditions populated
  ?             rule.Groups = empty
```

### SQL Queries Used

**Check for Groups:**
```sql
SELECT COUNT(*) 
FROM WafGroups 
WHERE WafRuleId = @RuleId
```

**Load Groups (New Schema):**
```sql
SELECT Id, GroupOrder 
FROM WafGroups 
WHERE WafRuleId = @RuleId 
ORDER BY GroupOrder
```

**Load Group Conditions (New Schema):**
```sql
SELECT Id, FieldId, OperatorId, Valor, FieldName, WafGroupId, WafRuleEntityId, 
       CreationDate, ISNULL(Negate, 0) as Negate
FROM WafConditionEntity 
WHERE WafGroupId = @GroupId 
ORDER BY Id
```

**Load Legacy Conditions:**
```sql
SELECT Id, FieldId, OperatorId, Valor, LogicOperator, WafRuleEntityId, 
       FieldName, ConditionOrder, CreationDate
FROM WafConditionEntity 
WHERE WafRuleEntityId = @RuleId 
  AND (WafGroupId IS NULL OR WafGroupId = 0)
ORDER BY ConditionOrder
```

## Evaluation Logic

### New Group-Based Evaluation

```
Rule Matches IF:
  (Group1: ALL conditions match) 
  OR 
  (Group2: ALL conditions match)
  OR
  (Group3: ALL conditions match)
  ...

Group Matches IF:
  Condition1 matches 
  AND 
  Condition2 matches 
  AND 
  Condition3 matches
  ...

Condition Matches:
  result = EvaluateCondition(condition)
  if (condition.Negate) 
    result = !result
  return result
```

### Legacy Flat Evaluation

```
Rule Matches:
  Evaluate conditions in ConditionOrder
  Apply LogicOperator (1=AND, 2=OR) between conditions
  Return final result
```

## Performance Considerations

### Indexes

Critical indexes for performance:

```sql
-- Primary lookups
CREATE INDEX IX_WafGroups_WafRuleId 
    ON WafGroups(WafRuleId) INCLUDE (GroupOrder, CreationDate);

CREATE INDEX IX_WafConditionEntity_WafGroupId 
    ON WafConditionEntity(WafGroupId) 
    INCLUDE (FieldId, OperatorId, Valor, FieldName, Negate);

-- Legacy rule support
CREATE INDEX IX_WafConditionEntity_Legacy 
    ON WafConditionEntity(WafRuleEntityId, ConditionOrder) 
    WHERE WafGroupId IS NULL
    INCLUDE (FieldId, OperatorId, Valor, FieldName, LogicOperator);

-- Rule loading
CREATE INDEX IX_WafRuleEntity_AppId_Habilitado 
    ON WafRuleEntity(AppId, Habilitado) 
    INCLUDE (Id, Nombre, ActionId, Prioridad);
```

### Caching

The repository caches loaded rules for 1 minute per host:
- Cache Key: `WAF_RULES_{hostname}`
- Expiration: 1 minute absolute
- Storage: ASP.NET Cache

**Cache Invalidation:**
When rules change, clear the cache:
```csharp
HttpRuntime.Cache.Remove($"WAF_RULES_{hostname}");
```

### Query Optimization

**Avoid N+1 Queries:**
The current implementation loads groups and conditions in separate queries. For high-traffic scenarios, consider a single query:

```sql
-- Optimized single query to load all groups and conditions
SELECT 
    g.Id as GroupId,
    g.GroupOrder,
    c.Id as ConditionId,
    c.FieldId,
    c.OperatorId,
    c.Valor,
    c.FieldName,
    c.Negate
FROM WafGroups g
LEFT JOIN WafConditionEntity c ON c.WafGroupId = g.Id
WHERE g.WafRuleId = @RuleId
ORDER BY g.GroupOrder, c.Id
```

## Validation and Constraints

### Data Integrity Rules

1. **Mutually Exclusive Schema Usage:**
   - If `WafGroupId` IS NOT NULL ? Must have valid group
   - If `WafGroupId` IS NULL ? Must have `WafRuleEntityId`

2. **Group Order Uniqueness:**
   - Within a rule, `GroupOrder` should be unique
   - Recommended constraint:
   ```sql
   ALTER TABLE WafGroups
   ADD CONSTRAINT UQ_WafGroups_RuleId_GroupOrder 
       UNIQUE (WafRuleId, GroupOrder);
   ```

3. **Orphaned Conditions:**
   - Cascade delete ensures conditions are removed when groups are deleted
   - Legacy conditions deleted when rule is deleted

### Validation Queries

**Find orphaned conditions:**
```sql
-- Conditions with WafGroupId that don't reference valid group
SELECT c.* 
FROM WafConditionEntity c
LEFT JOIN WafGroups g ON c.WafGroupId = g.Id
WHERE c.WafGroupId IS NOT NULL AND g.Id IS NULL;

-- Conditions without both WafGroupId and WafRuleEntityId
SELECT * 
FROM WafConditionEntity
WHERE WafGroupId IS NULL AND WafRuleEntityId IS NULL;
```

**Find rules without conditions:**
```sql
SELECT r.* 
FROM WafRuleEntity r
WHERE NOT EXISTS (
    SELECT 1 FROM WafConditionEntity c WHERE c.WafRuleEntityId = r.Id
)
AND NOT EXISTS (
    SELECT 1 FROM WafGroups g WHERE g.WafRuleId = r.Id
);
```

## Testing the Migration

### Test Cases

1. **Legacy Rule Still Works:**
```sql
-- Verify legacy rule loads correctly
EXEC sp_executesql N'
SELECT * FROM WafConditionEntity 
WHERE WafRuleEntityId = @RuleId AND WafGroupId IS NULL',
N'@RuleId INT', @RuleId = 1;
```

2. **New Group Rule Works:**
```sql
-- Verify group-based rule loads correctly
SELECT g.*, c.*
FROM WafGroups g
LEFT JOIN WafConditionEntity c ON c.WafGroupId = g.Id
WHERE g.WafRuleId = @RuleId
ORDER BY g.GroupOrder, c.Id;
```

3. **Mixed Environment:**
```sql
-- Verify both types coexist
SELECT 
    r.Id as RuleId,
    r.Nombre,
    CASE WHEN EXISTS(SELECT 1 FROM WafGroups WHERE WafRuleId = r.Id) 
         THEN 'Group-based' 
         ELSE 'Legacy' 
    END as SchemaType
FROM WafRuleEntity r;
```

## Rollback Plan

If issues arise, rollback steps:

```sql
-- 1. Remove foreign key constraint
ALTER TABLE WafConditionEntity
DROP CONSTRAINT FK_WafConditions_WafGroups;

-- 2. Remove new columns
ALTER TABLE WafConditionEntity
DROP COLUMN WafGroupId, Negate;

-- 3. Drop new table
DROP TABLE WafGroups;

-- 4. Restart IIS to clear cache
-- (Execute from command line)
-- iisreset
```

## Monitoring and Metrics

### Key Metrics to Track

1. **Rule Loading Performance:**
   - Time to load rules per host
   - Cache hit rate
   - Number of groups per rule
   - Number of conditions per group

2. **Evaluation Performance:**
   - Time to evaluate a single rule
   - Time to evaluate all rules for a request
   - Percentage using legacy vs. group-based evaluation

3. **Database Queries:**
   - Query execution time
   - Number of queries per rule load
   - Index usage statistics

### SQL Server Query Store

Enable Query Store to monitor performance:
```sql
ALTER DATABASE IISFrontGuard
SET QUERY_STORE = ON (
    OPERATION_MODE = READ_WRITE,
    DATA_FLUSH_INTERVAL_SECONDS = 900,
    INTERVAL_LENGTH_MINUTES = 60
);
```

## Summary

This migration provides:

? **Full backward compatibility** - Existing rules work without changes  
? **Flexible group-based logic** - Support complex (A AND B) OR (C AND D) patterns  
? **Automatic detection** - Repository chooses correct loading path  
? **Graceful migration** - Can migrate rules incrementally  
? **Performance optimized** - Proper indexes and caching  
? **Data integrity** - Foreign keys and cascading deletes  

The migration path is safe, reversible, and production-ready.
