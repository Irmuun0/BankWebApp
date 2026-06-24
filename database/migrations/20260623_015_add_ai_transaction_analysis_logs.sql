IF OBJECT_ID(N'dbo.ai_transaction_analysis_logs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ai_transaction_analysis_logs
    (
        id BIGINT IDENTITY(1,1) NOT NULL,
        transaction_id BIGINT NOT NULL,
        analyzed_by BIGINT NOT NULL,
        model_name NVARCHAR(100) NOT NULL,
        is_suspicious BIT NULL,
        risk_score DECIMAL(5,2) NULL,
        explanation NVARCHAR(MAX) NOT NULL,
        recommended_action NVARCHAR(1000) NULL,
        source_context_json NVARCHAR(MAX) NULL,
        created_at DATETIME2(0) NOT NULL
            CONSTRAINT df_ai_analysis_created_at
            DEFAULT CAST(SYSUTCDATETIME() AT TIME ZONE 'UTC' AT TIME ZONE 'Ulaanbaatar Standard Time' AS DATETIME2(0)),

        CONSTRAINT pk_ai_transaction_analysis_logs PRIMARY KEY (id),
        CONSTRAINT fk_ai_analysis_transaction FOREIGN KEY (transaction_id) REFERENCES dbo.transactions(id) ON DELETE CASCADE,
        CONSTRAINT fk_ai_analysis_admin_user FOREIGN KEY (analyzed_by) REFERENCES dbo.users(id) ON DELETE NO ACTION,
        CONSTRAINT chk_ai_analysis_risk_score CHECK (risk_score IS NULL OR (risk_score >= 0 AND risk_score <= 100))
    );
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'idx_ai_analysis_transaction_id'
      AND object_id = OBJECT_ID(N'dbo.ai_transaction_analysis_logs')
)
BEGIN
    CREATE INDEX idx_ai_analysis_transaction_id ON dbo.ai_transaction_analysis_logs(transaction_id);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'idx_ai_analysis_transaction_created'
      AND object_id = OBJECT_ID(N'dbo.ai_transaction_analysis_logs')
)
BEGIN
    CREATE INDEX idx_ai_analysis_transaction_created ON dbo.ai_transaction_analysis_logs(transaction_id, created_at DESC);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'idx_ai_analysis_is_suspicious'
      AND object_id = OBJECT_ID(N'dbo.ai_transaction_analysis_logs')
)
BEGIN
    CREATE INDEX idx_ai_analysis_is_suspicious ON dbo.ai_transaction_analysis_logs(is_suspicious);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'idx_ai_analysis_created_at'
      AND object_id = OBJECT_ID(N'dbo.ai_transaction_analysis_logs')
)
BEGIN
    CREATE INDEX idx_ai_analysis_created_at ON dbo.ai_transaction_analysis_logs(created_at DESC);
END;
