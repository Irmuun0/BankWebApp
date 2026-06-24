IF COL_LENGTH(N'dbo.accounts', N'is_primary') IS NULL
BEGIN
    ALTER TABLE dbo.accounts
    ADD is_primary bit NOT NULL
        CONSTRAINT df_accounts_is_primary DEFAULT (0);
END;
GO

;WITH ranked_accounts AS
(
    SELECT
        id,
        ROW_NUMBER() OVER (
            PARTITION BY user_id
            ORDER BY
                CASE WHEN is_active = 1 THEN 0 ELSE 1 END,
                created_at,
                id
        ) AS rn
    FROM dbo.accounts
)
UPDATE account
SET is_primary = CASE WHEN ranked_accounts.rn = 1 THEN 1 ELSE 0 END
FROM dbo.accounts AS account
INNER JOIN ranked_accounts ON ranked_accounts.id = account.id
WHERE NOT EXISTS (
    SELECT 1
    FROM dbo.accounts AS existing_primary
    WHERE existing_primary.user_id = account.user_id
      AND existing_primary.is_primary = 1
);

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'ux_accounts_one_primary_per_user'
      AND object_id = OBJECT_ID(N'dbo.accounts')
)
BEGIN
    CREATE UNIQUE INDEX ux_accounts_one_primary_per_user
        ON dbo.accounts(user_id)
        WHERE is_primary = 1;
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'idx_accounts_is_primary'
      AND object_id = OBJECT_ID(N'dbo.accounts')
)
BEGIN
    CREATE INDEX idx_accounts_is_primary
        ON dbo.accounts(is_primary);
END;
