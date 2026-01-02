using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace IISFrontGuard.Module.IntegrationTests
{
    /// <summary>
    /// Shared fixture for all IIS integration tests.
    /// 
    /// IMPORTANT: This fixture does NOT create or configure the IIS site.
    /// It assumes a pre-existing full IIS site is already configured and running at:
    ///   - Site Name: IISFrontGuard_Test
    ///   - URL: http://localhost:5080
    ///   - Physical Path: C:\inetpub\wwwroot\IISFrontGuard_Test
    ///   - App Pool: IISFrontGuard_TestPool
    /// 
    /// Use Setup-IISTestEnvironment.ps1 to create the IIS site before running tests.
    /// 
    /// This fixture ONLY:
    ///   - Deploys/updates binaries to the existing site's bin folder
    ///   - Creates/maintains the SQL Server database schema
    ///   - Seeds test data for each test class
    ///   - Reads connection string from the site's web.config
    /// 
    /// Initialized once and reused across all test classes in the collection.
    /// </summary>
    public sealed class IisIntegrationFixture : IAsyncLifetime, IDisposable
    {
        private static readonly SemaphoreSlim _deploymentLock = new SemaphoreSlim(1, 1);
        private static bool _isDeployed = false;
        private static bool _isDatabaseInitialized = false;

        public string SiteRoot { get; private set; } = "";
        public int Port { get; private set; }
        public Uri BaseUri => new Uri($"http://localhost:{Port}/");
        public HttpClient Client { get; private set; } = default;
        
        private string _connectionString;
        private string _dbName;
        private string _sqlMaster;
        
        /// <summary>
        /// Gets the connection string read from the IIS site's web.config.
        /// This ensures tests use the exact same database configuration as the deployed site.
        /// </summary>
        public string LocalDbAppCs => _connectionString;

        private readonly string _host = "localhost";

        // Physical path of the EXISTING IIS site (must be pre-configured)
        // This fixture does NOT create the IIS site - it must already exist
        private const string IIS_SITE_PHYSICAL_PATH = @"C:\inetpub\wwwroot\IISFrontGuard_Test";

        public async Task InitializeAsync()
        {
            Port = 5080;
            SiteRoot = IIS_SITE_PHYSICAL_PATH;

            // Verify the IIS site directory exists (should be created by setup script)
            if (!Directory.Exists(SiteRoot))
            {
                throw new InvalidOperationException(
                    $"IIS site directory does not exist: {SiteRoot}\n" +
                    "The IIS site must be created before running tests.\n" +
                    "Run Setup-IISTestEnvironment.ps1 to create the required IIS site.");
            }

            // Read connection string from the site's web.config (or fallback to test config)
            ReadConnectionStringFromWebConfig();

            // Thread-safe one-time deployment
            await _deploymentLock.WaitAsync();
            try
            {
                if (!_isDeployed)
                {
                    // Ensure bin directory exists (should exist if site is properly configured)
                    EnsureBinDirectory();

                    // Deploy/update binaries to the EXISTING IIS site
                    CopyBinariesToBin(SiteRoot);

                    _isDeployed = true;
                    Debug.WriteLine("[IisIntegrationFixture] Binaries deployed to existing IIS site");
                }

                if (!_isDatabaseInitialized)
                {
                    // Initialize database schema once
                    EnsureDatabaseAndSchema();
                    ValidateDatabaseSchema();
                    
                    _isDatabaseInitialized = true;
                    Debug.WriteLine("[IisIntegrationFixture] Database initialized");
                }
            }
            finally
            {
                _deploymentLock.Release();
            }

            // Seed test data (can be done per test class)
            SeedRules(_host);

            // Create HTTP client for this instance
            if (Client == null)
            {
                var handler = new HttpClientHandler
                {
                    AllowAutoRedirect = false,
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
                };
                Client = new HttpClient(handler) { BaseAddress = BaseUri, Timeout = TimeSpan.FromSeconds(15) };
            }

            // Wait until the EXISTING IIS site responds
            await WaitUntilUp();
        }

        public Task DisposeAsync()
        {
            // Don't dispose Client or clean database on dispose
            // Let xUnit handle the final cleanup when all tests complete
            
            try
            {
                // Only clean test data, keep schema
                CleanupTestData();
            }
            catch { /* ignore */ }

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            // Final cleanup - called once when all tests complete
            // Does NOT remove or modify the IIS site
            try
            {
                Client?.Dispose();
            }
            catch { /* ignore */ }
        }

        /// <summary>
        /// Reads the connection string from the IIS site's web.config file.
        /// Ensures tests use the exact same database configuration as the deployed site.
        /// If the web.config is missing, attempt to fall back to the test project's configuration
        /// (useful when IIS site is not fully configured in CI/test environments).
        /// </summary>
        private void ReadConnectionStringFromWebConfig()
        {
            var webConfigPath = Path.Combine(SiteRoot, "web.config");

            try
            {
                _connectionString = File.Exists(webConfigPath)
                    ? TryReadFromWebConfig(webConfigPath)
                    : TryReadFromTestConfigAndCreateWebConfig(webConfigPath);

                if (string.IsNullOrEmpty(_connectionString))
                {
                    _connectionString = ConfigurationManager.ConnectionStrings["IISFrontGuardConnection"]?.ConnectionString
                        ?? "Data Source=.;Initial Catalog=IISFrontGuard;Integrated Security=True;TrustServerCertificate=True;";

                    Debug.WriteLine("[IisIntegrationFixture] No web.config connection found - using fallback/test configuration connection string.");
                }

                ExtractDatabaseInfoFromConnectionString();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to read connection string from web.config: {webConfigPath}\n" +
                    $"Error: {ex.Message}", ex);
            }
        }

        private string TryReadFromWebConfig(string webConfigPath)
        {
            var fileMap = new ExeConfigurationFileMap { ExeConfigFilename = webConfigPath };
            var configuration = ConfigurationManager.OpenMappedExeConfiguration(fileMap, ConfigurationUserLevel.None);

            var hostConnectionString = configuration.AppSettings.Settings[$"GlobalLogger.Host.{_host}"]?.Value;
            
            if (!string.IsNullOrEmpty(hostConnectionString))
            {
                Debug.WriteLine($"[IisIntegrationFixture] Using host-specific connection string from GlobalLogger.Host.{_host}");
                return hostConnectionString;
            }

            var connectionStringSettings = configuration.ConnectionStrings.ConnectionStrings["IISFrontGuardConnection"];
            if (connectionStringSettings != null)
            {
                Debug.WriteLine("[IisIntegrationFixture] Using named connection string 'IISFrontGuardConnection'");
                return connectionStringSettings.ConnectionString;
            }

            return null;
        }

        private string TryReadFromTestConfigAndCreateWebConfig(string webConfigPath)
        {
            Debug.WriteLine($"[IisIntegrationFixture] web.config not found at {webConfigPath}. Falling back to test app.config.");

            var connectionString = ReadConnectionStringFromTestConfig();

            if (!string.IsNullOrEmpty(connectionString))
            {
                TryCreateMinimalWebConfig(webConfigPath, connectionString);
            }

            return connectionString;
        }

        private string ReadConnectionStringFromTestConfig()
        {
            var hostConnectionString = ConfigurationManager.AppSettings[$"GlobalLogger.Host.{_host}"];
            if (!string.IsNullOrEmpty(hostConnectionString))
            {
                Debug.WriteLine($"[IisIntegrationFixture] Using host-specific connection string from test config GlobalLogger.Host.{_host}");
                return hostConnectionString;
            }

            var cs = ConfigurationManager.ConnectionStrings["IISFrontGuardConnection"]?.ConnectionString;
            if (!string.IsNullOrEmpty(cs))
            {
                Debug.WriteLine("[IisIntegrationFixture] Using named connection string 'IISFrontGuardConnection' from test config");
                return cs;
            }

            return null;
        }

        private void TryCreateMinimalWebConfig(string webConfigPath, string connectionString)
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
                sb.AppendLine("<configuration>");
                sb.AppendLine("  <appSettings>");
                sb.AppendLine($"    <add key=\"GlobalLogger.Host.{_host}\" value=\"{System.Security.SecurityElement.Escape(connectionString)}\" />");
                sb.AppendLine("  </appSettings>");
                sb.AppendLine("  <connectionStrings>");
                sb.AppendLine($"    <add name=\"IISFrontGuardConnection\" connectionString=\"{System.Security.SecurityElement.Escape(connectionString)}\" providerName=\"System.Data.SqlClient\" />");
                sb.AppendLine("  </connectionStrings>");
                sb.AppendLine("</configuration>");

                File.WriteAllText(webConfigPath, sb.ToString());
                Debug.WriteLine($"[IisIntegrationFixture] Created minimal web.config at {webConfigPath} to configure test connection string.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[IisIntegrationFixture] Warning: Failed to create web.config at {webConfigPath}: {ex.Message}");
            }
        }

        private void ExtractDatabaseInfoFromConnectionString()
        {
            var builder = new SqlConnectionStringBuilder(_connectionString);
            _dbName = builder.InitialCatalog;
            
            if (string.IsNullOrEmpty(_dbName))
            {
                _dbName = "IISFrontGuard";
            }

            var masterBuilder = new SqlConnectionStringBuilder(_connectionString)
            {
                InitialCatalog = "master"
            };
            _sqlMaster = masterBuilder.ConnectionString;

            Debug.WriteLine($"[IisIntegrationFixture] Database: {_dbName}");
            Debug.WriteLine($"[IisIntegrationFixture] Server: {builder.DataSource}");
        }
        
        /// <summary>
        /// Ensures the bin directory exists in the IIS site.
        /// Does NOT create the site itself - only the bin subdirectory if missing.
        /// </summary>
        private void EnsureBinDirectory()
        {
            var binPath = Path.Combine(SiteRoot, "bin");
            if (!Directory.Exists(binPath))
            {
                Directory.CreateDirectory(binPath);
                Debug.WriteLine($"[IisIntegrationFixture] Created bin directory: {binPath}");
            }
        }

        /// <summary>
        /// Copies test binaries to the existing IIS site's bin folder.
        /// Does NOT create or modify IIS configuration.
        /// </summary>
        private static void CopyBinariesToBin(string siteRoot)
        {
            var testOutput = AppDomain.CurrentDomain.BaseDirectory;
            var bin = Path.Combine(siteRoot, "bin");

            Debug.WriteLine($"[IisIntegrationFixture] Deploying binaries from {testOutput} to {bin}");

            int copiedCount = 0;
            int skippedCount = 0;

            // Copy DLLs: IISFrontGuard.Module.dll + dependencies (MaxMind, etc.)
            foreach (var file in Directory.GetFiles(testOutput, "*.dll"))
            {
                var name = Path.GetFileName(file);
                try
                {
                    File.Copy(file, Path.Combine(bin, name), overwrite: true);
                    copiedCount++;
                }
                catch
                {
                    // Ignore locked files (may be in use by IIS)
                    skippedCount++;
                }
            }
            
            // Copy PDB files for debugging
            foreach (var file in Directory.GetFiles(testOutput, "*.pdb"))
            {
                var name = Path.GetFileName(file);
                try
                {
                    File.Copy(file, Path.Combine(bin, name), overwrite: true);
                    copiedCount++;
                }
                catch
                {
                    // Ignore locked files
                    skippedCount++;
                }
            }

            Debug.WriteLine($"[IisIntegrationFixture] Deployed {copiedCount} files, skipped {skippedCount} locked files");
        }

        /// <summary>
        /// Waits for the existing IIS site to respond.
        /// Does NOT start or configure the IIS site.
        /// </summary>
        private async Task WaitUntilUp()
        {
            var start = DateTime.UtcNow;
            while (DateTime.UtcNow - start < TimeSpan.FromSeconds(25))
            {
                try
                {
                    using (var r = await Client.GetAsync("/"))
                        // Any response means the existing IIS site is up
                        return;
                }
                catch(Exception)
                {
                    await Task.Delay(250);
                }
            }

            throw new TimeoutException(
                "The existing IIS site did not respond at http://localhost:5080.\n" +
                "Ensure the IIS site 'IISFrontGuard_Test' is created and running.\n" +
                "Troubleshooting:\n" +
                "  1. Run: Get-IISSite | Where-Object {$_.Name -eq 'IISFrontGuard_Test'}\n" +
                "  2. If missing, run: .\\Setup-IISTestEnvironment.ps1\n" +
                "  3. Check app pool: Get-IISAppPool | Where-Object {$_.Name -eq 'IISFrontGuard_TestPool'}\n" +
                "  4. Start site: Start-IISSite -Name 'IISFrontGuard_Test'");
        }

        private void EnsureDatabaseAndSchema()
        {
            Debug.WriteLine("[IisIntegrationFixture] Ensuring database and schema...");
            
            using (var cn = new SqlConnection(_sqlMaster))
            {
                cn.Open();

                // Create database if it doesn't exist
                using (var cmd = cn.CreateCommand())
                {
                    cmd.CommandText = $@"
IF DB_ID(N'{_dbName}') IS NULL
BEGIN
    CREATE DATABASE [{_dbName}];
END";
                    cmd.ExecuteNonQuery();
                }

                // Verify database was created
                using (var cmd = cn.CreateCommand())
                {
                    cmd.CommandText = $@"SELECT DB_ID(N'{_dbName}')";
                    var dbId = cmd.ExecuteScalar();
                    if (dbId == null || dbId == DBNull.Value)
                        throw new InvalidOperationException($"Failed to access database '{_dbName}'.");
                }
            }

            // Create schema in the application database
            using (var cn = new SqlConnection(LocalDbAppCs))
            {
                cn.Open();

                // Create lookup tables first (no dependencies)
                using (var cmd = cn.CreateCommand())
                {
                    cmd.CommandText = @"
IF OBJECT_ID('dbo.Action','U') IS NULL
BEGIN
  CREATE TABLE dbo.Action(
    Id tinyint IDENTITY(1,1) NOT NULL PRIMARY KEY,
    Name nvarchar(50) NOT NULL,
    Description nvarchar(255) NULL,
    CreatedAt datetime NULL DEFAULT GETDATE()
  );
  
  SET IDENTITY_INSERT dbo.Action ON;
  INSERT INTO dbo.Action (Id, Name, Description) VALUES 
    (1, N'Skip', N'Allow request and skip further WAF processing'),
    (2, N'Block', N'Block the request immediately'),
    (3, N'Managed Challenge', N'Apply managed (automatic) challenge'),
    (4, N'Interactive Challenge', N'Apply interactive challenge (e.g. captcha / JS)'),
    (5, N'Log', N'Log request without enforcement'),
    (6, N'Traffic', N'Traffic');
  SET IDENTITY_INSERT dbo.Action OFF;
END

IF OBJECT_ID('dbo.Field','U') IS NULL
BEGIN
  CREATE TABLE dbo.Field(
    Id tinyint IDENTITY(1,1) NOT NULL PRIMARY KEY,
    Name nvarchar(100) NOT NULL,
    NormalizedName nvarchar(100) NOT NULL UNIQUE,
    Description nvarchar(255) NULL,
    CreatedAt datetime2(7) NOT NULL DEFAULT GETDATE()
  );
  
  SET IDENTITY_INSERT dbo.Field ON;
  INSERT INTO dbo.Field (Id, Name, NormalizedName, Description) VALUES 
    (1, N'cookie', N'COOKIE', N'HTTP cookie value'),
    (2, N'hostname', N'HOSTNAME', N'Request host name'),
    (3, N'ip', N'IP', N'Client IP address'),
    (4, N'ip range', N'IP_RANGE', N'Client IP range'),
    (5, N'protocol', N'PROTOCOL', N'Request protocol (HTTP / HTTPS)'),
    (6, N'referrer', N'REFERRER', N'HTTP referrer header'),
    (7, N'method', N'METHOD', N'HTTP method'),
    (8, N'http version', N'HTTP_VERSION', N'HTTP protocol version'),
    (9, N'user-agent', N'USER_AGENT', N'User-Agent header'),
    (10, N'x-forwarded-for', N'X_FORWARDED_FOR', N'X-Forwarded-For header'),
    (11, N'mime type', N'MIME_TYPE', N'Request MIME type'),
    (12, N'Absolute Uri', N'Absolute_Uri', N'Full request URL'),
    (13, N'Absolute Path', N'Absolute_Path', N'Request URL without query string'),
    (14, N'Path And Query', N'Path_And_Query', N'Request URL path'),
    (15, N'url querystring', N'URL_QUERYSTRING', N'Request query string'),
    (16, N'header', N'HEADER', N'Specific HTTP header'),
    (17, N'content type', N'CONTENT_TYPE', N'Request Content-Type header'),
    (18, N'body', N'BODY', N'Raw request body'),
    (19, N'body length', N'BODY_LENGTH', N'Request body size in bytes'),
    (20, N'country', N'COUNTRY', N'Request country (ISO 3166-1 alpha-2)'),
    (21, N'country-iso2', N'COUNTRY_ISO2', N'Request country (ISO 3166-1 alpha-2)'),
    (22, N'continent', N'CONTINENT', N'Request continent');
  SET IDENTITY_INSERT dbo.Field OFF;
END

IF OBJECT_ID('dbo.Operator','U') IS NULL
BEGIN
  CREATE TABLE dbo.Operator(
    Id tinyint IDENTITY(1,1) NOT NULL PRIMARY KEY,
    Name nvarchar(100) NOT NULL,
    NormalizedName nvarchar(100) NOT NULL UNIQUE,
    Description nvarchar(255) NULL,
    CreatedAt datetime NULL DEFAULT GETDATE()
  );
  
  SET IDENTITY_INSERT dbo.Operator ON;
  INSERT INTO dbo.Operator (Id, Name, NormalizedName, Description) VALUES 
    (1, N'equals', N'EQUALS', N'Value is equal to the target'),
    (2, N'does not equal', N'NOT_EQUALS', N'Value is not equal to the target'),
    (3, N'contains', N'CONTAINS', N'Value contains the target'),
    (4, N'does not contain', N'NOT_CONTAINS', N'Value does not contain the target'),
    (5, N'matches regex', N'REGEX_MATCH', N'Value matches the regular expression'),
    (6, N'does not match regex', N'REGEX_NOT_MATCH', N'Value does not match the regular expression'),
    (7, N'starts with', N'STARTS_WITH', N'Value starts with the target'),
    (8, N'does not start with', N'NOT_STARTS_WITH', N'Value does not start with the target'),
    (9, N'ends with', N'ENDS_WITH', N'Value ends with the target'),
    (10, N'does not end with', N'NOT_ENDS_WITH', N'Value does not end with the target'),
    (11, N'is in', N'IN', N'Value is in the provided set'),
    (12, N'is not in', N'NOT_IN', N'Value is not in the provided set'),
    (13, N'is in list', N'IN_LIST', N'Value is contained in a predefined list'),
    (14, N'is not in list', N'NOT_IN_LIST', N'Value is not contained in a predefined list'),
    (15, N'is ip in range', N'IP_IN_RANGE', N'IP address is within the specified range'),
    (16, N'is ip not in range', N'IP_NOT_IN_RANGE', N'IP address is outside the specified range'),
    (17, N'greater than', N'GREATER_THAN', N'Value is greater than the target'),
    (18, N'less than', N'LESS_THAN', N'Value is less than the target'),
    (19, N'greater than or equal to', N'GREATER_THAN_OR_EQUAL', N'Value is greater than or equal to the target'),
    (20, N'less than or equal to', N'LESS_THAN_OR_EQUAL', N'Value is less than or equal to the target'),
    (21, N'is present', N'IS_PRESENT', N'Value is present (has any value)'),
    (22, N'is not present', N'IS_NOT_PRESENT', N'Value is not present (is empty)');
  SET IDENTITY_INSERT dbo.Operator OFF;
END
";
                    cmd.ExecuteNonQuery();
                }

                // Create main tables (matching init.sql schema exactly)
                using (var cmd = cn.CreateCommand())
                {
                    cmd.CommandText = @"
IF OBJECT_ID('dbo.AppEntity','U') IS NULL
BEGIN
  CREATE TABLE dbo.AppEntity(
    Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWID(),
    AppName nvarchar(255) NOT NULL,
    AppDescription nvarchar(max) NULL,
    Host nvarchar(128) NOT NULL,
    CreationDate datetime NULL DEFAULT GETDATE(),
    TokenExpirationDurationHr tinyint NULL DEFAULT 12
  );
END

IF OBJECT_ID('dbo.WafRuleEntity','U') IS NULL
BEGIN
  CREATE TABLE dbo.WafRuleEntity(
    Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    Nombre nvarchar(255) NULL,
    ActionId tinyint NOT NULL,
    AppId uniqueidentifier NOT NULL,
    Prioridad int NULL DEFAULT 0,
    Habilitado bit NULL DEFAULT 1,
    CreationDate datetime NULL DEFAULT GETDATE(),
    CONSTRAINT FK_WafRuleEntity_ActionId FOREIGN KEY(ActionId) REFERENCES dbo.Action(Id),
    CONSTRAINT FK_WafRuleEntity_AppId FOREIGN KEY(AppId) REFERENCES dbo.AppEntity(Id)
  );
END

IF OBJECT_ID('dbo.WafConditionEntity','U') IS NULL
BEGIN
  CREATE TABLE dbo.WafConditionEntity(
    Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    FieldId tinyint NOT NULL,
    OperatorId tinyint NOT NULL,
    Valor nvarchar(1000) NULL,
    LogicOperator tinyint NULL DEFAULT 1,
    WafRuleEntityId int NOT NULL,
    FieldName varchar(100) NULL,
    ConditionOrder int NOT NULL DEFAULT 0,
    CreationDate datetime NULL DEFAULT GETDATE(),
    CONSTRAINT FK_WafCondition_Rule FOREIGN KEY(WafRuleEntityId) REFERENCES dbo.WafRuleEntity(Id),
    CONSTRAINT FK_WafCondition_Field FOREIGN KEY(FieldId) REFERENCES dbo.Field(Id),
    CONSTRAINT FK_WafCondition_Operator FOREIGN KEY(OperatorId) REFERENCES dbo.Operator(Id)
  );
END

IF OBJECT_ID('dbo.RequestContext','U') IS NULL
BEGIN
  CREATE TABLE dbo.RequestContext(
    Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWID(),
    RayId uniqueidentifier NOT NULL,
    HostName nvarchar(255) NULL,
    AppId uniqueidentifier NULL,
    IPAddress nvarchar(45) NULL,
    Protocol nvarchar(10) NULL,
    Referrer nvarchar(2048) NULL,
    HttpMethod nvarchar(10) NULL,
    HttpVersion nvarchar(20) NULL,
    UserAgent nvarchar(1024) NULL,
    XForwardedFor nvarchar(255) NULL,
    MimeType nvarchar(255) NULL,
    UrlFull nvarchar(2048) NULL,
    UrlPath nvarchar(2048) NULL,
    UrlPathAndQuery nvarchar(2048) NULL,
    UrlQueryString nvarchar(2048) NULL,
    RuleId int NULL,
    ActionId tinyint NULL,
    CreatedAt datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    RequestBody nvarchar(max) NULL,
    CountryIso2 char(2) NULL,
    CONSTRAINT FK_RequestContext_ActionId FOREIGN KEY(ActionId) REFERENCES dbo.Action(Id),
    CONSTRAINT FK_RequestContext_AppId FOREIGN KEY(AppId) REFERENCES dbo.AppEntity(Id),
    CONSTRAINT FK_RequestContext_RuleId FOREIGN KEY(RuleId) REFERENCES dbo.WafRuleEntity(Id)
  );
END

IF OBJECT_ID('dbo.ResponseContext','U') IS NULL
BEGIN
  CREATE TABLE dbo.ResponseContext(
    Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWID(),
    Url nvarchar(max) NOT NULL,
    HttpMethod nvarchar(50) NOT NULL,
    ResponseTime bigint NOT NULL,
    Timestamp datetime NOT NULL,
    RayId uniqueidentifier NULL,
    StatusCode smallint NULL
  );
END
";
                    cmd.ExecuteNonQuery();
                }

                // Create stored procedures for logging
                // Drop InsertRequestContext if exists
                using (var cmd = cn.CreateCommand())
                {
                    cmd.CommandText = @"
IF OBJECT_ID('dbo.InsertRequestContext','P') IS NOT NULL
  DROP PROCEDURE dbo.InsertRequestContext;";
                    cmd.ExecuteNonQuery();
                }

                // Create InsertRequestContext
                using (var cmd = cn.CreateCommand())
                {
                    cmd.CommandText = @"
CREATE PROCEDURE dbo.InsertRequestContext
(
    @RayId              UNIQUEIDENTIFIER,
    @HostName           NVARCHAR(255)    = NULL,
    @AppId              UNIQUEIDENTIFIER = NULL,
    @IPAddress          NVARCHAR(45)     = NULL,
    @Protocol           NVARCHAR(10)     = NULL,
    @Referrer           NVARCHAR(2048)   = NULL,
    @HttpMethod         NVARCHAR(10)     = NULL,
    @HttpVersion        NVARCHAR(20)     = NULL,
    @UserAgent          NVARCHAR(1024)   = NULL,
    @XForwardedFor      NVARCHAR(255)    = NULL,
    @MimeType           NVARCHAR(255)    = NULL,
    @UrlFull            NVARCHAR(2048)   = NULL,
    @UrlPath            NVARCHAR(2048)   = NULL,
    @UrlPathAndQuery    NVARCHAR(2048)   = NULL,
    @UrlQueryString     NVARCHAR(2048)   = NULL,
    @RuleId             INT              = NULL,
    @ActionId           TINYINT          = NULL,
    @RequestBody        NVARCHAR(MAX)    = NULL,
    @CountryIso2        CHAR(2)          = NULL,
    @Id                 UNIQUEIDENTIFIER = NULL,
    @CreatedAt          DATETIME2(3)     = NULL
)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.RequestContext
    (Id, RayId, HostName, AppId, IPAddress, Protocol, Referrer, HttpMethod, HttpVersion, 
     UserAgent, XForwardedFor, MimeType, UrlFull, UrlPath, UrlPathAndQuery, UrlQueryString, 
     RuleId, ActionId, CreatedAt, RequestBody, CountryIso2)
    VALUES
    (COALESCE(@Id, NEWID()), @RayId, @HostName, @AppId, @IPAddress, @Protocol, @Referrer, 
     @HttpMethod, @HttpVersion, @UserAgent, @XForwardedFor, @MimeType, @UrlFull, @UrlPath, 
     @UrlPathAndQuery, @UrlQueryString, @RuleId, @ActionId, COALESCE(@CreatedAt, SYSUTCDATETIME()), 
     @RequestBody, @CountryIso2);
END;";
                    cmd.ExecuteNonQuery();
                }

                // Drop InsertResponseContext if exists
                using (var cmd = cn.CreateCommand())
                {
                    cmd.CommandText = @"
IF OBJECT_ID('dbo.InsertResponseContext','P') IS NOT NULL
  DROP PROCEDURE dbo.InsertResponseContext;";
                    cmd.ExecuteNonQuery();
                }

                // Create InsertResponseContext
                using (var cmd = cn.CreateCommand())
                {
                    cmd.CommandText = @"
CREATE PROCEDURE dbo.InsertResponseContext
(
    @Url          nvarchar(max),
    @HttpMethod   nvarchar(50),
    @ResponseTime bigint,
    @Timestamp    datetime,
    @RayId        uniqueidentifier = NULL,
    @StatusCode   smallint = NULL
)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.ResponseContext (Url, HttpMethod, ResponseTime, [Timestamp], RayId, StatusCode)
    VALUES (@Url, @HttpMethod, @ResponseTime, @Timestamp, @RayId, @StatusCode);
END;";
                    cmd.ExecuteNonQuery();
                }
            }

            Debug.WriteLine("[IisIntegrationFixture] Database schema created successfully");
        }

        private void CleanupTestData()
        {
            try
            {
                using (var cn = new SqlConnection(LocalDbAppCs))
                {
                    cn.Open();
                    using (var cmd = cn.CreateCommand())
                    {
                        cmd.CommandText = @"
-- Clean test data in correct order to respect foreign key constraints
-- ResponseContext has no FK dependencies
DELETE FROM dbo.ResponseContext;

-- RequestContext has FK to WafRuleEntity, AppEntity, Action
DELETE FROM dbo.RequestContext;

-- WafConditionEntity has FK to WafRuleEntity, Field, Operator
DELETE FROM dbo.WafConditionEntity;

-- WafRuleEntity has FK to AppEntity, Action
DELETE FROM dbo.WafRuleEntity;

-- AppEntity is referenced by WafRuleEntity and RequestContext
DELETE FROM dbo.AppEntity;";
                        cmd.ExecuteNonQuery();
                    }
                }
                Debug.WriteLine("[IisIntegrationFixture] Test data cleaned");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[IisIntegrationFixture] Warning: Failed to clean test data: {ex.Message}");
            }
        }

        private void SeedRules(string host)
        {
            using (var cn = new SqlConnection(LocalDbAppCs))
            {
                cn.Open();

                var appId = Guid.NewGuid();

                // clean & insert app + rules
                using (var cmd = cn.CreateCommand())
                {
                    cmd.CommandText = @"
-- Clean existing test data in correct order to respect FK constraints
DELETE FROM dbo.ResponseContext;
DELETE FROM dbo.RequestContext;
DELETE FROM dbo.WafConditionEntity;
DELETE FROM dbo.WafRuleEntity;
DELETE FROM dbo.AppEntity;

INSERT INTO dbo.AppEntity(Id, Host, AppName) VALUES(@AppId, @Host, @AppName);

-- Rule 1: Block when URL path contains 'block'
INSERT INTO dbo.WafRuleEntity(Nombre, ActionId, Prioridad, Habilitado, AppId)
VALUES(N'BlockOnPath', 2, 0, 1, @AppId);
DECLARE @RuleBlockId int = SCOPE_IDENTITY();
INSERT INTO dbo.WafConditionEntity(FieldId, OperatorId, Valor, LogicOperator, WafRuleEntityId, FieldName, ConditionOrder)
VALUES(13, 3, N'block', 1, @RuleBlockId, NULL, 0);

-- Rule 2: Interactive Challenge when URL path contains 'interactive'
INSERT INTO dbo.WafRuleEntity(Nombre, ActionId, Prioridad, Habilitado, AppId)
VALUES(N'ChallengeOnPath', 4, 1, 1, @AppId);
DECLARE @RuleChId int = SCOPE_IDENTITY();
INSERT INTO dbo.WafConditionEntity(FieldId, OperatorId, Valor, LogicOperator, WafRuleEntityId, FieldName, ConditionOrder)
VALUES(13, 3, N'interactive', 1, @RuleChId, NULL, 1);

-- Rule 3: Managed Challenge when URL path contains 'managed'
INSERT INTO dbo.WafRuleEntity(Nombre, ActionId, Prioridad, Habilitado, AppId)
VALUES(N'ManagedChallenge', 3, 2, 1, @AppId);
DECLARE @RuleManagedId int = SCOPE_IDENTITY();
INSERT INTO dbo.WafConditionEntity(FieldId, OperatorId, Valor, LogicOperator, WafRuleEntityId, FieldName, ConditionOrder)
VALUES(13, 3, N'managed', 1, @RuleManagedId, NULL, 2);
";
                    cmd.Parameters.AddWithValue("@AppId", appId);
                    cmd.Parameters.AddWithValue("@Host", host);
                    cmd.Parameters.AddWithValue("@AppName", $"Test App - {host}");
                    cmd.ExecuteNonQuery();
                }
            }
            
            Debug.WriteLine("[IisIntegrationFixture] Test rules seeded");
        }

        private void ValidateDatabaseSchema()
        {
            using (var cn = new SqlConnection(LocalDbAppCs))
            {
                cn.Open();

                // Validate lookup tables exist with correct row counts
                ValidateTable(cn, "Action", 6);
                ValidateTable(cn, "Field", 22);
                ValidateTable(cn, "Operator", 22);

                // Validate main tables exist
                ValidateTable(cn, "AppEntity", null);
                ValidateTable(cn, "WafRuleEntity", null);
                ValidateTable(cn, "WafConditionEntity", null);
                ValidateTable(cn, "RequestContext", null);
                ValidateTable(cn, "ResponseContext", null);

                // Validate stored procedures exist
                ValidateStoredProcedure(cn, "InsertRequestContext");
                ValidateStoredProcedure(cn, "InsertResponseContext");
            }
            
            Debug.WriteLine("[IisIntegrationFixture] Database schema validated");
        }

        private static void ValidateTable(SqlConnection cn, string tableName, int? expectedRowCount)
        {
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = $"SELECT OBJECT_ID('dbo.{tableName}','U')";
                var result = cmd.ExecuteScalar();
                if (result == null || result == DBNull.Value)
                    throw new InvalidOperationException($"Table 'dbo.{tableName}' was not created.");
            }

            if (expectedRowCount.HasValue)
            {
                using (var cmd = cn.CreateCommand())
                {
                    cmd.CommandText = $"SELECT COUNT(*) FROM dbo.{tableName}";
                    var count = (int)cmd.ExecuteScalar();
                    if (count != expectedRowCount.Value)
                        throw new InvalidOperationException($"Table 'dbo.{tableName}' expected {expectedRowCount.Value} rows but found {count}.");
                }
            }            
        }

        private static void ValidateStoredProcedure(SqlConnection cn, string procName)
        {
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = $"SELECT OBJECT_ID('dbo.{procName}','P')";
                var result = cmd.ExecuteScalar();
                if (result == null || result == DBNull.Value)
                    throw new InvalidOperationException($"Stored procedure 'dbo.{procName}' was not created.");
            }
        }

        /// <summary>
        /// Updates an app setting in the existing IIS site's web.config file.
        /// Note: Requires IIS app pool recycle to take effect.
        /// Does NOT create or modify IIS site configuration.
        /// </summary>
        /// <param name="key">The app setting key</param>
        /// <param name="value">The new value</param>
        public void UpdateWebConfigAppSetting(string key, string value)
        {
            if (string.IsNullOrEmpty(SiteRoot))
                throw new InvalidOperationException("Site root not initialized");

            var webConfigPath = Path.Combine(SiteRoot, "web.config");
            if (!File.Exists(webConfigPath))
                throw new FileNotFoundException("web.config not found", webConfigPath);

            var doc = new System.Xml.XmlDocument();
            doc.Load(webConfigPath);

            var node = doc.SelectSingleNode($"//configuration/appSettings/add[@key='{key}']");
            if (node != null)
            {
                node.Attributes["value"].Value = value;
            }
            else
            {
                var appSettings = doc.SelectSingleNode("//configuration/appSettings");
                if (appSettings != null)
                {
                    var newNode = doc.CreateElement("add");
                    newNode.SetAttribute("key", key);
                    newNode.SetAttribute("value", value);
                    appSettings.AppendChild(newNode);
                }
            }

            doc.Save(webConfigPath);
        }

        /// <summary>
        /// Recycles the existing IIS application pool to pick up configuration changes.
        /// Touches web.config which triggers automatic app pool recycle in IIS.
        /// Does NOT create or modify the IIS app pool configuration.
        /// </summary>
        public async Task RecycleAppPoolAsync()
        {
            // Touch web.config to trigger IIS app pool recycle
            var webConfigPath = Path.Combine(SiteRoot, "web.config");
            if (File.Exists(webConfigPath))
            {
                File.SetLastWriteTimeUtc(webConfigPath, DateTime.UtcNow);
                
                // Wait for IIS app pool to recycle and restart
                await Task.Delay(2000);
                
                // Wait for site to be responsive again
                await WaitUntilUp();
            }
        }
    }
}