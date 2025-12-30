-- Grant SQL Server permissions for NT AUTHORITY\SYSTEM account
-- This is needed when tests run under the SYSTEM account (CI/CD, Windows Services, etc.)

USE master;
GO

-- Create login for NT AUTHORITY\SYSTEM if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.server_principals WHERE name = N'NT AUTHORITY\SYSTEM')
BEGIN
    CREATE LOGIN [NT AUTHORITY\SYSTEM] FROM WINDOWS;
    PRINT 'Created login for NT AUTHORITY\SYSTEM';
END
ELSE
BEGIN
    PRINT 'Login for NT AUTHORITY\SYSTEM already exists';
END
GO

-- Grant server-level permissions
ALTER SERVER ROLE sysadmin ADD MEMBER [NT AUTHORITY\SYSTEM];
PRINT 'Granted sysadmin role to NT AUTHORITY\SYSTEM';
GO

-- Switch to the test database
USE IISFrontGuard_Test;
GO

-- Create user for the database if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = N'NT AUTHORITY\SYSTEM')
BEGIN
    CREATE USER [NT AUTHORITY\SYSTEM] FOR LOGIN [NT AUTHORITY\SYSTEM];
    PRINT 'Created user for NT AUTHORITY\SYSTEM in IISFrontGuard_Test';
END
ELSE
BEGIN
    PRINT 'User NT AUTHORITY\SYSTEM already exists in IISFrontGuard_Test';
END
GO

-- Grant db_owner role
ALTER ROLE db_owner ADD MEMBER [NT AUTHORITY\SYSTEM];
PRINT 'Granted db_owner role to NT AUTHORITY\SYSTEM in IISFrontGuard_Test';
GO

-- Verify permissions
SELECT 
    'NT AUTHORITY\SYSTEM' as Account,
    SUSER_NAME() as CurrentLogin,
    IS_SRVROLEMEMBER('sysadmin', 'NT AUTHORITY\SYSTEM') as IsSysAdmin,
    IS_MEMBER('db_owner') as IsDbOwner;
GO

PRINT 'Permissions granted successfully!';
GO
