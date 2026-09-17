IF COL_LENGTH(N'dbo.accounts', N'is_admin_locked') IS NULL
BEGIN
    ALTER TABLE dbo.accounts
        ADD is_admin_locked BIT NOT NULL
            CONSTRAINT df_accounts_is_admin_locked DEFAULT (0);
END;

IF COL_LENGTH(N'dbo.accounts', N'admin_locked_at') IS NULL
BEGIN
    ALTER TABLE dbo.accounts ADD admin_locked_at DATETIME2(7) NULL;
END;

IF COL_LENGTH(N'dbo.accounts', N'admin_locked_by_user_id') IS NULL
BEGIN
    ALTER TABLE dbo.accounts ADD admin_locked_by_user_id BIGINT NULL;
END;

-- Preserve admin locks that were created before dedicated lock columns existed.
EXEC sys.sp_executesql N'
;WITH latest_admin_account_action AS
(
    SELECT
        audit.target_id AS account_id,
        audit.user_id,
        audit.created_at,
        audit.action,
        audit.new_value,
        ROW_NUMBER() OVER
        (
            PARTITION BY audit.target_id
            ORDER BY audit.created_at DESC, audit.id DESC
        ) AS row_number
    FROM dbo.audit_logs AS audit
    WHERE audit.target_type = N''accounts''
      AND audit.action IN
      (
          N''ACCOUNT_STATUS_UPDATED'',
          N''SENDER_ACCOUNT_DEACTIVATED'',
          N''RECEIVER_ACCOUNT_DEACTIVATED''
      )
)
UPDATE account
SET
    account.is_admin_locked = 1,
    account.admin_locked_at = action.created_at,
    account.admin_locked_by_user_id = action.user_id
FROM dbo.accounts AS account
INNER JOIN latest_admin_account_action AS action
    ON action.account_id = account.id
   AND action.row_number = 1
WHERE account.is_active = 0
  AND
  (
      action.action IN (N''SENDER_ACCOUNT_DEACTIVATED'', N''RECEIVER_ACCOUNT_DEACTIVATED'')
      OR
      (
          action.action = N''ACCOUNT_STATUS_UPDATED''
          AND LOWER(COALESCE(
              CASE WHEN ISJSON(action.new_value) = 1
                  THEN JSON_VALUE(action.new_value, N''$.IsActive'')
              END,
              N'''')) = N''false''
      )
  );';

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'idx_accounts_is_admin_locked'
      AND object_id = OBJECT_ID(N'dbo.accounts')
)
BEGIN
    EXEC sys.sp_executesql
        N'CREATE INDEX idx_accounts_is_admin_locked ON dbo.accounts(is_admin_locked);';
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.check_constraints
    WHERE name = N'chk_accounts_admin_lock_requires_inactive'
      AND parent_object_id = OBJECT_ID(N'dbo.accounts')
)
BEGIN
    EXEC sys.sp_executesql N'
        ALTER TABLE dbo.accounts WITH CHECK
            ADD CONSTRAINT chk_accounts_admin_lock_requires_inactive
                CHECK (is_admin_locked = 0 OR is_active = 0);';
END;
