IF OBJECT_ID('dbo.chk_accounts_transaction_limit_positive', 'C') IS NOT NULL
BEGIN
    ALTER TABLE dbo.accounts DROP CONSTRAINT chk_accounts_transaction_limit_positive;
END;
GO

IF COL_LENGTH('dbo.accounts', 'daily_transaction_limit_mnt') IS NULL
BEGIN
    IF COL_LENGTH('dbo.accounts', 'transaction_limit_amount') IS NOT NULL
    BEGIN
        EXEC sp_rename 'dbo.accounts.transaction_limit_amount', 'daily_transaction_limit_mnt', 'COLUMN';
    END
    ELSE
    BEGIN
        ALTER TABLE dbo.accounts
            ADD daily_transaction_limit_mnt DECIMAL(18, 2) NULL;
    END
END;
GO

UPDATE dbo.accounts
SET daily_transaction_limit_mnt = 50000000.00
WHERE daily_transaction_limit_mnt IS NULL OR daily_transaction_limit_mnt <= 0;
GO

IF COL_LENGTH('dbo.accounts', 'daily_transaction_limit_mnt') IS NOT NULL
BEGIN
    ALTER TABLE dbo.accounts
        ALTER COLUMN daily_transaction_limit_mnt DECIMAL(18, 2) NOT NULL;
END;
GO

IF OBJECT_ID('dbo.df_accounts_daily_transaction_limit_mnt', 'D') IS NULL
BEGIN
    ALTER TABLE dbo.accounts
        ADD CONSTRAINT df_accounts_daily_transaction_limit_mnt
        DEFAULT (50000000.00) FOR daily_transaction_limit_mnt;
END;
GO

IF OBJECT_ID('dbo.chk_accounts_daily_transaction_limit_positive', 'C') IS NULL
BEGIN
    ALTER TABLE dbo.accounts
        ADD CONSTRAINT chk_accounts_daily_transaction_limit_positive
        CHECK (daily_transaction_limit_mnt > 0);
END;
GO
