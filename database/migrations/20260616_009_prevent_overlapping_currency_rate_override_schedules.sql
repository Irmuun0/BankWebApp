/*
    Prevent overlapping active/scheduled manual override windows per currency rate setting.

    SQL Server has no native exclusion constraint for time ranges, so this trigger
    enforces the business invariant at the database boundary.
*/

CREATE OR ALTER TRIGGER dbo.trg_currency_rate_override_schedules_prevent_overlap
ON dbo.currency_rate_override_schedules
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (
        SELECT 1
        FROM inserted new_schedule
        INNER JOIN dbo.currency_rate_override_schedules existing
            ON existing.currency_rate_setting_id = new_schedule.currency_rate_setting_id
           AND existing.id <> new_schedule.id
           AND existing.status <> N'CANCELLED'
           AND new_schedule.status <> N'CANCELLED'
           AND new_schedule.starts_at < existing.ends_at
           AND new_schedule.ends_at > existing.starts_at
    )
    BEGIN
        RAISERROR(N'Overlapping currency rate override schedule is not allowed.', 16, 1);
        ROLLBACK TRANSACTION;
        RETURN;
    END
END;
