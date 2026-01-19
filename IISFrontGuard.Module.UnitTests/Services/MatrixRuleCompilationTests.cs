using IISFrontGuard.Module.Models;
using IISFrontGuard.Module.Services;
using IISFrontGuard.Module.UnitTests.Helpers;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web;

namespace IISFrontGuard.Module.UnitTests.Services
{
    /// <summary>
    /// Unit tests for WAF rule compilation and evaluation using the matrix test data.
    /// These tests verify that each Field x Operator combination compiles and evaluates correctly.
    /// Automatically sets up test data before all tests and cleans up after.
    /// </summary>
    [TestFixture]
    [Category("Matrix")]
    [Category("Compilation")]
    public class MatrixRuleCompilationTests
    {
        private RuleCompiler _compiler;
        private static string _connectionString;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // Get connection string
            _connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["IISFrontGuardDB"]?.ConnectionString
                ?? "Server=(local);Database=IISFrontGuard;Integrated Security=true;";

            // Run matrix test data setup script
            TestContext.WriteLine("Setting up matrix test data...");
            ExecuteSqlScript("matrix_test_data.sql");
            TestContext.WriteLine("Matrix test data setup completed.");
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            // Run cleanup script
            TestContext.WriteLine("Cleaning up matrix test data...");
            ExecuteSqlScript("cleanup_matrix_test_data.sql");
            TestContext.WriteLine("Matrix test data cleanup completed.");
        }

        [SetUp]
        public void SetUp()
        {
            _compiler = new RuleCompiler();
        }

