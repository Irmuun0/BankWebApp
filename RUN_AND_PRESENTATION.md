# BankWebApp.Web ажиллуулах ба танилцуулга

Энэ файл нь `BankWebApp.Web` апп-ыг локал дээр ажиллуулах, өгөгдлийн сан болон FastAPI AI service-тэй холбож шалгах, мөн танилцуулга хийхэд ашиглах үндсэн workflow-ийг нэг дор нэгтгэсэн заавар юм.

## 1. Төслийн бүтэц

```text
BankWebApp/
  BankWebApp.Web/        ASP.NET Core / Blazor web app
  ai_service/            FastAPI rule-based suspicious transaction service
  BankWebApp.slnx        .NET solution file
```

Гол web app:

```powershell
C:\Users\irmuu\Documents\Dadlaga smart logic\BankWebApp\BankWebApp.Web
```

AI service:

```powershell
C:\Users\irmuu\Documents\Dadlaga smart logic\BankWebApp\ai_service
```

Өгөгдлийн сангийн script-үүд:

```powershell
C:\Users\irmuu\Documents\Dadlaga smart logic\system research\database design
```

## 2. Шаардлагатай орчин

- .NET SDK 10.0 эсвэл төслийн `net10.0` target ажиллуулах боломжтой SDK
- SQL Server / SQL Server Express
- Python virtual environment бүхий FastAPI service
- PowerShell

## 3. Өгөгдлийн сан бэлдэх

`BankWebApp.Web` нь MSSQL ашиглана.

Default connection string:

```json
"DefaultConnection": "Server=localhost\\SQLEXPRESS;Database=BankWebAppDb;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;"
```

Хэрвээ өөр SQL Server instance ашиглаж байгаа бол дараах файлын connection string-ийг өөрчилнө:

```powershell
C:\Users\irmuu\Documents\Dadlaga smart logic\BankWebApp\BankWebApp.Web\appsettings.json
```

Шинэ database үүсгэх үндсэн script:

```sql
C:\Users\irmuu\Documents\Dadlaga smart logic\system research\database design\bank_web_app_schema_mssql.sql
```

Demo data оруулах script:

```sql
C:\Users\irmuu\Documents\Dadlaga smart logic\system research\database design\bank_web_app_seed_mssql.sql
```

Одоог хүртэл хөгжүүлэлтийн явцад нэмэгдсэн шаардлагатай migration/script-үүд:

```text
alter_users_first_last_name_mssql.sql
create_currency_rate_settings_mssql.sql
alter_currency_rate_settings_split_margins_mssql.sql
create_fx_income_logs_mssql.sql
create_transaction_detection_logs_mssql.sql
```

Анхаарах зүйл:

- `bank_web_app_seed_mssql.sql` нь demo өгөгдөл үүсгэнэ. Өмнөх өгөгдлөө хадгалах шаардлагатай бол seed script-ийг шууд дахин ажиллуулахын өмнө шалгана.
- Demo хэрэглэгчдийн password DB дээр BCrypt hash байдлаар хадгалагдана. Plain password болон demo placeholder хадгалахгүй.

### 3.1. Migration strategy

Цаашдын DB өөрчлөлтүүдийг дараах folder дотор version-тэй migration байдлаар хадгална:

```powershell
C:\Users\irmuu\Documents\Dadlaga smart logic\BankWebApp\database\migrations
```

Plan буюу ямар migration ажиллахыг харах:

```powershell
cd "C:\Users\irmuu\Documents\Dadlaga smart logic\BankWebApp"
.\database\run-migrations.ps1 -ConnectionString "Server=localhost\SQLEXPRESS;Database=BankWebAppDb;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;" -PlanOnly
```

Migration ажиллуулах:

```powershell
cd "C:\Users\irmuu\Documents\Dadlaga smart logic\BankWebApp"
.\database\run-migrations.ps1 -ConnectionString "Server=localhost\SQLEXPRESS;Database=BankWebAppDb;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;"
```

Ажилласан migration-ууд `dbo.schema_migrations` хүснэгтэд бүртгэгдэнэ. Өмнө ажилласан migration файлыг засахгүй, дараагийн өөрчлөлтийг шинэ migration файл болгон нэмнэ.

## 4. FastAPI AI service асаах

Сэжигтэй гүйлгээ илрүүлэх rule-based service нь `http://localhost:8000` дээр ажиллана.

Terminal 1:

```powershell
cd "C:\Users\irmuu\Documents\Dadlaga smart logic\BankWebApp\ai_service"
.\.venv\Scripts\activate
uvicorn app.main:app --reload --port 8000
```

