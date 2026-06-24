IF EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE name = N'chk_notifications_type'
      AND parent_object_id = OBJECT_ID(N'dbo.notifications')
)
BEGIN
    ALTER TABLE dbo.notifications DROP CONSTRAINT chk_notifications_type;
END;

ALTER TABLE dbo.notifications
ADD CONSTRAINT chk_notifications_type CHECK (
    notification_type IN (
        N'TRANSACTION_SUCCESS',
        N'TRANSACTION_FAILED',
        N'SUSPICIOUS_GENERAL',
        N'ACCOUNT_CREATED',
        N'ACCOUNT_STATUS_UPDATED',
        N'SYSTEM',
        N'SECURITY_REVIEW',
        N'SECURITY_REVIEW_UPDATE'
    )
);
