/*
    Ensure currency rate setting audit table exists for admin rate changes.
*/

IF OBJECT_ID(N'dbo.currency_rate_setting_audits', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.currency_rate_setting_audits (
        id BIGINT IDENTITY(1,1) NOT NULL,
        currency_rate_setting_id BIGINT NOT NULL,
        action NVARCHAR(50) NOT NULL,
        old_buy_rate DECIMAL(18,8) NULL,
        old_sell_rate DECIMAL(18,8) NULL,
        new_buy_rate DECIMAL(18,8) NULL,
        new_sell_rate DECIMAL(18,8) NULL,
        old_is_manual_override BIT NULL,
        new_is_manual_override BIT NULL,
        old_manual_expires_at DATETIME2 NULL,
        new_manual_expires_at DATETIME2 NULL,
        changed_by BIGINT NULL,
        changed_at DATETIME2 NOT NULL
            CONSTRAINT df_currency_rate_setting_audits_changed_at
            DEFAULT CAST(SYSUTCDATETIME() AT TIME ZONE 'UTC' AT TIME ZONE 'Ulaanbaatar Standard Time' AS DATETIME2(0)),
        note NVARCHAR(500) NULL,

        CONSTRAINT pk_currency_rate_setting_audits PRIMARY KEY (id),
        CONSTRAINT fk_currency_rate_setting_audits_setting
            FOREIGN KEY (currency_rate_setting_id) REFERENCES dbo.currency_rate_settings(id)
            ON DELETE CASCADE,
        CONSTRAINT fk_currency_rate_setting_audits_changed_by
            FOREIGN KEY (changed_by) REFERENCES dbo.users(id)
            ON DELETE SET NULL,
        CONSTRAINT chk_currency_rate_setting_audits_action
            CHECK (action IN (N'ALGORITHM_REFRESH', N'ALGORITHM_MARGIN_UPDATED', N'MANUAL_OVERRIDE_SET', N'MANUAL_OVERRIDE_EXPIRED', N'MANUAL_OVERRIDE_DISABLED'))
    );
END

IF OBJECT_ID(N'dbo.chk_currency_rate_setting_audits_action', N'C') IS NOT NULL
BEGIN
    ALTER TABLE dbo.currency_rate_setting_audits DROP CONSTRAINT chk_currency_rate_setting_audits_action;
END

ALTER TABLE dbo.currency_rate_setting_audits
ADD CONSTRAINT chk_currency_rate_setting_audits_action
CHECK (action IN (N'ALGORITHM_REFRESH', N'ALGORITHM_MARGIN_UPDATED', N'MANUAL_OVERRIDE_SET', N'MANUAL_OVERRIDE_EXPIRED', N'MANUAL_OVERRIDE_DISABLED'));

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'idx_currency_rate_setting_audits_setting'
      AND object_id = OBJECT_ID(N'dbo.currency_rate_setting_audits')
)
BEGIN
    CREATE INDEX idx_currency_rate_setting_audits_setting
    ON dbo.currency_rate_setting_audits(currency_rate_setting_id, changed_at DESC);
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'idx_currency_rate_setting_audits_changed_by'
      AND object_id = OBJECT_ID(N'dbo.currency_rate_setting_audits')
)
BEGIN
    CREATE INDEX idx_currency_rate_setting_audits_changed_by
    ON dbo.currency_rate_setting_audits(changed_by, changed_at DESC);
END
