# Database Cleanup Strategy for Integration Tests

## Problem
The integration tests were experiencing foreign key constraint violations when cleaning up test data because tables were being deleted in the wrong order.

## Root Cause
The `DELETE` statements were attempting to delete from parent tables (`WafRuleEntity`, `AppEntity`) before deleting from child tables (`RequestContext`, `WafConditionEntity`) that reference them via foreign keys.

## Solution

### 1. Fixed Deletion Order
The correct order to delete data while respecting foreign key constraints is:

1. **ResponseContext** - No foreign key dependencies
2. **RequestContext** - Has FK to `WafRuleEntity`, `AppEntity`, and `Action`
3. **WafConditionEntity** - Has FK to `WafRuleEntity`, `Field`, and `Operator`
4. **WafRuleEntity** - Has FK to `AppEntity` and `Action`
5. **AppEntity** - Referenced by `WafRuleEntity` and `RequestContext`

**Note:** `Action`, `Field`, and `Operator` tables are NOT deleted as they contain seed data that should persist across test runs.

### 2. Files Updated

#### IisIntegrationFixture.cs
- **CleanupTestData()**: Fixed deletion order in the cleanup method
- **SeedRules()**: Fixed deletion order when seeding initial test data
- **EnsureDatabaseAndSchema()**: Fixed typo (`LocalDbAppAppCs` ? `LocalDbAppCs`)

#### RequestLoggerAdapterIntegrationTests.cs
- Added **CleanupTestDataAsync()** helper method to clean up test-specific data
- Added `System.Linq` using directive for LINQ extension methods
- Updated tests that insert `AppEntity` records to clean up after themselves:
  - `GetTokenExpirationDuration_WithExistingHost_ShouldReturnConfiguredDuration`
  - `GetTokenExpirationDuration_WithNullDuration_ShouldReturnDefault`
  - `Encryption_IntegrationWithDatabase`

#### Cleanup-TestData.sql
- Created new SQL script with correct deletion order
- Added helpful feedback messages
- Includes documentation about which tables are preserved

### 3. Best Practices Implemented

#### Test Isolation
Each test that inserts data now:
1. Creates its own test data with unique identifiers
2. Performs its assertions
3. Cleans up its own data in a `finally` block

Example pattern:
```csharp
[Fact]
public async Task SomeTest()
{
    var appId = Guid.NewGuid();
    
    try
    {
        // Arrange - Insert test data
        // Act - Perform test
        // Assert - Verify results
    }
    finally
    {
        // Cleanup - Remove test data
        await CleanupTestDataAsync(appId);
    }
}
```

#### Cleanup Helper Method
The `CleanupTestDataAsync()` method:
- Accepts multiple AppEntity IDs for bulk cleanup
- Cascades deletions in the correct order
- Handles database unavailability gracefully
- Logs warnings if cleanup fails

```csharp
private async Task CleanupTestDataAsync(params Guid[] appIds)
{
    // Deletes ResponseContext, RequestContext, WafConditionEntity,
    // WafRuleEntity, and AppEntity in correct order
}
```

### 4. Foreign Key Relationships

```
Action (seed data)
  ?
  ?? WafRuleEntity (FK: ActionId)
  ?? RequestContext (FK: ActionId)

Field (seed data)
  ?
  ?? WafConditionEntity (FK: FieldId)

Operator (seed data)
  ?
  ?? WafConditionEntity (FK: OperatorId)

AppEntity
  ?
  ?? WafRuleEntity (FK: AppId)
  ?? RequestContext (FK: AppId)

WafRuleEntity
  ?
  ?? WafConditionEntity (FK: WafRuleEntityId)
  ?? RequestContext (FK: RuleId)

RequestContext
  ?
  ?? ResponseContext (FK: RayId ? RayId in RequestContext)
```

### 5. Usage

#### Running All Tests
The fixture automatically seeds and cleans up data for each test class.

#### Manual Database Cleanup
Use the provided SQL script after test runs:
```powershell
sqlcmd -S (localdb)\MSSQLLocalDB -d IISFrontGuard -i "IISFrontGuard.Module.IntegrationTests\SQL_Scripts\Cleanup-TestData.sql"
```

Or from SQL Server Management Studio, run:
```sql
-- Clean test data in correct order to respect foreign key constraints
DELETE FROM dbo.ResponseContext;
DELETE FROM dbo.RequestContext;
DELETE FROM dbo.WafConditionEntity;
DELETE FROM dbo.WafRuleEntity;
DELETE FROM dbo.AppEntity;
```

### 6. Benefits

1. **No More FK Violations**: Tests clean up properly without constraint errors
2. **Test Isolation**: Each test manages its own data lifecycle
3. **Repeatable Tests**: Tests can be run multiple times without data pollution
4. **Graceful Degradation**: Tests skip cleanly if database is unavailable
5. **Clear Documentation**: Developers understand the data dependencies

### 7. Future Enhancements

Consider implementing:
- Transaction-based tests (rollback after each test)
- Database snapshot restore for faster cleanup
- Parallel test execution with isolated data partitions
- Automated FK relationship detection for dynamic cleanup order
