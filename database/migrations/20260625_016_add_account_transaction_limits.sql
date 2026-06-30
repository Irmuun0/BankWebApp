IF COL_LENGTH('dbo.accounts', 'transaction_limit_amount') IS NULL
BEGIN
    ALTER TABLE dbo.accounts
        ADD transaction_limit_amount DECIMAL(18, 2) NULL;
END;
GO

IF OBJECT_ID('dbo.chk_accounts_transaction_limit_positive', 'C') IS NULL
BEGIN
    ALTER TABLE dbo.accounts
        ADD CONSTRAINT chk_accounts_transaction_limit_positive
        CHECK (transaction_limit_amount IS NULL OR transaction_limit_amount > 0);
END;
GO

IF OBJECT_ID('dbo.account_transaction_limit_histories', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.account_transaction_limit_histories
    (
        id BIGINT IDENTITY(1,1) NOT NULL,
        account_id BIGINT NOT NULL,
        old_limit_amount DECIMAL(18, 2) NULL,
        new_limit_amount DECIMAL(18, 2) NULL,
        changed_by_user_id BIGINT NULL,
        reason NVARCHAR(500) NULL,
        created_at DATETIME2 NOT NULL
            CONSTRAINT df_account_transaction_limit_histories_created_at DEFAULT
                (CONVERT(DATETIME2(0), SYSUTCDATETIME() AT TIME ZONE 'UTC' AT TIME ZONE 'Ulaanbaatar Standard Time')),
        CONSTRAINT pk_account_transaction_limit_histories PRIMARY KEY (id),
        CONSTRAINT fk_account_transaction_limit_histories_account
            FOREIGN KEY (account_id) REFERENCES dbo.accounts(id) ON DELETE CASCADE,
        CONSTRAINT fk_account_transaction_limit_histories_admin_user
            FOREIGN KEY (changed_by_user_id) REFERENCES dbo.users(id) ON DELETE SET NULL
    );

    CREATE INDEX idx_account_transaction_limit_histories_account_created
        ON dbo.account_transaction_limit_histories(account_id, created_at DESC);
END;
GO
