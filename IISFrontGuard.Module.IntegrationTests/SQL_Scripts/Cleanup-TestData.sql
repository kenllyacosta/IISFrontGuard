-- Clean up test data in the correct order to respect foreign key constraints
-- Order: ResponseContext -> RequestContext -> WafConditionEntity -> WafRuleEntity -> AppEntity

USE [IISFrontGuard]
GO

-- 1. Delete ResponseContext (no FK dependencies)
DELETE FROM dbo.ResponseContext;

-- 2. Delete RequestContext (has FK to WafRuleEntity, AppEntity, Action)
DELETE FROM dbo.RequestContext;

-- 3. Delete WafConditionEntity (has FK to WafRuleEntity, Field, Operator)
DELETE FROM dbo.WafConditionEntity;

-- 4. Delete WafRuleEntity (has FK to AppEntity, Action)
DELETE FROM dbo.WafRuleEntity;

-- 5. Delete AppEntity (referenced by WafRuleEntity and RequestContext)
DELETE FROM dbo.AppEntity;

PRINT 'Test data cleanup completed successfully';
PRINT '  - ResponseContext: Cleared';
PRINT '  - RequestContext: Cleared';
PRINT '  - WafConditionEntity: Cleared';
PRINT '  - WafRuleEntity: Cleared';
PRINT '  - AppEntity: Cleared';
PRINT '';
PRINT 'Note: Lookup tables (Action, Field, Operator) are NOT deleted';
PRINT '      as they contain seed data that should persist across test runs.';

-- Note: Action, Field, and Operator tables are not deleted as they contain seed data
-- that should persist across test runs