Service ажиллаж байгаа эсэхийг шалгах:

```powershell
Invoke-RestMethod -Uri "http://localhost:8000/health" -Method Get
```

Хүлээгдэх үр дүн:

```text
status  : ok
service : bank-ai-service
```

Хэрвээ `.venv` байхгүй эсвэл package дутуу бол:

```powershell
cd "C:\Users\irmuu\Documents\Dadlaga smart logic\BankWebApp\ai_service"
python -m venv .venv
.\.venv\Scripts\activate
pip install -r requirements.txt
```

## 5. BankWebApp.Web асаах

Terminal 2:

```powershell
cd "C:\Users\irmuu\Documents\Dadlaga smart logic\BankWebApp"
dotnet restore BankWebApp.slnx
dotnet build BankWebApp.slnx
dotnet run --project BankWebApp.Web
```

Эсвэл шууд web app folder дотор:

```powershell
cd "C:\Users\irmuu\Documents\Dadlaga smart logic\BankWebApp\BankWebApp.Web"
dotnet run
```

Апп ихэвчлэн дараах URL дээр асна:

```text
http://localhost:5155
```

Terminal дээр `Now listening on:` гэж гарсан URL-ийг browser дээр нээнэ.

## 6. Demo login

User login:

```text
URL:      http://localhost:5155/
Username: bat
Password: Password@123
```

Admin login:

```text
URL:      http://localhost:5155/admin/login
Username: admin
Password: Password@123
```

Нэмэлт demo хэрэглэгчид:

```text
saruul / Password@123
temuulen / Password@123
```

Эдгээр demo хэрэглэгчдийн DB дээр хадгалагдсан `password_hash` нь BCrypt hash. Login хийх үед оруулсан password-ийг `BCrypt.Verify` ашиглан шалгана.

Login security:

- Password policy service нь хамгийн багадаа 8 тэмдэгт, том үсэг, жижиг үсэг, тоо, тусгай тэмдэгт, username/email агуулаагүй эсэхийг шалгана.
- Нэг хэрэглэгч нууц үгээ 5 удаа дараалан буруу оруулбал `locked_until` дээр 15 минутын lock хугацаа бичигдэнэ.
- Lock хугацаа дуусахаас өмнө зөв password оруулсан ч login хийхгүй.
- Lock хугацаа дууссаны дараа зөв password оруулбал `failed_login_count`, `locked_until`, `last_failed_login_at` reset хийгдэнэ.
- Login болон logout event-үүд `dbo.security_event_logs` хүснэгтэд хадгалагдана.
- Lock enforcement нь app/browser-ийн local цагт найдахгүй. Систем MSSQL-ээс `SYSUTCDATETIME()` авч, боломжтой үед SQL Server-ийн monotonic `ms_ticks`-ээр 15 минутын хугацааг шалгана.
- USER login session нь нэвтэрснээс хойш 30 минутын дараа дуусна. Sliding expiration унтраалттай тул идэвхтэй ашиглаж байсан ч 30 минут өнгөрөхөд дахин login шаарддаг.
- ADMIN session одоогоор 8 цагийн absolute timeout-той.
- USER интерфэйсийн баруун дээд profile icon-ийн зүүн талд үлдсэн хугацаа `MM:SS` хэлбэрээр харагдана. Дээр нь cursor аваачихад тайлбар мессеж шууд гарна.
- Blazor page нээлттэй хэвээр байвал session expiry monitor автоматаар `/auth/session-expired` рүү шилжүүлж cookie-г цэвэрлэнэ.

## 7. Хэрэглэгчийн үндсэн workflow

Хэрэглэгч нэвтэрсний дараа дараах хэсгүүдийг ашиглана:

- Dashboard
- Миний дансууд
- Данс нээх
- Дансны тохиргоо
- Дансны дэлгэрэнгүй
- Гүйлгээ хийх
- Мэдэгдэл

Гүйлгээ хийх үед:

- Илгээх данс сонгоно.
- Өөрийн данс хооронд эсвэл бусдын данс руу шилжүүлнэ.
- Хүлээн авах данс өөр валюттай бол хөрвүүлэх ханш харуулна.
- MNT/USD хөрвүүлэлтэд системд хадгалсан хамгийн сүүлийн идэвхтэй ханш ашиглагдана.
- Гүйлгээний утга заавал оруулна.
- Амжилттай бол modal popup гарч шилжүүлсэн дүн, огноо цаг, үлдэгдэл, хүлээн авагчийн masked нэр, дансны дугаар, гүйлгээний утгыг харуулна.
- Гүйлгээний дараа FastAPI service рүү rule-based detection request илгээгдэнэ.

