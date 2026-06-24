# Run from the ai_service folder after starting FastAPI:
# .\scripts\test_gemini_analysis.ps1
#
# Start service example:
# cd "C:\Users\irmuu\Documents\Dadlaga smart logic\BankWebApp\ai_service"
# .\.venv\Scripts\activate
# $env:GEMINI_API_KEY = "YOUR_API_KEY_HERE"
# uvicorn app.main:app --reload --port 8000

param(
    [string]$BaseUrl = "http://localhost:8000"
)

$ErrorActionPreference = "Stop"

Write-Host "Checking health..." -ForegroundColor Cyan
Invoke-RestMethod -Uri "$BaseUrl/health" -Method Get | Format-List

$context = @{
    transactionId = 125
    createdAt = "2026-06-24T01:20:00"
    amount = 5000000
    sourceCurrency = "MNT"
    creditedAmount = 1399.00
    targetCurrency = "USD"
    riskScore = 80
    suspiciousReason = "High amount compared to average. Night-time transaction."
    reviewStatus = "PENDING"
    fromAccountMasked = "100****168"
    toAccountMasked = "200****901"
    description = "GPU payment test"
    isCrossCurrency = $true
    exchangeRateValue = 3576.21
    detectionCheckedAt = "2026-06-24T01:21:00"
}

$analysisBody = @{
    context = $context
    modelName = "gemini-3.1-flash-lite"
}

Write-Host ""
Write-Host "Testing /analyze-transaction..." -ForegroundColor Cyan
$analysis = Invoke-RestMethod `
    -Uri "$BaseUrl/analyze-transaction" `
    -Method Post `
    -ContentType "application/json; charset=utf-8" `
    -Body ($analysisBody | ConvertTo-Json -Depth 10)

$analysis | Format-List

Write-Host ""
Write-Host "Testing /chat/ask..." -ForegroundColor Cyan
$chatBody = @{
    context = $context
    existingAnalysis = $analysis.explanation
    question = "What should the admin check first for this transaction?"
    modelName = "gemini-3.1-flash-lite"
} | ConvertTo-Json -Depth 10

Invoke-RestMethod `
    -Uri "$BaseUrl/chat/ask" `
    -Method Post `
    -ContentType "application/json; charset=utf-8" `
    -Body $chatBody | Format-List
