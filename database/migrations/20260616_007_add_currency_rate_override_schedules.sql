/*
    Add scheduled manual override support for bank buy/sell exchange rates.
*/

IF OBJECT_ID(N'dbo.currency_rate_override_schedules', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.currency_rate_override_schedules (
        id BIGINT IDENTITY(1,1) NOT NULL,
        currency_rate_setting_id BIGINT NOT NULL,
        manual_buy_rate DECIMAL(18,8) NOT NULL,
        manual_sell_rate DECIMAL(18,8) NOT NULL,
        starts_at DATETIME2 NOT NULL,
        ends_at DATETIME2 NOT NULL,
        status NVARCHAR(20) NOT NULL
            CONSTRAINT df_currency_rate_override_schedules_status DEFAULT N'SCHEDULED',
        created_by BIGINT NULL,
        created_at DATETIME2 NOT NULL
            CONSTRAINT df_currency_rate_override_schedules_created_at
            DEFAULT CAST(SYSUTCDATETIME() AT TIME ZONE 'UTC' AT TIME ZONE 'Ulaanbaatar Standard Time' AS DATETIME2(0)),
        cancelled_by BIGINT NULL,
        cancelled_at DATETIME2 NULL,
        note NVARCHAR(500) NULL,

        CONSTRAINT pk_currency_rate_override_schedules PRIMARY KEY (id),
        CONSTRAINT fk_currency_rate_override_schedules_setting
            FOREIGN KEY (currency_rate_setting_id) REFERENCES dbo.currency_rate_settings(id)
            ON DELETE CASCADE,
        CONSTRAINT fk_currency_rate_override_schedules_created_by
            FOREIGN KEY (created_by) REFERENCES dbo.users(id)
            ON DELETE SET NULL,
        CONSTRAINT fk_currency_rate_override_schedules_cancelled_by
            FOREIGN KEY (cancelled_by) REFERENCES dbo.users(id)
            ON DELETE NO ACTION,
        CONSTRAINT chk_currency_rate_override_schedules_status
            CHECK (status IN (N'SCHEDULED', N'CANCELLED')),
        CONSTRAINT chk_currency_rate_override_schedules_time
            CHECK (ends_at > starts_at),
        CONSTRAINT chk_currency_rate_override_schedules_rates
            CHECK (manual_buy_rate > 0 AND manual_sell_rate > 0)
    );
END

IF OBJECT_ID(N'dbo.chk_currency_rate_setting_audits_action', N'C') IS NOT NULL
BEGIN
    ALTER TABLE dbo.currency_rate_setting_audits DROP CONSTRAINT chk_currency_rate_setting_audits_action;
END

ALTER TABLE dbo.currency_rate_setting_audits
ADD CONSTRAINT chk_currency_rate_setting_audits_action
CHECK (action IN (
    N'ALGORITHM_REFRESH',
    N'ALGORITHM_MARGIN_UPDATED',
    N'MANUAL_OVERRIDE_SET',
    N'MANUAL_OVERRIDE_EXPIRED',
    N'MANUAL_OVERRIDE_DISABLED',
    N'MANUAL_OVERRIDE_SCHEDULED',
    N'MANUAL_OVERRIDE_CANCELLED'
));

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'idx_currency_rate_override_schedules_setting_time'
      AND object_id = OBJECT_ID(N'dbo.currency_rate_override_schedules')
)
BEGIN
    CREATE INDEX idx_currency_rate_override_schedules_setting_time
    ON dbo.currency_rate_override_schedules(currency_rate_setting_id, starts_at, ends_at);
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'idx_currency_rate_override_schedules_status'
      AND object_id = OBJECT_ID(N'dbo.currency_rate_override_schedules')
)
BEGIN
    CREATE INDEX idx_currency_rate_override_schedules_status
    ON dbo.currency_rate_override_schedules(status);
END

INSERT INTO dbo.currency_rate_override_schedules (
    currency_rate_setting_id,
    manual_buy_rate,
    manual_sell_rate,
    starts_at,
    ends_at,
    status,
    created_by,
    created_at,
    note
)
SELECT
    setting.id,
    setting.manual_buy_rate,
    setting.manual_sell_rate,
    CASE
        WHEN setting.updated_at < CAST(SYSUTCDATETIME() AT TIME ZONE 'UTC' AT TIME ZONE 'Ulaanbaatar Standard Time' AS DATETIME2(0))
            THEN setting.updated_at
        ELSE CAST(SYSUTCDATETIME() AT TIME ZONE 'UTC' AT TIME ZONE 'Ulaanbaatar Standard Time' AS DATETIME2(0))
    END,
    setting.manual_expires_at,
    N'SCHEDULED',
    setting.updated_by,
    setting.updated_at,
    N'Migrated from legacy manual override columns.'
FROM dbo.currency_rate_settings setting
WHERE setting.is_manual_override = 1
  AND setting.manual_buy_rate IS NOT NULL
  AND setting.manual_sell_rate IS NOT NULL
  AND setting.manual_expires_at IS NOT NULL
  AND setting.manual_expires_at > CAST(SYSUTCDATETIME() AT TIME ZONE 'UTC' AT TIME ZONE 'Ulaanbaatar Standard Time' AS DATETIME2(0))
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.currency_rate_override_schedules existing
      WHERE existing.currency_rate_setting_id = setting.id
        AND existing.starts_at = setting.updated_at
        AND existing.ends_at = setting.manual_expires_at
        AND existing.status = N'SCHEDULED'
  );
