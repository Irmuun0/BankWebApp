param(
    [string]$BaseUrl = "http://localhost:8000"
)

$ErrorActionPreference = "Stop"

$cases = @(
    @{
        name = "normal-transfer"
        body = @{
            transactionId = 1
            senderUserId = 10
            amount = 100000
            sourceCurrency = "MNT"
            creditedAmount = 100000
            targetCurrency = "MNT"
            isCrossCurrency = $false
            description = "Хэрэглээний төлбөр"
            createdHour = 14
            senderAverageAmountLast30Days = 80000
            senderMaxAmountLast30Days = 150000
            senderTransactionCountLast24Hours = 1
            smallTransactionCountLast24Hours = 0
            smallTransactionTotalLast24Hours = 0
            distinctReceiverCountLast24Hours = 1
            distinctSenderCountToReceiverLast24Hours = 1
            recentInboundAmountLast30Minutes = 0
            senderAccountAgeDays = 90
            senderDaysSinceLastTransaction = 1
        }
    },
    @{
        name = "structuring"
        body = @{
            transactionId = 2
            senderUserId = 10
            amount = 500000
            sourceCurrency = "MNT"
            creditedAmount = 500000
            targetCurrency = "MNT"
            isCrossCurrency = $false
            description = "шилжүүлэг"
            createdHour = 14
            senderAverageAmountLast30Days = 200000
            senderMaxAmountLast30Days = 500000
            senderTransactionCountLast24Hours = 10
            smallTransactionCountLast24Hours = 10
            smallTransactionTotalLast24Hours = 5000000
            distinctReceiverCountLast24Hours = 2
            distinctSenderCountToReceiverLast24Hours = 1
            recentInboundAmountLast30Minutes = 0
            senderAccountAgeDays = 90
            senderDaysSinceLastTransaction = 1
        }
    },
    @{
        name = "rapid-in-out"
        body = @{
            transactionId = 3
            senderUserId = 11
            amount = 900000
            sourceCurrency = "MNT"
            creditedAmount = 900000
            targetCurrency = "MNT"
            isCrossCurrency = $false
            description = "дамжуулж өг"
            createdHour = 15
            senderAverageAmountLast30Days = 100000
            senderMaxAmountLast30Days = 200000
            senderTransactionCountLast24Hours = 2
            smallTransactionCountLast24Hours = 0
            smallTransactionTotalLast24Hours = 0
            distinctReceiverCountLast24Hours = 1
            distinctSenderCountToReceiverLast24Hours = 1
            recentInboundAmountLast30Minutes = 1000000
            senderAccountAgeDays = 45
            senderDaysSinceLastTransaction = 1
        }
    }
)

Invoke-RestMethod -Uri "$BaseUrl/health" -Method Get | Format-List

foreach ($case in $cases) {
    $json = $case.body | ConvertTo-Json -Depth 5
    $bodyBytes = [System.Text.Encoding]::UTF8.GetBytes($json)
    $result = Invoke-RestMethod `
        -Uri "$BaseUrl/detect-suspicious" `
        -Method Post `
        -ContentType "application/json; charset=utf-8" `
        -Body $bodyBytes

    Write-Host "`n[$($case.name)]"
    $result | Format-List
}
