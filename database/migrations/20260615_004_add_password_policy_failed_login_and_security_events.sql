/*
    Add login security state and security event logging.

    Rules implemented in application code:
    - Password policy is enforced by PasswordPolicyService for future password
      create/change flows.
    - 5 consecutive bad password attempts lock the account for 15 minutes.
    - Login/security events are written to dbo.security_event_logs.
*/

IF COL_LENGTH(N'dbo.users', N'failed_login_count') IS NULL
BEGIN
    ALTER TABLE dbo.users
    ADD failed_login_count INT NOT NULL
        CONSTRAINT df_users_failed_login_count DEFAULT 0;
END

IF COL_LENGTH(N'dbo.users', N'locked_until') IS NULL
BEGIN
    ALTER TABLE dbo.users
    ADD locked_until DATETIME2(0) NULL;
END

IF COL_LENGTH(N'dbo.users', N'last_failed_login_at') IS NULL
BEGIN
    ALTER TABLE dbo.users
    ADD last_failed_login_at DATETIME2(0) NULL;
END

IF COL_LENGTH(N'dbo.users', N'password_changed_at') IS NULL
BEGIN
    ALTER TABLE dbo.users
    ADD password_changed_at DATETIME2(0) NULL;
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'idx_users_locked_until'
      AND object_id = OBJECT_ID(N'dbo.users')
)
BEGIN
    CREATE INDEX idx_users_locked_until
    ON dbo.users (locked_until);
END

IF OBJECT_ID(N'dbo.security_event_logs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.security_event_logs
    (
        id BIGINT IDENTITY(1,1) NOT NULL,
        user_id BIGINT NULL,
        username_or_email NVARCHAR(100) NULL,
        event_type NVARCHAR(80) NOT NULL,
        success BIT NOT NULL,
        message NVARCHAR(500) NULL,
        ip_address NVARCHAR(45) NULL,
        user_agent NVARCHAR(255) NULL,
        created_at DATETIME2(0) NOT NULL
            CONSTRAINT df_security_event_logs_created_at
            DEFAULT CAST(SYSUTCDATETIME() AT TIME ZONE 'UTC' AT TIME ZONE 'Ulaanbaatar Standard Time' AS DATETIME2(0)),
        CONSTRAINT pk_security_event_logs PRIMARY KEY (id),
        CONSTRAINT fk_security_event_logs_user
            FOREIGN KEY (user_id) REFERENCES dbo.users(id)
            ON DELETE SET NULL
    );
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'idx_security_event_logs_user_created'
      AND object_id = OBJECT_ID(N'dbo.security_event_logs')
)
BEGIN
    CREATE INDEX idx_security_event_logs_user_created
    ON dbo.security_event_logs (user_id, created_at DESC);
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'idx_security_event_logs_event_created'
      AND object_id = OBJECT_ID(N'dbo.security_event_logs')
)
BEGIN
    CREATE INDEX idx_security_event_logs_event_created
    ON dbo.security_event_logs (event_type, created_at DESC);
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'idx_security_event_logs_created'
      AND object_id = OBJECT_ID(N'dbo.security_event_logs')
)
BEGIN
    CREATE INDEX idx_security_event_logs_created
    ON dbo.security_event_logs (created_at DESC);
END
