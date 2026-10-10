-- Creates the database, the least-privilege logins and the role that isolation relies on.
-- Run once, as an administrator, before the migrator. Safe to run again.
--
--   scs_app       the application: reads and writes data under clinic isolation; cannot alter
--                 structure; cannot write the clinic records; cannot see the access records
--   scs_report    hand-written report queries: reads only, under clinic isolation
--   scs_ops       the operator connection: the only login the application uses to work across
--                 clinics, and only through the operations declared in code
--   scs_migrator  the migrator: may alter structure; used by the pipeline, never by the
--                 running application
--
-- User scs_signin_lookup has no login. One stored procedure runs as it to find which clinic an
-- account belongs to before anyone is signed in (spec 003). Nobody can connect as it, and the
-- application cannot impersonate it.
--
-- Role scs_cross_clinic (members scs_ops, scs_migrator) is what the isolation predicate
-- honours. Membership is granted here, by an administrator, so the application can never
-- widen its own access.
--
-- sqlcmd variables: SCS_DB, SCS_APP_PASSWORD, SCS_REPORT_PASSWORD, SCS_OPS_PASSWORD,
--                   SCS_MIGRATOR_PASSWORD

IF DB_ID(N'$(SCS_DB)') IS NULL
    CREATE DATABASE [$(SCS_DB)];
GO

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'scs_app')
    CREATE LOGIN [scs_app] WITH PASSWORD = N'$(SCS_APP_PASSWORD)', CHECK_POLICY = OFF;

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'scs_report')
    CREATE LOGIN [scs_report] WITH PASSWORD = N'$(SCS_REPORT_PASSWORD)', CHECK_POLICY = OFF;

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'scs_ops')
    CREATE LOGIN [scs_ops] WITH PASSWORD = N'$(SCS_OPS_PASSWORD)', CHECK_POLICY = OFF;

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'scs_migrator')
    CREATE LOGIN [scs_migrator] WITH PASSWORD = N'$(SCS_MIGRATOR_PASSWORD)', CHECK_POLICY = OFF;
GO

USE [$(SCS_DB)];
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'scs_app')
    CREATE USER [scs_app] FOR LOGIN [scs_app];

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'scs_report')
    CREATE USER [scs_report] FOR LOGIN [scs_report];

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'scs_ops')
    CREATE USER [scs_ops] FOR LOGIN [scs_ops];

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'scs_migrator')
    CREATE USER [scs_migrator] FOR LOGIN [scs_migrator];

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'scs_signin_lookup')
    CREATE USER [scs_signin_lookup] WITHOUT LOGIN;

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'scs_cross_clinic' AND type = 'R')
    CREATE ROLE [scs_cross_clinic];
GO

-- Schemas whose permissions are decided here rather than by a migration.
IF SCHEMA_ID(N'tenancy') IS NULL
    EXEC (N'CREATE SCHEMA [tenancy]');

IF SCHEMA_ID(N'crossing') IS NULL
    EXEC (N'CREATE SCHEMA [crossing]');

IF SCHEMA_ID(N'identity') IS NULL
    EXEC (N'CREATE SCHEMA [identity]');

IF SCHEMA_ID(N'security') IS NULL
    EXEC (N'CREATE SCHEMA [security]');
GO

ALTER ROLE [db_datareader] ADD MEMBER [scs_app];
ALTER ROLE [db_datawriter] ADD MEMBER [scs_app];

ALTER ROLE [db_datareader] ADD MEMBER [scs_report];

ALTER ROLE [db_datareader] ADD MEMBER [scs_ops];
ALTER ROLE [db_datawriter] ADD MEMBER [scs_ops];
ALTER ROLE [scs_cross_clinic] ADD MEMBER [scs_ops];

ALTER ROLE [db_ddladmin] ADD MEMBER [scs_migrator];
ALTER ROLE [db_datareader] ADD MEMBER [scs_migrator];
ALTER ROLE [db_datawriter] ADD MEMBER [scs_migrator];
ALTER ROLE [scs_cross_clinic] ADD MEMBER [scs_migrator];

-- The sign-in lookup sees accounts of every clinic, through its one procedure only: it holds no
-- permission of its own, so it can read nothing except what that procedure selects.
ALTER ROLE [scs_cross_clinic] ADD MEMBER [scs_signin_lookup];

-- The migrator creates the isolation policies and the function they call.
GRANT ALTER ANY SECURITY POLICY TO [scs_migrator];
GRANT REFERENCES TO [scs_migrator];

-- The migrator creates the procedure that runs as the sign-in lookup; the application may only
-- execute what the identity schema offers.
GRANT IMPERSONATE ON USER::[scs_signin_lookup] TO [scs_migrator];
GRANT EXECUTE ON SCHEMA::[identity] TO [scs_app];
GO

-- Clinic records and the directory of clinic names are written only by operators.
DENY INSERT, UPDATE, DELETE ON SCHEMA::[tenancy] TO [scs_app];
DENY INSERT, UPDATE, DELETE ON SCHEMA::[tenancy] TO [scs_report];

-- Who looked at which clinic is not visible to any clinic's work.
DENY SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[crossing] TO [scs_app];
DENY SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[crossing] TO [scs_report];

-- An access record is written once and removed when it is older than the retention period.
-- Nothing may alter one, not even the login that writes it.
DENY UPDATE ON SCHEMA::[crossing] TO [scs_ops];
DENY UPDATE ON SCHEMA::[crossing] TO [scs_migrator];
GO

-- Attempts to sign in with an email that matches no account belong to no clinic. The
-- application may add one and nothing else; operators read them.
DENY SELECT, UPDATE, DELETE ON SCHEMA::[security] TO [scs_app];
DENY SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[security] TO [scs_report];
DENY UPDATE ON SCHEMA::[security] TO [scs_ops];
GO
