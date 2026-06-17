# Database Migrations

Энэ folder нь `BankWebApp` төслийн цаашдын MSSQL schema өөрчлөлтийг version-тэй хадгалах албан газар.

## Яагаад хэрэгтэй вэ?

Өмнө нь DB өөрчлөлтүүд тусдаа SQL script-үүдээр явж байсан. Энэ нь хурдан хөгжүүлэлтэд тохиромжтой боловч дараах эрсдэлтэй:

- Аль script аль DB дээр ажилласан нь тодорхойгүй болно.
- Нэг script-ийг санамсаргүй дахин ажиллуулж болно.
- Код ба өгөгдлийн сангийн version зөрөх магадлалтай.
- Дараагийн хөгжүүлэлтэд rollback, review, release хийхэд хүндрэлтэй.

Одооноос шинэ DB өөрчлөлт бүрийг migration файл болгон нэмнэ.

## Нэршил

```text
YYYYMMDD_NNN_short_description.sql
```

Жишээ:

```text
20260615_002_add_security_event_logs.sql
20260615_003_add_failed_login_columns.sql
```

Дүрэм:

- Өмнө ажилласан migration файлыг засахгүй.
- Алдаа засах бол шинэ migration нэмнэ.
- `GO` batch separator ашиглаж болно.
- Seed data-г migration-тэй хольж болохгүй. Шаардлагатай бол тусдаа seed script байлгана.
- Migration нь боломжтой үед idempotent шалгалттай байна.

## Ажиллуулах

Project root:

```powershell
cd "C:\Users\irmuu\Documents\Dadlaga smart logic\BankWebApp"
```

Plan буюу ямар migration ажиллахыг харах:

```powershell
.\database\run-migrations.ps1 -ConnectionString "Server=localhost\SQLEXPRESS;Database=BankWebAppDb;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;" -PlanOnly
```

Migration ажиллуулах:

```powershell
.\database\run-migrations.ps1 -ConnectionString "Server=localhost\SQLEXPRESS;Database=BankWebAppDb;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;"
```

Ажилласан migration-уудыг шалгах:

```sql
SELECT
    migration_id,
    script_name,
    checksum,
    applied_at,
    execution_ms,
    success
FROM dbo.schema_migrations
ORDER BY applied_at;
```

## Одоогийн baseline

`20260615_001_baseline_current_schema.sql` нь одоо байгаа schema-г migration system-ийн эхлэх цэг гэж тэмдэглэнэ.

Энэ baseline нь өмнөх том schema script-үүдийг дахин ажиллуулахгүй. Цаашдын өөрчлөлтүүд л шинэ migration болж нэмэгдэнэ.

## Жишээ workflow

1. Шинэ table хэрэгтэй бол `migrations` folder дотор шинэ файл үүсгэнэ.

```text
20260615_002_add_security_event_logs.sql
```

2. SQL өөрчлөлтөө тэр файлд бичнэ.

```sql
IF OBJECT_ID(N'dbo.security_event_logs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.security_event_logs
    (
        id INT IDENTITY(1,1) NOT NULL CONSTRAINT pk_security_event_logs PRIMARY KEY,
        user_id INT NULL,
        event_type NVARCHAR(50) NOT NULL,
        message NVARCHAR(500) NULL,
        created_at DATETIME2(0) NOT NULL CONSTRAINT df_security_event_logs_created_at DEFAULT SYSUTCDATETIME()
    );
END
```

3. Эхлээд `-PlanOnly` ажиллуулж ямар migration ажиллахыг шалгана.

4. Дараа нь `-PlanOnly`-гүй ажиллуулж DB дээр өөрчлөлт оруулна.

5. Ажилласан эсэхийг шалгана.

```sql
SELECT * FROM dbo.schema_migrations ORDER BY applied_at;
```

## Troubleshooting

PowerShell дээр мөр үргэлжлүүлэх backtick ашиглахдаа сүүлийн мөрийн төгсгөлд backtick үлдээж болохгүй. Эхний удаад нэг мөр command ашиглах нь хамгийн амар.

Хэрвээ дараах алдаа гарвал:

```text
Cannot generate SSPI context
```

SQL Server Windows authentication холболтын асуудал гэсэн үг. Дараах хувилбаруудыг туршина:

```powershell
.\database\run-migrations.ps1 -ConnectionString "Server=.\SQLEXPRESS;Database=BankWebAppDb;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;" -PlanOnly
```

Эсвэл `BankWebApp.Web\appsettings.json` дээр ажиллаж байгаа яг тэр connection string-ийг хуулж ашиглана.
