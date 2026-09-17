SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.user_registration_requests', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.user_registration_requests', N'decision_message') IS NOT NULL
BEGIN
    DECLARE @marker nvarchar(100) = N'Your Phoebe Bank internet banking account has been approved.';
    DECLARE @safeMessage nvarchar(1000) =
        N'Your Phoebe Bank internet banking account has been approved. '
        + N'The temporary sign-in credential was issued through the selected delivery workflow. '
        + N'The password must be changed after first sign-in.';

    UPDATE dbo.user_registration_requests
    SET decision_message =
        CASE
            WHEN CHARINDEX(@marker, decision_message) > 1
                THEN RTRIM(LEFT(decision_message, CHARINDEX(@marker, decision_message) - 1))
                     + CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10) + @safeMessage
            ELSE @safeMessage
        END,
        updated_at = DATEADD(HOUR, 8, SYSUTCDATETIME())
    WHERE decision_message LIKE N'%Temporary password:%';
END;
