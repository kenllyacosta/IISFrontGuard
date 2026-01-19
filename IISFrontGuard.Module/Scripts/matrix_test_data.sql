-- ============================================================================
-- Matrix Test Data Generator for WAF Rules
-- ============================================================================
-- This script generates test data for comprehensive WAF rule testing.
-- It creates one rule per Field x Operator combination with appropriate
-- test values for each combination.
--
-- Usage:
--   1. Run this script to generate ~550 test rules
--   2. Run the MatrixRuleEvaluationTests integration test
--   3. Run cleanup_matrix_test_data.sql to remove test data
-- ============================================================================

USE IISFrontGuard;
GO

-- Pick the localhost app (or create if not exists)
DECLARE @AppId UNIQUEIDENTIFIER;

SELECT TOP 1 @AppId = Id 
FROM dbo.AppEntity 
WHERE Host = N'localhost' 
ORDER BY CreationDate DESC;

IF @AppId IS NULL
BEGIN
  -- Generate a new GUID for the app
  SET @AppId = NEWID();
  
  INSERT INTO dbo.AppEntity (Id, AppName, AppDescription, Host, TokenExpirationDurationHr)
  VALUES (@AppId, N'Localhost App', N'Test application for localhost', N'localhost', 12);
  
  PRINT 'Created new AppEntity for localhost with Id: ' + CAST(@AppId AS VARCHAR(36));
END
ELSE
BEGIN
  PRINT 'Using existing AppEntity for localhost with Id: ' + CAST(@AppId AS VARCHAR(36));
END

DECLARE @Now DATETIME = GETDATE();

-- ============================================================================
-- CLEANUP: Remove existing TEST: rules (optional - uncomment to reset)
-- ============================================================================
/*
DELETE c FROM dbo.WafConditionEntity c
JOIN dbo.WafGroups g ON g.Id = c.WafGroupId
JOIN dbo.WafRuleEntity r ON r.Id = g.WafRuleId
WHERE r.Nombre LIKE 'TEST:%';

DELETE g FROM dbo.WafGroups g
JOIN dbo.WafRuleEntity r ON r.Id = g.WafRuleId
WHERE r.Nombre LIKE 'TEST:%';

DELETE FROM dbo.WafRuleEntity WHERE Nombre LIKE 'TEST:%';

PRINT 'Cleaned up existing TEST: rules';
*/

-- ============================================================================
-- Build matrix (Fields x Operators) with per-case sample values
-- ============================================================================
IF OBJECT_ID('tempdb..#Matrix') IS NOT NULL DROP TABLE #Matrix;

CREATE TABLE #Matrix
(
  FieldId     TINYINT NOT NULL,
  OperatorId  TINYINT NOT NULL,
  FieldName   VARCHAR(100) NULL,
  Valor       NVARCHAR(1000) NULL
);

;WITH F AS
(
  SELECT Id AS FieldId, NormalizedName
  FROM dbo.Field
  -- Optional: Filter to specific fields for smaller test set
  -- WHERE Id IN (1,2,3,7,9,13,14,15,16,18,19,21)
),
O AS
(
  SELECT Id AS OperatorId, NormalizedName
  FROM dbo.Operator
  -- Optional: Filter to specific operators for smaller test set
  -- WHERE Id IN (1,3,5,7,11,15,17,21)
)
INSERT INTO #Matrix(FieldId, OperatorId, FieldName, Valor)
SELECT
  f.FieldId,
  o.OperatorId,

  -- FieldName only needed for cookie/header
  CASE
    WHEN f.FieldId = 1 THEN 'sessionid'       -- cookie name
    WHEN f.FieldId = 16 THEN 'x-test'         -- header name
    ELSE NULL
  END AS FieldName,

  -- Valor depends on operator type + field type
  CASE
    -- Presence operators ignore Valor
    WHEN o.OperatorId IN (21,22) THEN N''

    -- IP range operators require CIDR/list
    WHEN o.OperatorId IN (15,16) THEN N'203.0.113.0/24,198.51.100.10'

    -- Numeric operators require numeric
    WHEN o.OperatorId IN (17,18,19,20) THEN
      CASE
        WHEN f.FieldId = 19 THEN N'100'      -- body length threshold
        ELSE N'10'                           -- fallback numeric
      END

    -- Regex operators
    WHEN o.OperatorId IN (5,6) THEN
      CASE
        WHEN f.FieldId IN (12,13,14,15) THEN N'.*test.*'   -- URL-ish
        WHEN f.FieldId = 18 THEN N'.*hello.*'              -- body
        ELSE N'.*bot.*'                                    -- headers/UA/etc
      END

    -- IN / LIST operators: CSV values
    WHEN o.OperatorId IN (11,12,13,14) THEN
      CASE
        WHEN f.FieldId IN (7)  THEN N'GET,POST,PUT'
        WHEN f.FieldId IN (5)  THEN N'http,https'
        WHEN f.FieldId IN (8)  THEN N'1.0,1.1,2'
        WHEN f.FieldId IN (20,21) THEN N'ES,US,FR'
        WHEN f.FieldId IN (22) THEN N'EU,NA,SA,AS,AF,OC'
        WHEN f.FieldId IN (3,23,24,25,4) THEN N'203.0.113.10,198.51.100.10'
        ELSE N'test,hello,bot'
      END

    -- Default string value for string operators (equals/contains/starts/ends, etc.)
    ELSE
      CASE
        WHEN f.FieldId = 1  THEN N'abc123'                         -- cookie value
        WHEN f.FieldId = 2  THEN N'localhost'                      -- hostname
        WHEN f.FieldId IN (3,23,24,25) THEN N'203.0.113.10'        -- ip
        WHEN f.FieldId = 4  THEN N'203.0.113.0/24'                 -- ip-range "field"
        WHEN f.FieldId = 5  THEN N'https'                          -- protocol
        WHEN f.FieldId = 6  THEN N'https://example.com/from-test'  -- referrer
        WHEN f.FieldId = 7  THEN N'POST'                           -- method
        WHEN f.FieldId = 8  THEN N'1.1'                            -- http version
        WHEN f.FieldId = 9  THEN N'scanner-bot'                    -- user-agent
        WHEN f.FieldId = 10 THEN N'203.0.113.10'                   -- x-forwarded-for
        WHEN f.FieldId = 11 THEN N'application/json'               -- mime type
        WHEN f.FieldId = 12 THEN N'https://localhost/api/test?x=test' -- Absolute Uri
        WHEN f.FieldId = 13 THEN N'/api/test'                      -- Absolute Path
        WHEN f.FieldId = 14 THEN N'/api/test?x=test'               -- Path and query
        WHEN f.FieldId = 15 THEN N'x=test'                         -- querystring
        WHEN f.FieldId = 16 THEN N'hello-test'                     -- header x-test
        WHEN f.FieldId = 17 THEN N'application/json'               -- content-type
        WHEN f.FieldId = 18 THEN N'hello test'                     -- body
        WHEN f.FieldId = 19 THEN N'100'                            -- body length
        WHEN f.FieldId IN (20,21) THEN N'ES'                       -- country / iso2
        WHEN f.FieldId = 22 THEN N'EU'                             -- continent
        ELSE N'test'
      END
  END AS Valor
