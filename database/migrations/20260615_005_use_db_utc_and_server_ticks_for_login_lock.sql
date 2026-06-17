/*
    Make login lock enforcement independent from the web server local clock.

    - *_utc columns store authoritative DB UTC timestamps.
    - locked_until_server_tick stores SQL Server ms_ticks deadline when
      available. ms_ticks is monotonic since SQL Server start and is not moved
      forward by manually changing the OS clock during local testing.
    - Existing local-time columns are kept for compatibility/display.
*/

IF COL_LENGTH(N'dbo.users', N'locked_until_utc') IS NULL
BEGIN
    ALTER TABLE dbo.users
    ADD locked_until_utc DATETIME2(0) NULL;
END

IF COL_LENGTH(N'dbo.users', N'last_failed_login_at_utc') IS NULL
BEGIN
    ALTER TABLE dbo.users
    ADD last_failed_login_at_utc DATETIME2(0) NULL;
END

IF COL_LENGTH(N'dbo.users', N'last_login_at_utc') IS NULL
BEGIN
    ALTER TABLE dbo.users
    ADD last_login_at_utc DATETIME2(0) NULL;
END

IF COL_LENGTH(N'dbo.users', N'locked_until_server_tick') IS NULL
BEGIN
    ALTER TABLE dbo.users
    ADD locked_until_server_tick BIGINT NULL;
END

IF COL_LENGTH(N'dbo.users', N'last_failed_login_server_tick') IS NULL
BEGIN
    ALTER TABLE dbo.users
    ADD last_failed_login_server_tick BIGINT NULL;
END

IF COL_LENGTH(N'dbo.security_event_logs', N'created_at_utc') IS NULL
BEGIN
    ALTER TABLE dbo.security_event_logs
    ADD created_at_utc DATETIME2(0) NULL
        CONSTRAINT df_security_event_logs_created_at_utc DEFAULT SYSUTCDATETIME();
END

GO

UPDATE dbo.users
SET locked_until_utc = DATEADD(HOUR, -8, locked_until)
WHERE locked_until IS NOT NULL
  AND locked_until_utc IS NULL;

UPDATE dbo.users
SET last_failed_login_at_utc = DATEADD(HOUR, -8, last_failed_login_at)
WHERE last_failed_login_at IS NOT NULL
  AND last_failed_login_at_utc IS NULL;

UPDATE dbo.users
SET last_login_at_utc = DATEADD(HOUR, -8, last_login_at)
WHERE last_login_at IS NOT NULL
  AND last_login_at_utc IS NULL;

UPDATE dbo.security_event_logs
SET created_at_utc = DATEADD(HOUR, -8, created_at)
WHERE created_at IS NOT NULL
  AND created_at_utc IS NULL;

DECLARE @serverTick BIGINT = NULL;

BEGIN TRY
    SELECT @serverTick = CAST(ms_ticks AS BIGINT)
    FROM sys.dm_os_sys_info;
END TRY
BEGIN CATCH
    SET @serverTick = NULL;
END CATCH

IF @serverTick IS NOT NULL
BEGIN
    UPDATE dbo.users
    SET locked_until_server_tick = @serverTick + DATEDIFF_BIG(MILLISECOND, SYSUTCDATETIME(), locked_until_utc)
    WHERE locked_until_utc IS NOT NULL
      AND locked_until_utc > SYSUTCDATETIME()
      AND locked_until_server_tick IS NULL;
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'idx_users_locked_until_utc'
      AND object_id = OBJECT_ID(N'dbo.users')
)
BEGIN
    CREATE INDEX idx_users_locked_until_utc
    ON dbo.users (locked_until_utc);
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'idx_security_event_logs_created_at_utc'
      AND object_id = OBJECT_ID(N'dbo.security_event_logs')
)
BEGIN
    CREATE INDEX idx_security_event_logs_created_at_utc
    ON dbo.security_event_logs (created_at_utc DESC);
END
