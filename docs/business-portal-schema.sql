-- Role: Prepares assumed future portal registry tables after sql-demo-schema.sql; owner review only, never agent-applied.
-- V1 runtime does not use these tables. SQL stays single-business with implicit staff membership,
-- existing CustomerPortals.Json and file-backed portal state. This does not enable multi-business catalog SQL.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID(N'dbo.PortalBusinesses', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PortalBusinesses (
        Id nvarchar(40) NOT NULL PRIMARY KEY,
        Slug nvarchar(40) NOT NULL UNIQUE,
        Name nvarchar(120) NOT NULL,
        Kind nvarchar(20) NOT NULL,
        CreatedAt datetimeoffset NOT NULL
    );
END;
IF OBJECT_ID(N'dbo.PortalBusinessMemberships', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PortalBusinessMemberships (
        UserId nvarchar(450) NOT NULL PRIMARY KEY,
        BusinessId nvarchar(40) NOT NULL,
        Role nvarchar(20) NOT NULL,
        FOREIGN KEY (BusinessId) REFERENCES dbo.PortalBusinesses(Id)
    );
END;
COMMIT TRANSACTION;
