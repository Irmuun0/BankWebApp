# Bank AI Service

Rule-based suspicious transaction detection service for the Bank Web Software MVP.

## Run on Windows

```powershell
cd ai_service
python -m venv .venv
.\.venv\Scripts\activate
pip install -r requirements.txt
uvicorn app.main:app --reload --port 8000
```

## Run on Linux/macOS

```bash
cd ai_service
python -m venv .venv
source .venv/bin/activate
pip install -r requirements.txt
uvicorn app.main:app --reload --port 8000
```

## Endpoints

- `GET http://localhost:8000/health`
- `POST http://localhost:8000/detect-suspicious`
- Swagger: `http://localhost:8000/docs`

The service receives only transaction-related values. It must not receive password hashes, national IDs, phone numbers, email addresses, or full user profiles.

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
