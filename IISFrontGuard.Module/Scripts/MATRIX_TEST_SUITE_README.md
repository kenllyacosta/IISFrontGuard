# Matrix Test Suite for WAF Rules

This test suite provides comprehensive testing for all Field × Operator combinations in the WAF rule engine. It includes approximately **550 test rules** covering all possible field and operator combinations.

## Overview

The matrix test suite consists of:

1. **SQL Scripts** - Generate and cleanup test data
2. **Unit Tests** - Verify rule compilation and basic evaluation logic
3. **Integration Tests** - Verify end-to-end rule evaluation with real request contexts

## Files

### SQL Scripts

Located in `IISFrontGuard.Module\Scripts\`:

- **`matrix_test_data.sql`** - Generates ~550 test rules with one rule per Field×Operator combination
- **`cleanup_matrix_test_data.sql`** - Removes all test data created by the matrix script

### Unit Tests

Located in `IISFrontGuard.Module.UnitTests\Services\`:

- **`MatrixRuleCompilationTests.cs`** - Tests that all Field×Operator combinations compile successfully

### Integration Tests

Located in `IISFrontGuard.Module.IntegrationTests\WAF\`:

- **`MatrixRuleEvaluationTests.cs`** - Tests end-to-end evaluation of all matrix rules against crafted requests

## Usage

### Automated Approach (Recommended)

The test classes now include automatic setup and cleanup! Simply run the tests:

```bash
# Unit tests - automatic setup/cleanup
dotnet test --filter FullyQualifiedName~MatrixRuleCompilationTests

# Integration tests - automatic setup/cleanup
dotnet test --filter FullyQualifiedName~MatrixRuleLoadingTests
```

**What happens automatically:**

1. **OneTimeSetUp** (NUnit) / **Constructor** (XUnit): Executes `matrix_test_data.sql`
   - Creates ~550 test rules in the database
   - Creates AppEntity for localhost if needed
   
2. **Tests Execute**: All test cases run against the generated data

3. **OneTimeTearDown** (NUnit) / **Dispose** (XUnit): Executes `cleanup_matrix_test_data.sql`
   - Removes all TEST: rules
   - Removes associated groups and conditions
   - Leaves database in clean state

### Manual Approach (Alternative)

If you prefer manual control over test data:

### Step 1: Generate Test Data

Run the SQL script to populate the database with test rules:

```sql
-- Execute in SQL Server Management Studio or your preferred SQL client
USE IISFrontGuard;
GO

-- Run the matrix test data generator
-- File: IISFrontGuard.Module\Scripts\matrix_test_data.sql
```

This will create:
- ~550 WAF rules (one per Field×Operator combination)
- ~550 WAF groups (one per rule)
- ~550 WAF conditions (one per group)

All rules are named with the pattern: `TEST: Field=X Op=Y`

### Step 2: Run Unit Tests

The unit tests verify that all combinations compile correctly:

```bash
# Run all matrix compilation tests
dotnet test --filter FullyQualifiedName~MatrixRuleCompilationTests

# Or run specific test categories
dotnet test --filter "FullyQualifiedName~MatrixRuleCompilationTests.CompileRule_StringFieldWithOperator"
```

**Expected Results:**
- All ~550 combinations should compile without errors
- Negation logic should work correctly
- Multi-group OR logic should work correctly

### Step 3: Run Integration Tests

The integration tests verify end-to-end evaluation:

```bash
# Run all matrix evaluation tests
dotnet test --filter FullyQualifiedName~MatrixRuleEvaluationTests

# Or run the main comprehensive test
dotnet test --filter "FullyQualifiedName~MatrixRuleEvaluationTests.TestMatrixRules_AllFieldOperatorCombinations"
```

**Expected Results:**
- Success rate should be ?90%
- Each field should have multiple operators tested
- Specific critical combinations should all pass

### Step 4: Cleanup Test Data

After testing, remove the test data:

```sql
-- Execute in SQL Server
USE IISFrontGuard;
GO

