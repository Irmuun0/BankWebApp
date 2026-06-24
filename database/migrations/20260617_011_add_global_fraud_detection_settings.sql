IF OBJECT_ID(N'dbo.fraud_detection_settings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.fraud_detection_settings
    (
        id INT NOT NULL,
        suspicious_threshold INT NOT NULL CONSTRAINT df_fraud_detection_settings_threshold DEFAULT (60),
        updated_at DATETIME2(0) NOT NULL CONSTRAINT df_fraud_detection_settings_updated_at DEFAULT (CONVERT(DATETIME2(0), SYSUTCDATETIME() AT TIME ZONE 'UTC' AT TIME ZONE 'Ulaanbaatar Standard Time')),
        updated_by BIGINT NULL,
        CONSTRAINT pk_fraud_detection_settings PRIMARY KEY (id),
        CONSTRAINT ck_fraud_detection_settings_threshold CHECK (suspicious_threshold >= 1 AND suspicious_threshold <= 100),
        CONSTRAINT fk_fraud_detection_settings_updated_by FOREIGN KEY (updated_by) REFERENCES dbo.users(id) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (SELECT 1 FROM dbo.fraud_detection_settings WHERE id = 1)
BEGIN
    INSERT INTO dbo.fraud_detection_settings (id, suspicious_threshold)
    SELECT
        1,
        COALESCE((SELECT TOP (1) suspicious_threshold FROM dbo.fraud_rule_settings ORDER BY id), 60);
END;
