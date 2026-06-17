/*
    Store migration applied_at values in Asia/Ulaanbaatar local time.

    The initial migration runner used SYSUTCDATETIME(), so the baseline row was
    stored 8 hours behind local time. This migration updates the default
    constraint and corrects the existing baseline timestamp.
*/

DECLARE @constraintName SYSNAME;
DECLARE @sql NVARCHAR(MAX);

SELECT @constraintName = dc.name
FROM sys.default_constraints dc
INNER JOIN sys.columns c
    ON c.default_object_id = dc.object_id
INNER JOIN sys.tables t
    ON t.object_id = c.object_id
INNER JOIN sys.schemas s
    ON s.schema_id = t.schema_id
WHERE s.name = N'dbo'
  AND t.name = N'schema_migrations'
  AND c.name = N'applied_at';

IF @constraintName IS NOT NULL
BEGIN
    SET @sql = N'ALTER TABLE dbo.schema_migrations DROP CONSTRAINT ' + QUOTENAME(@constraintName) + N';';
    EXEC sp_executesql @sql;
END

ALTER TABLE dbo.schema_migrations
ADD CONSTRAINT df_schema_migrations_applied_at
DEFAULT CAST(SYSUTCDATETIME() AT TIME ZONE 'UTC' AT TIME ZONE 'Ulaanbaatar Standard Time' AS DATETIME2(0))
FOR applied_at;

UPDATE dbo.schema_migrations
SET applied_at = DATEADD(HOUR, 8, applied_at)
WHERE migration_id = N'20260615_001_baseline_current_schema'
  AND applied_at = '2026-06-15T02:56:35';
