IF OBJECT_ID(N'dbo.user_registration_requests', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.user_registration_requests
    (
        id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT pk_user_registration_requests PRIMARY KEY,
        first_name NVARCHAR(50) NOT NULL,
        last_name NVARCHAR(50) NOT NULL,
        requested_username NVARCHAR(50) NOT NULL,
        email NVARCHAR(100) NOT NULL,
        phone_number NVARCHAR(20) NOT NULL,
        national_id NVARCHAR(20) NOT NULL,
        emergency_phone_number NVARCHAR(20) NULL,
        preferred_contact_method NVARCHAR(20) NOT NULL CONSTRAINT df_user_registration_requests_contact DEFAULT N'EMAIL',
        status NVARCHAR(20) NOT NULL CONSTRAINT df_user_registration_requests_status DEFAULT N'PENDING',
        request_note NVARCHAR(500) NULL,
        admin_note NVARCHAR(500) NULL,
        decision_message NVARCHAR(1000) NULL,
        reviewed_by_admin_id BIGINT NULL,
        reviewed_at DATETIME2(7) NULL,
        created_user_id BIGINT NULL,
        created_at DATETIME2(7) NOT NULL CONSTRAINT df_user_registration_requests_created_at DEFAULT SYSUTCDATETIME(),
        updated_at DATETIME2(7) NOT NULL CONSTRAINT df_user_registration_requests_updated_at DEFAULT SYSUTCDATETIME(),
        CONSTRAINT chk_user_registration_requests_status CHECK (status IN (N'PENDING', N'APPROVED', N'REJECTED', N'NEEDS_INFO')),
        CONSTRAINT chk_user_registration_requests_contact CHECK (preferred_contact_method IN (N'EMAIL', N'PHONE')),
        CONSTRAINT fk_user_registration_requests_reviewed_by FOREIGN KEY (reviewed_by_admin_id) REFERENCES dbo.users(id),
        CONSTRAINT fk_user_registration_requests_created_user FOREIGN KEY (created_user_id) REFERENCES dbo.users(id)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_user_registration_requests_status_created_at' AND object_id = OBJECT_ID(N'dbo.user_registration_requests'))
    CREATE INDEX idx_user_registration_requests_status_created_at ON dbo.user_registration_requests(status, created_at DESC);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_user_registration_requests_requested_username' AND object_id = OBJECT_ID(N'dbo.user_registration_requests'))
    CREATE INDEX idx_user_registration_requests_requested_username ON dbo.user_registration_requests(requested_username);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_user_registration_requests_email' AND object_id = OBJECT_ID(N'dbo.user_registration_requests'))
    CREATE INDEX idx_user_registration_requests_email ON dbo.user_registration_requests(email);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_user_registration_requests_national_id' AND object_id = OBJECT_ID(N'dbo.user_registration_requests'))
    CREATE INDEX idx_user_registration_requests_national_id ON dbo.user_registration_requests(national_id);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_user_registration_requests_created_user_id' AND object_id = OBJECT_ID(N'dbo.user_registration_requests'))
    CREATE INDEX idx_user_registration_requests_created_user_id ON dbo.user_registration_requests(created_user_id);