FROM F f
CROSS JOIN O o;

PRINT 'Created matrix with ' + CAST((SELECT COUNT(*) FROM #Matrix) AS VARCHAR) + ' combinations';

-- ============================================================================
-- Create rules + groups + conditions (1 condition per rule)
-- ============================================================================
DECLARE @RuleId INT, @GroupId INT;
DECLARE @FieldId TINYINT, @OperatorId TINYINT;
DECLARE @FieldName VARCHAR(100), @Valor NVARCHAR(1000);
DECLARE @RuleCount INT = 0;

DECLARE cur CURSOR LOCAL FAST_FORWARD FOR
  SELECT FieldId, OperatorId, FieldName, Valor
  FROM #Matrix
  ORDER BY FieldId, OperatorId;

OPEN cur;
FETCH NEXT FROM cur INTO @FieldId, @OperatorId, @FieldName, @Valor;

WHILE @@FETCH_STATUS = 0
BEGIN
  -- Create rule
  INSERT dbo.WafRuleEntity (Nombre, ActionId, AppId, Prioridad, Habilitado, CreationDate)
  VALUES (CONCAT('TEST: Field=', @FieldId, ' Op=', @OperatorId), 5, @AppId, 1000, 1, @Now);

  SET @RuleId = SCOPE_IDENTITY();

  -- Create group
  INSERT dbo.WafGroups (WafRuleId, GroupOrder, CreationDate)
  VALUES (@RuleId, 1, SYSUTCDATETIME());

  SET @GroupId = SCOPE_IDENTITY();

  -- Create condition
  INSERT dbo.WafConditionEntity
  (
    FieldId, OperatorId, Valor,
    LogicOperator, WafRuleEntityId,
    FieldName, ConditionOrder, CreationDate,
    WafGroupId, Negate
  )
  VALUES
  (
    @FieldId, @OperatorId, @Valor,
    1, @RuleId,
    @FieldName, 1, @Now,
    @GroupId, 0
  );

  SET @RuleCount = @RuleCount + 1;

  FETCH NEXT FROM cur INTO @FieldId, @OperatorId, @FieldName, @Valor;
END

CLOSE cur;
DEALLOCATE cur;

-- ============================================================================
-- Summary
-- ============================================================================
PRINT '';
PRINT '=================================================================';
PRINT 'Matrix test data created successfully!';
PRINT '=================================================================';
PRINT 'Total TEST: rules created: ' + CAST(@RuleCount AS VARCHAR);
PRINT '';

SELECT 
    'Rules' AS EntityType,
    COUNT(*) AS Count
FROM dbo.WafRuleEntity
WHERE Nombre LIKE 'TEST:%'
UNION ALL
SELECT 
    'Groups',
    COUNT(*)
FROM dbo.WafGroups g
JOIN dbo.WafRuleEntity r ON r.Id = g.WafRuleId
WHERE r.Nombre LIKE 'TEST:%'
UNION ALL
SELECT 
    'Conditions',
    COUNT(*)
FROM dbo.WafConditionEntity c
JOIN dbo.WafRuleEntity r ON r.Id = c.WafRuleEntityId
WHERE r.Nombre LIKE 'TEST:%';

PRINT '';
PRINT 'Sample rules:';
SELECT TOP 5
    r.Id,
    r.Nombre,
    c.FieldId,
    c.OperatorId,
    c.Valor,
    c.FieldName
FROM dbo.WafRuleEntity r
JOIN dbo.WafGroups g ON g.WafRuleId = r.Id
JOIN dbo.WafConditionEntity c ON c.WafGroupId = g.Id
WHERE r.Nombre LIKE 'TEST:%'
ORDER BY r.Id;

PRINT '';
PRINT 'Next steps:';
PRINT '1. Run the MatrixRuleEvaluationTests integration test';
PRINT '2. Use cleanup_matrix_test_data.sql to remove test data when done';
PRINT '=================================================================';
GO
