IF OBJECT_ID(N'dbo.fraud_rule_settings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.fraud_rule_settings
    (
        id INT IDENTITY(1,1) NOT NULL,
        rule_code NVARCHAR(80) NOT NULL,
        display_name NVARCHAR(160) NOT NULL,
        description NVARCHAR(500) NULL,
        is_enabled BIT NOT NULL CONSTRAINT df_fraud_rule_settings_enabled DEFAULT (1),
        score INT NOT NULL,
        numeric_threshold DECIMAL(18,4) NULL,
        amount_threshold_mnt DECIMAL(18,2) NULL,
        amount_threshold_usd DECIMAL(18,2) NULL,
        suspicious_threshold INT NOT NULL CONSTRAINT df_fraud_rule_settings_suspicious_threshold DEFAULT (60),
        updated_at DATETIME2(0) NOT NULL CONSTRAINT df_fraud_rule_settings_updated_at DEFAULT (CONVERT(DATETIME2(0), SYSUTCDATETIME() AT TIME ZONE 'UTC' AT TIME ZONE 'Ulaanbaatar Standard Time')),
        updated_by BIGINT NULL,
        CONSTRAINT pk_fraud_rule_settings PRIMARY KEY (id),
        CONSTRAINT uq_fraud_rule_settings_rule_code UNIQUE (rule_code),
        CONSTRAINT ck_fraud_rule_settings_score CHECK (score >= 0 AND score <= 100),
        CONSTRAINT ck_fraud_rule_settings_suspicious_threshold CHECK (suspicious_threshold >= 1 AND suspicious_threshold <= 100),
        CONSTRAINT fk_fraud_rule_settings_updated_by FOREIGN KEY (updated_by) REFERENCES dbo.users(id) ON DELETE SET NULL
    );

    CREATE INDEX idx_fraud_rule_settings_enabled ON dbo.fraud_rule_settings(is_enabled);
END;

MERGE dbo.fraud_rule_settings AS target
USING (VALUES
    (N'HIGH_AMOUNT_COMPARED_TO_AVERAGE', N'Дундаж дүнгээс өндөр', N'Гүйлгээ хэрэглэгчийн 30 хоногийн дундаж дүнгээс хэд дахин өндөр эсэх.', 1, 35, 3.0000, NULL, NULL),
    (N'VERY_HIGH_AMOUNT', N'Маш өндөр дүн', N'Валют бүрийн маш өндөр дүнгийн босго.', 1, 30, NULL, 5000000.00, 1500.00),
    (N'NIGHT_TIME_TRANSACTION', N'Шөнийн гүйлгээ', N'00:00-с тохируулсан цаг хүртэл хийсэн гүйлгээ.', 1, 15, 6.0000, NULL, NULL),
    (N'MANY_TRANSACTIONS_LAST_24_HOURS', N'24 цагт олон гүйлгээ', N'Сүүлийн 24 цагийн илгээсэн гүйлгээний тоо.', 1, 20, 5.0000, NULL, NULL),
    (N'HIGH_CROSS_CURRENCY_TRANSACTION', N'Өндөр дүнтэй валют хөрвүүлэлт', N'Валют зөрсөн өндөр дүнтэй гүйлгээ.', 1, 15, NULL, 1000000.00, 300.00),
    (N'SUSPICIOUS_DESCRIPTION_KEYWORD', N'Эрсдэлтэй түлхүүр үг', N'Гүйлгээний утгад эрсдэлтэй түлхүүр үг орсон эсэх.', 1, 10, NULL, NULL, NULL),
    (N'MANY_SMALL_TRANSACTIONS', N'Олон жижиг гүйлгээ', N'24 цагт олон жижиг дүнтэй гүйлгээ хийсэн эсэх.', 1, 20, 10.0000, NULL, NULL),
    (N'STRUCTURING_SMALL_SPLIT_TRANSFERS', N'Жижиглэж хуваасан гүйлгээ', N'Олон жижиг гүйлгээний нийлбэр өндөр болсон эсэх.', 1, 35, NULL, 5000000.00, 1500.00),
    (N'RAPID_IN_OUT_FLOW', N'Орж ирсэн мөнгийг хурдан гаргах', N'30 минутын дотор орсон мөнгөний хэдэн хувийг гаргасан эсэх.', 1, 30, 0.8000, NULL, NULL),
    (N'NEW_ACCOUNT_HIGH_AMOUNT', N'Шинэ дансны өндөр дүн', N'Шинэ данснаас өндөр дүнтэй гүйлгээ хийсэн эсэх.', 1, 25, 7.0000, 1000000.00, 300.00),
    (N'DORMANT_ACCOUNT_ACTIVITY', N'Удаан идэвхгүй дансны хөдөлгөөн', N'Олон хоног хөдөлгөөнгүй байсан данс өндөр дүн гаргасан эсэх.', 1, 25, 30.0000, 1000000.00, 300.00),
    (N'MANY_RECEIVERS_SHORT_TIME', N'Олон хүлээн авагч', N'24 цагт олон өөр хүлээн авагч руу мөнгө тараасан эсэх.', 1, 25, 5.0000, NULL, NULL),
    (N'MANY_SENDERS_TO_ONE_ACCOUNT', N'Нэг данс руу олон илгээгч', N'Нэг хүлээн авагч данс руу олон хэрэглэгчээс мөнгө төвлөрсөн эсэх.', 1, 30, 5.0000, NULL, NULL),
    (N'GENERIC_OR_HIDDEN_DESCRIPTION', N'Хэт ерөнхий утга', N'Гүйлгээний утга хэт ерөнхий эсвэл санаатай нуусан мэт эсэх.', 1, 10, NULL, NULL, NULL),
    (N'DESCRIPTION_AMOUNT_MISMATCH', N'Утга ба дүн нийцэхгүй', N'Бага хэрэглээний утгатай боловч дүн өндөр эсэх.', 1, 20, NULL, 3000000.00, 1000.00)
) AS source(rule_code, display_name, description, is_enabled, score, numeric_threshold, amount_threshold_mnt, amount_threshold_usd)
ON target.rule_code = source.rule_code
WHEN NOT MATCHED THEN
    INSERT (rule_code, display_name, description, is_enabled, score, numeric_threshold, amount_threshold_mnt, amount_threshold_usd, suspicious_threshold)
    VALUES (source.rule_code, source.display_name, source.description, source.is_enabled, source.score, source.numeric_threshold, source.amount_threshold_mnt, source.amount_threshold_usd, 60);
