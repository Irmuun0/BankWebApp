IF COL_LENGTH('dbo.users', 'password_reset_required') IS NULL
BEGIN
    ALTER TABLE dbo.users
    ADD password_reset_required bit NOT NULL
        CONSTRAINT df_users_password_reset_required DEFAULT (0);
END;