## 8. Admin үндсэн workflow

Admin нэвтэрсний дараа:

- Admin dashboard
- Хэрэглэгчид
- Данснууд
- Гүйлгээнүүд
- FX орлогын тайлан
- Сэжигтэй гүйлгээнүүд

Admin талд сэжигтэй гүйлгээний мэдээлэл:

- FastAPI шалгасан эсэх
- Rule-based score
- Илэрсэн rule-үүд
- Risk level
- Admin review status
- Хэрэглэгчид илгээх generic notification

Admin review хийх үед хэрэглэгчид шууд fraud rule-ийн нарийн мэдээлэл харуулахгүй, зөвхөн шалгалтын ерөнхий мэдэгдэл харуулах зарчим баримтална.

FX орлогын тайлан:

- URL: `/admin/fx-income`
- Огнооны интервалаар ханшийн зөрүүнээс олсон орлогыг харуулна.
- Нийт FX орлого, авах ханшийн орлого, зарах ханшийн орлого, дундаж spread, гүйлгээний тоо гэсэн summary card-уудтай.
- Transaction ID, дансны дугаар, валют, income type, гүйлгээний утгаар хайж болно.
- Дэлгэрэнгүй хүснэгт дээр албан ханш, хэрэглэгчид ашигласан ханш, spread, орлого, чиглэл, ханшийн огноо харагдана.

## 9. Ханшийн мэдээлэл

Login дэлгэц дээр Монголбанкны ханшийн мэдээллийг карт хэлбэрээр харуулна.

Харуулж байгаа валютууд:

```text
USD, EUR, JPY, CHF, GBP, HKD, CNY, KRW, CAD, AUD, SGD, NZD, XAU, XAG
```

USD дээр:

- Авах ханш
- Зарах ханш
- Дэлгэрэнгүй popup дээр албан ханш

MNT/USD гүйлгээ хийхэд авах/зарах ханшийг чиглэлээс хамаарч ашиглана.

## 10. Сэжигтэй гүйлгээ илрүүлэх логик

Гүйлгээний үндсэн мөнгөн шилжилт MSSQL transaction дотор хийгдэнэ.

Дараа нь FastAPI AI service рүү detection request илгээнэ:

```text
POST http://localhost:8000/detect-suspicious
```

FastAPI service:

- Rule-based шалгалт хийнэ.
- Score бодно.
- Илэрсэн rule бүрийн тайлбар буцаана.
- Сэжигтэй эсэхийг тогтооно.

Web app:

- Detection result-ийг `transaction_detection_logs` хүснэгтэд хадгална.
- Сэжигтэй гэж үнэлэгдсэн бол `suspicious_transaction_details` хүснэгтэд дэлгэрэнгүй мөр үүсгэнэ.
- Хэрэглэгчид `notifications` хүснэгтээр мэдэгдэл үүсгэнэ.

FastAPI service унтарсан үед:

- Гүйлгээ өөрөө амжилттай хийгдсэн хэвээр үлдэнэ.
- Detection log дээр service unavailable төлөв хадгалагдана.
- Энэ нь банкны үндсэн гүйлгээ AI service-ээс хэт хамааралтай болохоос сэргийлсэн fallback шийдэл юм.

## 11. FastAPI rule test ажиллуулах

Unit test:

```powershell
cd "C:\Users\irmuu\Documents\Dadlaga smart logic\BankWebApp\ai_service"
.\.venv\Scripts\activate
python -m unittest discover -s tests
```

Endpoint test:

Terminal 1 дээр service асаасан байна:

```powershell
cd "C:\Users\irmuu\Documents\Dadlaga smart logic\BankWebApp\ai_service"
.\.venv\Scripts\activate
uvicorn app.main:app --reload --port 8000
```

Terminal 2 дээр:

```powershell
cd "C:\Users\irmuu\Documents\Dadlaga smart logic\BankWebApp\ai_service"
.\.venv\Scripts\activate
.\scripts\test_detect_suspicious.ps1
```

## 12. Ашигтай SQL шалгах query

Сүүлийн detection log:

```sql
SELECT TOP 50
    id,
    transaction_id,
    ai_service_status,
    is_suspicious,
    risk_score,
    risk_level,
    detected_rules,
    created_at
FROM dbo.transaction_detection_logs
ORDER BY created_at DESC;
```

