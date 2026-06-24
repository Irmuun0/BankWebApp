# Run from the BankWebApp folder:
# .\tools\test-gemini-api.ps1
#
# Optional examples:
# .\tools\test-gemini-api.ps1 -Model "gemini-2.5-flash"
# .\tools\test-gemini-api.ps1 -Prompt "Монгол хэлээр нэг өгүүлбэрээр хариул."
# .\tools\test-gemini-api.ps1 -ApiKey "YOUR_API_KEY_HERE"

# other models: "models/gemma-4-26b-a4b-it", "models/gemma-4-31b-it"

param(
    [string]$Model = "gemini-3.1-flash-lite",
    [string]$Prompt = "Сайн уу. Зөвхөн Монгол кириллээр нэг өгүүлбэр хариул.",
    [string]$ApiKey = $env:GEMINI_API_KEY,
    [int]$TimeoutSec = 120
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ApiKey)) {
    Write-Host "ERROR: GEMINI_API_KEY environment variable олдсонгүй." -ForegroundColor Red
    Write-Host "Set temporary key for this terminal:" -ForegroundColor Yellow
    Write-Host '$env:GEMINI_API_KEY = "YOUR_API_KEY_HERE"'
    exit 1
}

$uri = "https://generativelanguage.googleapis.com/v1beta/models/$($Model):generateContent"
$body = @{
    contents = @(
        @{
            role = "user"
            parts = @(
                @{
                    text = $Prompt
                }
            )
        }
    )
    generationConfig = @{
        temperature = 0.2
        maxOutputTokens = 300
    }
} | ConvertTo-Json -Depth 10

Write-Host "Testing Gemini API..." -ForegroundColor Cyan
Write-Host "Model: $Model"
Write-Host "URL: $uri"
Write-Host ""

try {
    $response = Invoke-RestMethod `
        -Method Post `
        -Uri $uri `
        -Headers @{ "x-goog-api-key" = $ApiKey } `
        -ContentType "application/json; charset=utf-8" `
        -Body $body `
        -TimeoutSec $TimeoutSec

    $text = $response.candidates[0].content.parts[0].text

    Write-Host "SUCCESS: Gemini API is working." -ForegroundColor Green
    Write-Host ""
    Write-Host "Response:"
    Write-Host $text
    exit 0
}
catch {
    Write-Host "FAILED: Gemini API call failed." -ForegroundColor Red

    if ($_.Exception.Response -ne $null) {
        $statusCode = [int]$_.Exception.Response.StatusCode
        $statusDescription = $_.Exception.Response.StatusDescription
        Write-Host "HTTP Status: $statusCode $statusDescription" -ForegroundColor Yellow

        try {
            $stream = $_.Exception.Response.GetResponseStream()
            $reader = New-Object System.IO.StreamReader($stream)
            $errorBody = $reader.ReadToEnd()
            if (-not [string]::IsNullOrWhiteSpace($errorBody)) {
                Write-Host ""
                Write-Host "Error body:"
                Write-Host $errorBody
            }
        }
        catch {
            Write-Host "Error body reading not possible: $($_.Exception.Message)"
        }

        Write-Host ""
        switch ($statusCode) {
            400 { Write-Host "Hint: Request body эсвэл model нэр буруу байж болно." -ForegroundColor Yellow }
            401 { Write-Host "Hint: API key буруу эсвэл хоосон байна." -ForegroundColor Yellow }
            403 { Write-Host "Hint: Project/API permission, API restriction, billing эрх шалга." -ForegroundColor Yellow }
            404 { Write-Host "Hint: Model нэр олдсонгүй. -Model `"gemini-3.1-flash-lite`" гэж турш." -ForegroundColor Yellow }
            429 { Write-Host "Hint: Quota/rate limit/billing/prepay асуудал. AI Studio дээр Prepay required эсэхийг шалга." -ForegroundColor Yellow }
            503 { Write-Host "Hint: Google талын high demand/unavailable. Түр хүлээгээд дахин оролд эсвэл -Model `"gemini-3.1-flash-lite`" ашигла." -ForegroundColor Yellow }
            default { Write-Host "Hint: Google API error body-г харж оношил." -ForegroundColor Yellow }
        }
    }
    else {
        Write-Host "Exception: $($_.Exception.GetType().Name) - $($_.Exception.Message)" -ForegroundColor Yellow
        Write-Host "Hint: Network, DNS, proxy/firewall эсвэл TimeoutSec асуудал байж болно." -ForegroundColor Yellow
    }

    exit 1
}
