# Bank AI Service

Rule-based suspicious transaction detection and Gemini analysis service for the Bank Web Software MVP.

This service does not connect to MSSQL. `BankWebApp.Web` prepares a sanitized transaction context, sends it to this API, then stores AI results and audit logs itself.

## Run on Windows

```powershell
cd ai_service
python -m venv .venv
.\.venv\Scripts\activate
pip install -r requirements.txt
$env:GEMINI_API_KEY = "YOUR_API_KEY_HERE"
uvicorn app.main:app --reload --port 8000
```

## Run on Linux/macOS

```bash
cd ai_service
python -m venv .venv
source .venv/bin/activate
pip install -r requirements.txt
export GEMINI_API_KEY="YOUR_API_KEY_HERE"
uvicorn app.main:app --reload --port 8000
```

## Endpoints

- `GET http://localhost:8000/health`
- `POST http://localhost:8000/detect-suspicious`
- `POST http://localhost:8000/analyze-transaction`
- `POST http://localhost:8000/chat/ask`
- `POST http://localhost:8000/chat/explain`
- `POST http://localhost:8000/chat/bank-info`
- `POST http://localhost:8000/chat/user-finance`
- Swagger: `http://localhost:8000/docs`

The service receives only transaction-related values. It must not receive password hashes, national IDs, phone numbers, email addresses, or full user profiles.

## Gemini configuration

Use environment variables or a local `.env` file:

```text
GEMINI_API_KEY=your_api_key_here
GEMINI_MODEL=gemini-3.1-flash-lite
GEMINI_BASE_URL=https://generativelanguage.googleapis.com/v1beta
GEMINI_TIMEOUT_SECONDS=120
```

`BankWebApp.Web` no longer calls Gemini directly. It calls this service using `AiService:BaseUrl`.

## Run rule tests

### 1. Unit tests

Unit tests do not require the FastAPI server or MSSQL. They call the rule
function directly.

```powershell
cd "C:\Users\irmuu\Documents\Dadlaga smart logic\BankWebApp\ai_service"
.\.venv\Scripts\activate
python -m unittest discover -s tests
```

Expected result:

```text
Ran 16 tests
OK
```

### 2. Endpoint sample tests

Endpoint tests require FastAPI to be running.

Terminal 1:

```powershell
cd "C:\Users\irmuu\Documents\Dadlaga smart logic\BankWebApp\ai_service"
.\.venv\Scripts\activate
uvicorn app.main:app --reload --port 8000
```

Confirm this works in a browser:

```text
http://localhost:8000/health
```

Expected response:

```json
{"status":"ok","service":"bank-ai-service"}
```

Terminal 2:

```powershell
cd "C:\Users\irmuu\Documents\Dadlaga smart logic\BankWebApp\ai_service"
.\.venv\Scripts\activate
.\scripts\test_detect_suspicious.ps1
```

If the service uses another port:

```powershell
.\scripts\test_detect_suspicious.ps1 -BaseUrl "http://localhost:8001"
```


