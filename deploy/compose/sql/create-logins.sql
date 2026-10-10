-- Creates the database and the two least-privilege logins. Run once, as an administrator,
-- before the migrator. Safe to run again.
--
--   scs_app       the application: reads and writes data, cannot alter structure
--   scs_migrator  the migrator: may alter structure; never used by the running application
--
-- sqlcmd variables: SCS_DB, SCS_APP_PASSWORD, SCS_MIGRATOR_PASSWORD

IF DB_ID(N'$(SCS_DB)') IS NULL
    CREATE DATABASE [$(SCS_DB)];
GO

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'scs_app')
    CREATE LOGIN [scs_app] WITH PASSWORD = N'$(SCS_APP_PASSWORD)', CHECK_POLICY = OFF;

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'scs_migrator')
    CREATE LOGIN [scs_migrator] WITH PASSWORD = N'$(SCS_MIGRATOR_PASSWORD)', CHECK_POLICY = OFF;
GO

USE [$(SCS_DB)];
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'scs_app')
    CREATE USER [scs_app] FOR LOGIN [scs_app];

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'scs_migrator')
    CREATE USER [scs_migrator] FOR LOGIN [scs_migrator];
GO

ALTER ROLE [db_datareader] ADD MEMBER [scs_app];
ALTER ROLE [db_datawriter] ADD MEMBER [scs_app];

ALTER ROLE [db_ddladmin] ADD MEMBER [scs_migrator];
ALTER ROLE [db_datareader] ADD MEMBER [scs_migrator];
ALTER ROLE [db_datawriter] ADD MEMBER [scs_migrator];
GO
