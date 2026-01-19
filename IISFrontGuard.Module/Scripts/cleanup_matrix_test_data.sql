-- ============================================================================
-- Cleanup Script for Matrix Test Data
-- ============================================================================
-- This script removes all TEST: rules created by matrix_test_data.sql
-- ============================================================================

USE IISFrontGuard;
GO

PRINT 'Starting cleanup of matrix test data...';
PRINT '';

-- Show what will be deleted
PRINT 'Rules to be deleted:';
SELECT COUNT(*) AS RuleCount
FROM dbo.WafRuleEntity
WHERE Nombre LIKE 'TEST:%';

PRINT '';
PRINT 'Groups to be deleted:';
SELECT COUNT(*) AS GroupCount
FROM dbo.WafGroups g
JOIN dbo.WafRuleEntity r ON r.Id = g.WafRuleId
WHERE r.Nombre LIKE 'TEST:%';

PRINT '';
PRINT 'Conditions to be deleted:';
SELECT COUNT(*) AS ConditionCount
FROM dbo.WafConditionEntity c
JOIN dbo.WafRuleEntity r ON r.Id = c.WafRuleEntityId
WHERE r.Nombre LIKE 'TEST:%';

PRINT '';
PRINT 'Deleting data...';

-- Delete conditions first (child of groups)
DELETE c 
FROM dbo.WafConditionEntity c
JOIN dbo.WafGroups g ON g.Id = c.WafGroupId
JOIN dbo.WafRuleEntity r ON r.Id = g.WafRuleId
WHERE r.Nombre LIKE 'TEST:%';

PRINT 'Conditions deleted: ' + CAST(@@ROWCOUNT AS VARCHAR);

-- Delete groups (child of rules)
DELETE g 
FROM dbo.WafGroups g
JOIN dbo.WafRuleEntity r ON r.Id = g.WafRuleId
WHERE r.Nombre LIKE 'TEST:%';

PRINT 'Groups deleted: ' + CAST(@@ROWCOUNT AS VARCHAR);

-- Delete rules
DELETE FROM dbo.WafRuleEntity 
WHERE Nombre LIKE 'TEST:%';

PRINT 'Rules deleted: ' + CAST(@@ROWCOUNT AS VARCHAR);

PRINT '';
PRINT 'Cleanup completed successfully!';
GO