Сэжигтэй гүйлгээ:

```sql
SELECT TOP 50
    id,
    transaction_id,
    risk_level,
    risk_score,
    review_status,
    created_at,
    reviewed_at
FROM dbo.suspicious_transaction_details
ORDER BY created_at DESC;
```

Хэрэглэгчийн мэдэгдэл:

```sql
SELECT TOP 50
    id,
    user_id,
    title,
    message,
    is_read,
    created_at
FROM dbo.notifications
ORDER BY created_at DESC;
```

Security event log:

```sql
SELECT TOP 50
    id,
    user_id,
    username_or_email,
    event_type,
    success,
    message,
    ip_address,
    created_at
FROM dbo.security_event_logs
ORDER BY created_at DESC;
```

Login lock state:

```sql
SELECT
    username,
    failed_login_count,
    locked_until,
    locked_until_utc,
    locked_until_server_tick,
    last_failed_login_at,
    last_failed_login_at_utc,
    last_login_at
FROM dbo.users
ORDER BY id;
```

Ханшийн зөрүүний орлого:

```sql
SELECT TOP 50
    id,
    transaction_id,
    base_currency,
    quote_currency,
    official_rate,
    applied_rate,
    income_amount_mnt,
    created_at
FROM dbo.fx_income_logs
ORDER BY created_at DESC;
```

## 13. Түгээмэл асуудал

### FastAPI рүү холбогдохгүй байх

Алдаа:

```text
Unable to connect to the remote server
```

Шийдэл:

- `ai_service` дээр `uvicorn app.main:app --reload --port 8000` ажиллаж байгаа эсэхийг шалгана.
- `http://localhost:8000/health` шалгана.

### Detection endpoint body parse error

Алдаа:

```text
There was an error parsing the body
```

Шийдэл:

- `scripts\test_detect_suspicious.ps1` шинэ хувилбар байгаа эсэхийг шалгана.
- JSON body UTF-8 болон `ContentType = "application/json; charset=utf-8"` ашиглаж байгаа эсэхийг шалгана.

### MSSQL connection fail

Шийдэл:

- SQL Server service ажиллаж байгаа эсэхийг шалгана.
- `localhost\SQLEXPRESS` instance байгаа эсэхийг шалгана.
- `appsettings.json` дахь connection string зөв эсэхийг шалгана.
- Database name `BankWebAppDb` зөв эсэхийг шалгана.

### HTTPS warning

Terminal дээр дараах warning гарч болно:

```text
Failed to determine the https port for redirect.
```

Development үед HTTP URL ашиглаж байгаа бол ихэвчлэн blocker биш. Browser дээр terminal-ийн `Now listening on:` гэж заасан HTTP URL-ийг ашиглана.

## 14. Танилцуулгын товч script

Танилцуулга хийхдээ дараах дарааллаар үзүүлэхэд ойлгомжтой:

1. Login page нээж user/admin тусдаа нэвтрэх flow байгааг харуулах.
2. Login page дээр ханшийн мэдээлэл, USD авах/зарах ханш, popup дэлгэрэнгүйг харуулах.
3. User-ээр нэвтэрч dashboard, дансны жагсаалт, дансны дэлгэрэнгүйг харуулах.
4. Дансны тохиргоо дээр active/inactive toggle workflow харуулах.
5. Dashboard дээрээс гүйлгээ хийх цэсийг нээж `Өөрийн данс хооронд` болон `Бусдын данс руу` сонголтыг харуулах.
6. MNT/USD хөрвүүлэлттэй гүйлгээ хийж ханш, гүйлгээний дүн, хүлээн авах дүнг харуулах.
7. Амжилттай гүйлгээний popup modal харуулах.
8. Admin-аар нэвтэрч transaction log болон suspicious transaction хэсгийг харуулах.
9. FastAPI service асаалттай үед detection score хадгалагдаж байгааг SQL query эсвэл admin UI дээр харуулах.

## 15. Хөгжүүлэлтийн дараагийн боломжит ажил

- Admin UI дээр ханшийн тохиргоог бүрэн удирдах.
- Ханшийн зөрүүнээс олсон орлогын тайланг admin UI дээр гаргах.
- Gemini API ашиглан admin-д сэжигтэй гүйлгээний analysis гаргах.
- Хэрэглэгчийн chatbot.
- UX workflow сайжруулах.
- 30 минут inactivity timeout.
- Demo password fallback-ийг бүрэн хасаж BCrypt password seed ашиглах.