-- Run the cleanup script
-- File: IISFrontGuard.Module\Scripts\cleanup_matrix_test_data.sql
```

## Test Matrix

### Fields Tested (25 total)

| ID | Field Name | Description |
|----|------------|-------------|
| 1 | cookie | HTTP cookie value |
| 2 | hostname | Request host name |
| 3 | ip | Client IP address |
| 4 | ip range | Client IP range |
| 5 | protocol | Request protocol (HTTP/HTTPS) |
| 6 | referrer | HTTP referrer header |
| 7 | method | HTTP method |
| 8 | http version | HTTP protocol version |
| 9 | user-agent | User-Agent header |
| 10 | x-forwarded-for | X-Forwarded-For header |
| 11 | mime type | Request MIME type |
| 12 | Absolute Uri | Full request URL |
| 13 | Absolute Path | Request URL without query string |
| 14 | Path And Query | Request URL path |
| 15 | url querystring | Request URL query string |
| 16 | header | Specific HTTP header |
| 17 | content type | Request Content-Type header |
| 18 | body | Raw request body |
| 19 | body length | Request body size in bytes |
| 20 | country | Request country |
| 21 | country-iso2 | Request country (ISO 3166-1 alpha-2) |
| 22 | continent | Request continent |
| 23 | ip-cf-connecting-ip | CF-Connecting-IP header |
| 24 | ip-x-forwarded-for | X-Forwarded-For (IP extraction) |
| 25 | ip-cf-connecting-ip | True-Client-IP header |

### Operators Tested (22 total)

| ID | Operator Name | Description |
|----|---------------|-------------|
| 1 | equals | Value is equal to target |
| 2 | does not equal | Value is not equal to target |
| 3 | contains | Value contains target |
| 4 | does not contain | Value does not contain target |
| 5 | matches regex | Value matches regex pattern |
| 6 | does not match regex | Value does not match regex |
| 7 | starts with | Value starts with target |
| 8 | does not start with | Value does not start with target |
| 9 | ends with | Value ends with target |
| 10 | does not end with | Value does not end with target |
| 11 | is in | Value is in provided set |
| 12 | is not in | Value is not in provided set |
| 13 | is in list | Value is in predefined list |
| 14 | is not in list | Value is not in predefined list |
| 15 | is ip in range | IP is within range (CIDR) |
| 16 | is ip not in range | IP is outside range |
| 17 | greater than | Numeric: value > target |
| 18 | less than | Numeric: value < target |
| 19 | greater than or equal | Numeric: value ? target |
| 20 | less than or equal | Numeric: value ? target |
| 21 | is present | Value exists |
| 22 | is not present | Value does not exist |

### Total Combinations

**25 Fields × 22 Operators = 550 possible combinations**

> Note: Not all combinations are logically valid (e.g., numeric operators on string fields). The test suite handles these cases gracefully and tests all meaningful combinations.

## Test Data Values

The matrix script generates appropriate test values for each combination:

### String Values
- Cookie: `abc123`
- Hostname: `localhost`
- Method: `POST`
- User-Agent: `scanner-bot`
- Path: `/api/test`
- Body: `hello test`

### Numeric Values
- Body Length: `100`

### IP Values
- Single IP: `203.0.113.10`
- IP Range: `203.0.113.0/24`
- IP List: `203.0.113.10,198.51.100.10`

### List Values
- Methods: `GET,POST,PUT`
- Protocols: `http,https`
- Countries: `ES,US,FR`
- Continents: `EU,NA,SA,AS,AF,OC`

## Customization

### Testing a Subset of Combinations

To test only specific fields or operators, modify the SQL script:

```sql
-- In matrix_test_data.sql, uncomment and modify these filters:

;WITH F AS
(
  SELECT Id AS FieldId, NormalizedName
  FROM dbo.Field
  WHERE Id IN (1,2,3,7,9,13,14,15,16,18,19,21)  -- Only these fields
),
O AS
(
  SELECT Id AS OperatorId, NormalizedName
  FROM dbo.Operator
  WHERE Id IN (1,3,5,7,11,15,17,21)  -- Only these operators
)
```

This reduces the number of test rules generated.

### Adding Custom Test Cases

You can add specific test cases to the integration tests:

```csharp
[IntegrationTestFact]
public void TestMatrixRules_CustomCombination_ShouldWork()
{
    // Add your custom test logic here
    var matrixRules = LoadMatrixRulesFromDatabase();
    var customRule = matrixRules.First(r => r.FieldId == 7 && r.OperatorId == 1);
    
    // ... test logic
}
```

## Troubleshooting

### Test Failures

If tests fail, check:

1. **Database Connection**: Ensure connection string is correct in `App.config`
2. **Test Data**: Run `matrix_test_data.sql` to ensure test rules exist
3. **Cache**: Clear the WAF rule cache if rules aren't loading
4. **GeoIP Database**: Some tests require `GeoLite2-Country.mmdb` for country/continent fields

### Common Issues

**Issue**: "No AppEntity with Host=localhost found"
- **Solution**: The matrix script creates this automatically, but ensure your database has the localhost app

**Issue**: Tests timeout
- **Solution**: Reduce the number of combinations by filtering fields/operators in the SQL script

**Issue**: Compilation errors for certain combinations
- **Solution**: Some Field×Operator combinations may not be semantically valid. Check the test output for specific failures.

## Performance

### Expected Runtime

- **Unit Tests**: < 5 seconds (tests compilation only)
- **Integration Tests**: 1-3 minutes (tests ~550 rules end-to-end)
- **SQL Script Execution**: < 10 seconds

### Optimization

For faster test execution:
1. Filter to a smaller subset of combinations (see Customization section)
2. Run unit tests first to catch compilation issues
3. Use parallel test execution if supported by your test runner

## Contributing

When adding new fields or operators:

1. Update the `Field` or `Operator` table in the database
2. Update the matrix script to include appropriate test values
3. Update this README with the new field/operator
4. Run the full test suite to verify compatibility

## See Also

- [WAF Rule Documentation](../RULE_INDEXING_DOCUMENTATION.md)
- [Rule Compiler Documentation](../OPTIMIZED_ENGINE_DOCUMENTATION.md)
- [Integration Test Setup](../IISFrontGuard.Module.IntegrationTests/README.md)
