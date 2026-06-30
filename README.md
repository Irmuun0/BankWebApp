# Phoebe Bank Web App

Phoebe Bank нь ASP.NET Core Blazor дээр хөгжүүлсэн банкны веб системийн дадлагын төсөл. Систем нь хэрэглэгчийн банкны үндсэн workflow, админ удирдлага, валютын ханш, AI/rule-based suspicious transaction detection, Gemini AI туслах, audit/security log зэрэг хэсгүүдтэй.

## Төслийн бүтэц

```text
BankWebApp/
  BankWebApp.Web/        Blazor web app, auth, DB access, admin/user UI
  ai_service/            FastAPI AI service: rule detection, Gemini prompt/API
  database/migrations/   MSSQL schema migration файлууд
  tools/                 Туслах test script-үүд
  BankWebApp.slnx        .NET solution
```

## Гол боломжууд

- User/Admin login, BCrypt password, failed login lock, session timeout
- Хэрэглэгчийн dashboard, данс, дансны тохиргоо, данс нээх
- Өөрийн болон бусдын данс руу гүйлгээ хийх
- MNT/USD ханшийн хөрвүүлэлт, банкны авах/зарах ханш
- Данс бүрийн өдрийн гүйлгээний лимит
- Admin dashboard, хэрэглэгч/данс/гүйлгээ удирдлага
- Rule-based suspicious transaction detection
- Admin AI Detection, Gemini analysis/chat workflow
- Suspicious transaction review, notification, action workflow
- FX income report, audit log, security event log
- Public болон authenticated AI chat туслах

## Шаардлагатай орчин

- .NET SDK 10.0 compatible SDK
- SQL Server / SQL Server Express
- Python 3.11+ ба FastAPI virtual environment
- PowerShell
- Gemini API ашиглах бол `GEMINI_API_KEY` environment variable

## Өгөгдлийн сан

Default local connection string:

```text
Server=localhost\SQLEXPRESS;Database=BankWebAppDb;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;
```

Migration plan харах:

```powershell
cd "C:\Users\irmuu\Documents\Dadlaga smart logic\BankWebApp"
.\database\run-migrations.ps1 -ConnectionString "Server=localhost\SQLEXPRESS;Database=BankWebAppDb;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;" -PlanOnly
```

Migration ажиллуулах:

```powershell
.\database\run-migrations.ps1 -ConnectionString "Server=localhost\SQLEXPRESS;Database=BankWebAppDb;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;"
```

Migration ажилласан эсэх нь `dbo.schema_migrations` хүснэгтэд бүртгэгдэнэ. Өмнө ажилласан migration файлыг засахгүй, дараагийн өөрчлөлтийг шинэ migration болгон нэмнэ.

## FastAPI AI service асаах

```powershell
cd "C:\Users\irmuu\Documents\Dadlaga smart logic\BankWebApp\ai_service"
.\.venv\Scripts\activate
uvicorn app.main:app --reload --port 8000
```

Health check:

```powershell
Invoke-RestMethod -Uri "http://localhost:8000/health" -Method Get
```

Gemini ашиглах бол PowerShell environment variable тохируулна:

```powershell
$env:GEMINI_API_KEY = "YOUR_API_KEY_HERE"
```

Анхаарах зүйл: API key-г `.env`, README, source code руу бодит утгаар нь commit хийхгүй.

## Web app асаах

```powershell
cd "C:\Users\irmuu\Documents\Dadlaga smart logic\BankWebApp"
dotnet restore BankWebApp.slnx
dotnet build BankWebApp.slnx
dotnet run --project BankWebApp.Web
```

Ихэвчлэн дараах URL дээр асна:

```text
http://localhost:5155
```

## Demo login

```text
Admin:
  username: admin
  password: Password@123

User:
  username: bat
  password: Password@123
```

Demo password нь зөвхөн local/demo зорилготой. DB дээр BCrypt hash байдлаар хадгалагдана.

## Found error / known issues тэмдэглэл

- Admin accounts хайлт дээр хэрэглэгчийн бүтэн нэрээр хайхад тухайн хүний данс гарч ирэхгүй, харин username бичихэд гарч ирж байна.
- Гүйлгээний утгын бэлэн сонголт бүх хэрэглэгч дээр `"Бат-с"` гэж харагдаж байна. Одоогийн хэрэглэгчийн нэрээр dynamic болгох шаардлагатай.
- Admin хэрэглэгчийн дансыг хаасны дараа тухайн хэрэглэгч өөрөө буцаагаад идэвхжүүлж болохгүй байх ёстой. Admin decision-ийг user override хийхээс хамгаалах хэрэгтэй.
- Зарим хүснэгт бүх өгөгдлийг нэг дор авч харуулж байна. Жинхэнэ системд pagination/date range/server-side filtering хэрэгтэй.
- Зарим жагсаалтад нэмэлт filter хэрэгтэй: хугацаа, хэрэглэгч, дүнгийн интервал, данс, валют, төлөв.
- Нэвтэрсэн хэрэглэгч AI chat-аас “хамгийн их дүнгээр хийсэн гүйлгээ” гэж асуухад буруу хариулсан. User finance context болон prompt/result validation сайжруулах шаардлагатай.
- Гүйлгээний мөнгөн дүнд үсэг/тэмдэгт оруулахад UI дээр validation сул байна. Зөвхөн тоон утга оруулах client/server validation-ийг баталгаажуулах хэрэгтэй.
- Rule-based detection-ийн `GENERIC_OR_HIDDEN_DESCRIPTION` rule дээр “хэт ерөнхий утга” гэж үзэх үгсийг admin-аас тохируулдаг болгох боломжтой.
- `Request textDocument/... failed` VS/IDE notification гарвал ихэвчлэн C# language server/IntelliSense cache-ийн асуудал. App runtime-ийн алдаа биш байж болно. IDE restart эсвэл IntelliSense cache refresh хийж шалгана.
- FastAPI test дээр `Unable to connect to the remote server` гарвал `ai_service` асаагүй эсвэл port `8000` өөр service ашиглаж байна.
- FastAPI дээр `There was an error parsing the body` гарвал JSON body болон `Content-Type: application/json; charset=utf-8` зөв эсэхийг шалгана.
- Gemini дээр `GEMINI_API_KEY environment variable олдсонгүй` гарвал тухайн terminal/session дотор env var тохируулагдаагүй байна. Шинэ terminal нээсэн бол дахин тохируулна.
- Gemini API `429` эсвэл `503` өгвөл quota/high demand асуудал байж болно. Өөр model/key эсвэл дараа дахин туршина.
- `A second operation was started on this context instance...` гарвал shared `DbContext` concurrency асуудал. Service-үүдэд нэг зэрэг олон EF query эхлүүлэхээс зайлсхийж, transient/factory pattern ашиглана.

## Push хийхийн өмнөх checklist

```powershell
dotnet build BankWebApp.slnx
git status --short
git diff --check
```

Commit-д оруулахгүй зүйлс:

- `bin/`, `obj/`
- `.vs/`, `.vscode/`
- `logs/`
- `__pycache__/`, `*.pyc`
- `App_Data/DataProtectionKeys/`
- `appsettings.Development.json`
- API key, password, personal secret

GitHub руу push хийх жишээ:

```powershell
git add .
git commit -m "Prepare Phoebe Bank web app"
git push -u origin main
```