        private static void ExecuteSqlScript(string scriptFileName)
        {
            try
            {
                // Try multiple possible locations for the script file
                var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                TestContext.WriteLine($"Base directory: {baseDirectory}");

                var possiblePaths = new[]
                {
                    // Common build output locations
                    Path.Combine(baseDirectory, "..", "..", "..", "IISFrontGuard.Module", "Scripts", scriptFileName),
                    Path.Combine(baseDirectory, "..", "..", "..", "..", "IISFrontGuard.Module", "Scripts", scriptFileName),
                    Path.Combine(baseDirectory, "Scripts", scriptFileName),
                    
                    // Direct workspace paths
                    Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "IISFrontGuard.Module", "Scripts", scriptFileName),
                    Path.Combine(Directory.GetCurrentDirectory(), "Scripts", scriptFileName),
                };

                string scriptPath = null;
                foreach (var path in possiblePaths)
                {
                    var fullPath = Path.GetFullPath(path);
                    TestContext.WriteLine($"Checking path: {fullPath}");
                    
                    if (File.Exists(fullPath))
                    {
                        scriptPath = fullPath;
                        TestContext.WriteLine($"? Found script at: {scriptPath}");
                        break;
                    }
                }

                if (scriptPath == null || !File.Exists(scriptPath))
                {
                    var errorMsg = $"ERROR: Script '{scriptFileName}' not found. Searched locations:\n" + 
                                   string.Join("\n", possiblePaths.Select(p => "  - " + Path.GetFullPath(p)));
                    TestContext.WriteLine(errorMsg);
                    throw new FileNotFoundException(errorMsg, scriptFileName);
                }

                // Read the script
                var script = File.ReadAllText(scriptPath);
                TestContext.WriteLine($"Script size: {script.Length} characters");

                // Split by GO statements (case insensitive, handles various line endings)
                var batches = System.Text.RegularExpressions.Regex.Split(
                    script, 
                    @"^\s*GO\s*$", 
                    System.Text.RegularExpressions.RegexOptions.Multiline | 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase
                );

                TestContext.WriteLine($"Split into {batches.Length} batches");

                using (var connection = new SqlConnection(_connectionString))
                {
                    TestContext.WriteLine($"Connecting to database: {connection.Database} on {connection.DataSource}");
                    connection.Open();
                    TestContext.WriteLine("? Connected successfully");

                    int batchNumber = 0;
                    int successfulBatches = 0;
                    int failedBatches = 0;

                    foreach (var batch in batches)
                    {
                        batchNumber++;
                        
                        if (string.IsNullOrWhiteSpace(batch))
                        {
                            TestContext.WriteLine($"Batch {batchNumber}: Skipped (empty)");
                            continue;
                        }

                        var trimmedBatch = batch.Trim();
                        if (trimmedBatch.Length == 0)
                        {
                            TestContext.WriteLine($"Batch {batchNumber}: Skipped (whitespace only)");
                            continue;
                        }

                        try
                        {
                            TestContext.WriteLine($"Executing batch {batchNumber} ({trimmedBatch.Length} chars)...");
                            
                            using (var command = new SqlCommand(trimmedBatch, connection))
                            {
                                command.CommandTimeout = 120;
                                var rowsAffected = command.ExecuteNonQuery();
                                TestContext.WriteLine($"? Batch {batchNumber} completed. Rows affected: {rowsAffected}");
                                successfulBatches++;
                            }
                        }
                        catch (SqlException ex)
                        {
                            failedBatches++;
                            TestContext.WriteLine($"? ERROR in batch {batchNumber}:");
                            TestContext.WriteLine($"  SQL Error: {ex.Message}");
                            TestContext.WriteLine($"  Error Number: {ex.Number}");
                            TestContext.WriteLine($"  Line Number: {ex.LineNumber}");
                            TestContext.WriteLine($"  Batch preview: {trimmedBatch.Substring(0, Math.Min(200, trimmedBatch.Length))}...");
                            
                            // Don't throw - continue with other batches
                        }
                    }

                    TestContext.WriteLine($"\n=== SCRIPT EXECUTION SUMMARY ===");
                    TestContext.WriteLine($"Script: {scriptFileName}");
                    TestContext.WriteLine($"Total batches: {batchNumber}");
                    TestContext.WriteLine($"Successful: {successfulBatches}");
                    TestContext.WriteLine($"Failed: {failedBatches}");
                    TestContext.WriteLine($"================================\n");

                    if (failedBatches > 0)
                    {
                        TestContext.WriteLine($"WARNING: {failedBatches} batch(es) failed during execution of {scriptFileName}");
                    }
                }

                TestContext.WriteLine($"? Successfully executed {scriptFileName}");
            }
            catch (FileNotFoundException fnfEx)
            {
                TestContext.WriteLine($"FATAL ERROR: {fnfEx.Message}");
                throw; // Re-throw to fail the test setup
            }
            catch (SqlException sqlEx)
            {
                TestContext.WriteLine($"FATAL SQL ERROR executing script {scriptFileName}:");
                TestContext.WriteLine($"  Message: {sqlEx.Message}");
                TestContext.WriteLine($"  Error Number: {sqlEx.Number}");
                TestContext.WriteLine($"  Server: {sqlEx.Server}");
                throw; // Re-throw to fail the test setup
            }
            catch (Exception ex)
            {
                TestContext.WriteLine($"FATAL ERROR executing script {scriptFileName}:");
                TestContext.WriteLine($"  Type: {ex.GetType().Name}");
                TestContext.WriteLine($"  Message: {ex.Message}");
                TestContext.WriteLine($"  Stack: {ex.StackTrace}");
                throw; // Re-throw to fail the test setup
            }
        }


        [Test]
        [TestCaseSource(nameof(GetStringOperatorCombinations))]
        public void CompileRule_StringFieldWithOperator_ShouldCompileSuccessfully(byte fieldId, byte operatorId, string fieldName)
        {
            // Arrange
            var rule = CreateTestRule(fieldId, operatorId, "test_value", fieldName: fieldName);

            // Act
            var compiled = _compiler.CompileRule(rule);

            // Assert
            Assert.IsNotNull(compiled);
            Assert.IsNotNull(compiled.Evaluate);
            Assert.AreEqual(rule.Id, compiled.Id);
            TestContext.WriteLine($"Successfully compiled: Field={fieldId}, Operator={operatorId}");
        }

        [Test]
        [TestCaseSource(nameof(GetNumericOperatorCombinations))]
        public void CompileRule_NumericFieldWithOperator_ShouldCompileSuccessfully(byte fieldId, byte operatorId)
        {
            // Arrange
            var rule = CreateTestRule(fieldId, operatorId, "100");

            // Act
            var compiled = _compiler.CompileRule(rule);

            // Assert
            Assert.IsNotNull(compiled);
            Assert.IsNotNull(compiled.Evaluate);
            TestContext.WriteLine($"Successfully compiled: Field={fieldId}, Operator={operatorId}");
        }

        [Test]
        [TestCaseSource(nameof(GetIpOperatorCombinations))]
        public void CompileRule_IpFieldWithOperator_ShouldCompileSuccessfully(byte fieldId, byte operatorId, string valor)
        {
            // Arrange
            var rule = CreateTestRule(fieldId, operatorId, valor);

            // Act
            var compiled = _compiler.CompileRule(rule);

            // Assert
            Assert.IsNotNull(compiled);
            Assert.IsNotNull(compiled.Evaluate);
            TestContext.WriteLine($"Successfully compiled: Field={fieldId}, Operator={operatorId}, Valor={valor}");
        }

        [Test]
        [TestCaseSource(nameof(GetPresenceOperatorCombinations))]
        public void CompileRule_PresenceOperator_ShouldCompileSuccessfully(byte fieldId, byte operatorId)
        {
            // Arrange
            var rule = CreateTestRule(fieldId, operatorId, "");

            // Act
            var compiled = _compiler.CompileRule(rule);

            // Assert
            Assert.IsNotNull(compiled);
            Assert.IsNotNull(compiled.Evaluate);
            TestContext.WriteLine($"Successfully compiled presence check: Field={fieldId}, Operator={operatorId}");
        }


        [Test]
        public void CompileRule_AllFieldOperatorCombinations_ShouldCompileWithoutErrors()
        {
            // Arrange
            var combinations = GetAllFieldOperatorCombinations();
            int successCount = 0;
            int failureCount = 0;
            var failures = new List<string>();

            // Act
            foreach (var (fieldId, operatorId, valor, fieldName) in combinations)
            {
                try
                {
                    var rule = CreateTestRule(fieldId, operatorId, valor, fieldName: fieldName);
                    var compiled = _compiler.CompileRule(rule);
                    Assert.IsNotNull(compiled.Evaluate);
                    successCount++;
                }
                catch (Exception ex)
                {
                    failureCount++;
                    var message = $"Field={fieldId}, Operator={operatorId}: {ex.Message}";
                    failures.Add(message);
                    TestContext.WriteLine($"FAIL: {message}");
                }
            }

            // Assert
            TestContext.WriteLine($"\n=== COMPILATION SUMMARY ===");
            TestContext.WriteLine($"Total Combinations: {combinations.Count}");
            TestContext.WriteLine($"Successful: {successCount}");
            TestContext.WriteLine($"Failed: {failureCount}");

            if (failures.Count > 0)
            {
                TestContext.WriteLine($"\n=== FAILURES ===");
                foreach (var failure in failures)
                {
                    TestContext.WriteLine(failure);
                }
            }

            // All combinations should compile successfully
            Assert.IsTrue(failureCount == 0, $"{failureCount} combinations failed to compile");
        }

        [Test]
        public void CompileRule_WithNegation_ShouldInvertResult()
        {
            // Arrange
            var ruleWithoutNegate = CreateTestRule(7, 1, "POST"); // Method equals POST
            var ruleWithNegate = CreateTestRule(7, 1, "POST", negate: true);

            var context = TestModelFactory.CreateRequestContext(method: "POST");

            // Act
            var compiledWithout = _compiler.CompileRule(ruleWithoutNegate);
            var compiledWith = _compiler.CompileRule(ruleWithNegate);

            var resultWithout = compiledWithout.Evaluate(context);
            var resultWith = compiledWith.Evaluate(context);

            // Assert
            Assert.IsTrue(resultWithout);
            Assert.IsFalse(resultWith); // Negated should be opposite
        }

        [Test]
        public void CompileRule_WithMultipleGroups_ShouldUseOrLogic()
        {
            // Arrange - create rule with 2 groups (Group1 OR Group2)
            var rule = new WafRule
            {
                Id = 999,
                Nombre = "Test Multi-Group",
                ActionId = 5,
                Prioridad = 10,
                Groups = new List<WafGroup>
                {
                    new WafGroup
                    {
                        Id = 1,
                        Conditions = new List<WafCondition>
                        {
                            new WafCondition { FieldId = 7, OperatorId = 1, Valor = "GET" } // Method = GET
                        }
                    },
                    new WafGroup
                    {
                        Id = 2,
                        Conditions = new List<WafCondition>
                        {
                            new WafCondition { FieldId = 7, OperatorId = 1, Valor = "POST" } // Method = POST
                        }
                    }
                }
            };

            var contextGet = TestModelFactory.CreateRequestContext(method: "GET");
            var contextPost = TestModelFactory.CreateRequestContext(method: "POST");
            var contextPut = TestModelFactory.CreateRequestContext(method: "PUT");

            // Act
            var compiled = _compiler.CompileRule(rule);

            // Assert
            Assert.IsTrue(compiled.Evaluate(contextGet)); // Matches group 1
            Assert.IsTrue(compiled.Evaluate(contextPost)); // Matches group 2
            Assert.IsFalse(compiled.Evaluate(contextPut)); // Matches neither
        }

        #region Helper Methods


        private WafRule CreateTestRule(byte fieldId, byte operatorId, string valor, bool negate = false, string fieldName = null)
        {
            return new WafRule
            {
                Id = 1,
                Nombre = $"TEST: Field={fieldId} Op={operatorId}",
                ActionId = 5,
                Prioridad = 1000,
                Habilitado = true,
                Groups = new List<WafGroup>
                {
                    new WafGroup
                    {
                        Id = 1,
                        Conditions = new List<WafCondition>
                        {
                            new WafCondition
                            {
                                FieldId = fieldId,
                                OperatorId = operatorId,
                                Valor = valor,
                                Negate = negate,
                                FieldName = fieldName
                            }
                        }
                    }
                }
            };
        }

        private static List<(byte fieldId, byte operatorId, string valor, string fieldName)> GetAllFieldOperatorCombinations()
        {
            var combinations = new List<(byte, byte, string, string)>();

            // String operators
            byte[] stringOperators = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 21, 22 };
            byte[] stringFields = { 2, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 20, 21, 22 };

            foreach (var field in stringFields)
            {
                foreach (var op in stringOperators)
                {
                    combinations.Add((field, op, "test_value", field == 16 ? "x-test" : null));
                }
            }

            // List operators
            byte[] listOperators = { 11, 12, 13, 14 };
            foreach (var field in stringFields)
            {
                foreach (var op in listOperators)
                {
                    combinations.Add((field, op, "val1,val2,val3", field == 16 ? "x-test" : null));
                }
            }

            // IP operators
            byte[] ipOperators = { 1, 2, 15, 16, 21, 22 };
            byte[] ipFields = { 3, 4, 23, 24, 25 };
            foreach (var field in ipFields)
            {
                foreach (var op in ipOperators)
                {
                    var valor = op == 15 || op == 16 ? "203.0.113.0/24" : "203.0.113.10";
                    combinations.Add((field, op, valor, null));
                }
            }

            // Numeric operators
            byte[] numericOperators = { 1, 2, 17, 18, 19, 20 };
            byte[] numericFields = { 19 }; // body length
            foreach (var field in numericFields)
            {
                foreach (var op in numericOperators)
                {
                    combinations.Add((field, op, "100", null));
                }
            }

            // Cookie field (special case)
            var allOperators = new List<byte>(stringOperators);
            allOperators.AddRange(listOperators);
            
            foreach (var op in allOperators)
                combinations.Add((1, op, "cookie_value", "sessionid"));

            return combinations;
        }


        public static IEnumerable<TestCaseData> GetStringOperatorCombinations()
        {
            // Test common string fields with string operators
            yield return new TestCaseData((byte)2, (byte)1, null).SetName("hostname equals");
            yield return new TestCaseData((byte)7, (byte)1, null).SetName("method equals");
            yield return new TestCaseData((byte)9, (byte)3, null).SetName("user-agent contains");
            yield return new TestCaseData((byte)13, (byte)7, null).SetName("path starts with");
            yield return new TestCaseData((byte)14, (byte)3, null).SetName("path and query contains");
            yield return new TestCaseData((byte)16, (byte)1, "x-test").SetName("header equals");
        }

        public static IEnumerable<TestCaseData> GetNumericOperatorCombinations()
        {
            yield return new TestCaseData((byte)19, (byte)17).SetName("body length greater than");
            yield return new TestCaseData((byte)19, (byte)18).SetName("body length less than");
            yield return new TestCaseData((byte)19, (byte)19).SetName("body length greater or equal");
            yield return new TestCaseData((byte)19, (byte)20).SetName("body length less or equal");
            yield return new TestCaseData((byte)19, (byte)1).SetName("body length equals");
        }

        public static IEnumerable<TestCaseData> GetIpOperatorCombinations()
        {
            yield return new TestCaseData((byte)3, (byte)1, "203.0.113.10").SetName("IP equals");
            yield return new TestCaseData((byte)3, (byte)15, "203.0.113.0/24").SetName("IP in range");
            yield return new TestCaseData((byte)3, (byte)13, "203.0.113.10,198.51.100.10").SetName("IP in list");
            yield return new TestCaseData((byte)23, (byte)1, "203.0.113.10").SetName("CF-Connecting-IP equals");
            yield return new TestCaseData((byte)24, (byte)15, "203.0.113.0/24").SetName("X-Forwarded-For in range");
        }

        public static IEnumerable<TestCaseData> GetPresenceOperatorCombinations()
        {
            yield return new TestCaseData((byte)9, (byte)21).SetName("user-agent is present");
            yield return new TestCaseData((byte)9, (byte)22).SetName("user-agent is not present");
            yield return new TestCaseData((byte)16, (byte)21).SetName("header is present");
            yield return new TestCaseData((byte)18, (byte)21).SetName("body is present");
        }

        #endregion
    }
}
