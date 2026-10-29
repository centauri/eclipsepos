SET XACT_ABORT ON;
IF OBJECT_ID('dbo.employee', 'U') IS NULL OR OBJECT_ID('dbo.sub', 'U') IS NULL
    THROW 51000, 'This is not an EclipsePOS v1.0.6/1.0.8 database.', 1;

IF OBJECT_ID('dbo.EclipseSchemaVersion', 'U') IS NULL
    CREATE TABLE dbo.EclipseSchemaVersion(version int NOT NULL PRIMARY KEY, applied_utc datetime2 NOT NULL DEFAULT SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.EclipseSchemaVersion WHERE version = 2)
BEGIN
    ALTER TABLE dbo.employee ALTER COLUMN logon_pass varchar(256) NOT NULL;
    ALTER TABLE dbo.sub ALTER COLUMN subscriber_pass varchar(256) NOT NULL;
    INSERT dbo.EclipseSchemaVersion(version) VALUES (2);
END;
