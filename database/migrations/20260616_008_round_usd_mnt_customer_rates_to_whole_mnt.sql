/*
    Force USD/MNT customer buy/sell rates to whole MNT values.
    Official/base rates remain decimal; only bank customer rates are normalized.
*/

UPDATE dbo.currency_rate_settings
SET
    algo_buy_rate = FLOOR(algo_buy_rate),
    algo_sell_rate = CEILING(algo_sell_rate),
    manual_buy_rate = CASE
        WHEN manual_buy_rate IS NULL THEN NULL
        ELSE FLOOR(manual_buy_rate)
    END,
    manual_sell_rate = CASE
        WHEN manual_sell_rate IS NULL THEN NULL
        ELSE CEILING(manual_sell_rate)
    END,
    updated_at = CAST(SYSUTCDATETIME() AT TIME ZONE 'UTC' AT TIME ZONE 'Ulaanbaatar Standard Time' AS DATETIME2(0))
WHERE currency_code = N'USD'
  AND base_currency = N'MNT';

UPDATE schedule
SET
    manual_buy_rate = FLOOR(schedule.manual_buy_rate),
    manual_sell_rate = CEILING(schedule.manual_sell_rate)
FROM dbo.currency_rate_override_schedules schedule
INNER JOIN dbo.currency_rate_settings setting
    ON setting.id = schedule.currency_rate_setting_id
WHERE setting.currency_code = N'USD'
  AND setting.base_currency = N'MNT';

UPDATE log_item
SET rate = FLOOR(log_item.rate)
FROM dbo.exchange_rate_logs log_item
WHERE log_item.from_currency = N'USD'
  AND log_item.to_currency = N'MNT'
  AND log_item.source IN (N'ALGORITHM', N'MANUAL_OVERRIDE');
