-- Role: Owner-run read-only inventory before the assumed public demo backup and additive schema review.
-- Run only after selecting the existing demo database in a trusted SQL client. This file changes no data or schema.
-- Never run --setup-db, --reset, a reseed, or restore over the existing database for this demonstration.
-- Backup guidance (owner action, not executed by the agent):
-- 1. Record the existing database name, Docker volume mapping and the three absolute storage folders.
-- 2. Pause demo writes yourself. Take a full SQL COPY_ONLY backup with CHECKSUM using your SQL client.
--    Choose a new backup filename in an already mounted backup folder. Never overwrite an existing backup.
-- 3. Copy the completed backup out of the container/volume to private durable storage. Verify it using
--    RESTORE VERIFYONLY WITH CHECKSUM in the trusted client. A restore into a separately named disposable
--    database is the stronger recovery check. Never target the working database for that test.
-- 4. Back up ITEM_ASSET_STORAGE_PATH, BUSINESS_STORAGE_PATH and JEWELRY_STORAGE_PATH together with SQL.
--    Include portal.json, portal assets/audit, registry.json if present, originals and derived image bytes.
--    Preserve the existing AI_DATA_PROTECTION_SECRET privately. Without it the backed-up SQL key ring is unreadable.
--    Do not paste connection strings, the secret, hashes, tokens, key-ring XML or account data into reports/Git.
-- 5. Review docs/sql-demo-schema.sql (generated offline by scripts/prepare-sql-demo.py) and subsequent
--    approved additive migrations against this inventory. Owner applies only after backup verification.
--    Do not blindly replay CREATE scripts or attempt automatic repair when a legacy type differs.
-- 6. Rerun this inventory after owner migration, then use run-public-demo.py check/setup-owner/serve.
--    Startup does not migrate or seed. This inventory cannot prove filesystem backups or real recovery.

SET NOCOUNT ON;

SELECT DB_NAME() AS SelectedDatabase,
       CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128)) AS SqlVersion,
       d.state_desc AS DatabaseState, d.recovery_model_desc AS RecoveryModel,
       d.is_read_only AS IsReadOnly
FROM sys.databases AS d WHERE d.database_id = DB_ID();

-- Presence only: no user rows, password hashes, keys or protected content are returned.
WITH Expected(TableName) AS
(
    SELECT TableName FROM (VALUES
        (N'Item'), (N'Category'), (N'Store'), (N'Metal'), (N'Purity'), (N'Design'),
        (N'ItemVariant'), (N'ItemImage'), (N'StockByStore'), (N'MetalRate'),
        (N'ItemImageAsset'), (N'AspNetUsers'), (N'PermissionGrants'), (N'StoreGrants'),
        (N'CustomerPortals'), (N'AiDataProtectionKeys'), (N'AiPublicationProducts'),
        (N'AiKnowledgeAudit'), (N'AiConversations')) AS names(TableName)
)
SELECT e.TableName, CASE WHEN t.object_id IS NULL THEN N'MISSING: review schema' ELSE N'present' END AS Status
FROM Expected AS e LEFT JOIN sys.tables AS t ON t.name = e.TableName AND t.schema_id = SCHEMA_ID(N'dbo')
ORDER BY e.TableName;

-- Full column shape lets the owner compare nullable/default/concurrency and identity contracts before any ALTER.
SELECT SCHEMA_NAME(t.schema_id) AS SchemaName, t.name AS TableName, c.column_id AS ColumnOrder,
       c.name AS ColumnName, TYPE_NAME(c.system_type_id) AS SqlType, c.max_length AS MaxLengthBytes,
       c.precision AS [Precision], c.scale AS Scale, c.is_nullable AS IsNullable, c.is_identity AS IsIdentity,
       dc.definition AS DefaultDefinition
FROM sys.tables AS t JOIN sys.columns AS c ON c.object_id = t.object_id
LEFT JOIN sys.default_constraints AS dc ON dc.object_id = c.default_object_id
WHERE t.schema_id = SCHEMA_ID(N'dbo')
ORDER BY t.name, c.column_id;

-- Metadata row counts are approximate. They reveal counts only and never inspect private record contents.
SELECT t.name AS TableName, SUM(p.rows) AS ApproximateRows
FROM sys.tables AS t JOIN sys.partitions AS p ON p.object_id = t.object_id AND p.index_id IN (0, 1)
WHERE t.schema_id = SCHEMA_ID(N'dbo') GROUP BY t.name ORDER BY t.name;

SELECT t.name AS TableName, i.name AS IndexName, i.is_unique AS IsUnique,
       i.is_primary_key AS IsPrimaryKey, i.is_disabled AS IsDisabled
FROM sys.tables AS t JOIN sys.indexes AS i ON i.object_id = t.object_id
WHERE t.schema_id = SCHEMA_ID(N'dbo') AND i.index_id > 0 ORDER BY t.name, i.name;
