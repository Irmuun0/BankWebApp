param(
    [Parameter(Mandatory = $true)]
    [string]$ConnectionString,

    [string]$MigrationsPath = (Join-Path $PSScriptRoot "migrations"),

    [switch]$PlanOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $MigrationsPath)) {
    throw "Migrations path not found: $MigrationsPath"
}

Add-Type -AssemblyName System.Data

function New-Sha256 {
    param([Parameter(Mandatory = $true)][string]$Text)

    $bytes = [System.Text.Encoding]::UTF8.GetBytes($Text)
    $hash = [System.Security.Cryptography.SHA256]::Create().ComputeHash($bytes)
    return ([System.BitConverter]::ToString($hash)).Replace("-", "").ToLowerInvariant()
}

function Split-SqlBatches {
    param([Parameter(Mandatory = $true)][string]$Sql)

    return [System.Text.RegularExpressions.Regex]::Split($Sql, "(?im)^\s*GO\s*(?:--.*)?$")
}

function Invoke-SqlNonQuery {
    param(
        [Parameter(Mandatory = $true)][System.Data.SqlClient.SqlConnection]$Connection,
        [System.Data.SqlClient.SqlTransaction]$Transaction,
        [Parameter(Mandatory = $true)][string]$Sql
    )

    $command = $Connection.CreateCommand()
    $command.CommandText = $Sql
    $command.CommandTimeout = 120
    if ($null -ne $Transaction) {
        $command.Transaction = $Transaction
    }

    [void]$command.ExecuteNonQuery()
}

function Invoke-SqlScalar {
    param(
        [Parameter(Mandatory = $true)][System.Data.SqlClient.SqlConnection]$Connection,
        [Parameter(Mandatory = $true)][string]$Sql,
        [System.Data.SqlClient.SqlParameter[]]$Parameters = @()
    )

    $command = $Connection.CreateCommand()
    $command.CommandText = $Sql
    $command.CommandTimeout = 120
    foreach ($parameter in $Parameters) {
        [void]$command.Parameters.Add($parameter)
    }

    return $command.ExecuteScalar()
}

function New-SqlParameter {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        $Value
    )

    $parameter = New-Object System.Data.SqlClient.SqlParameter
    $parameter.ParameterName = $Name
    $parameter.Value = if ($null -eq $Value) { [DBNull]::Value } else { $Value }
    return $parameter
}

$trackingSql = @"
IF OBJECT_ID(N'dbo.schema_migrations', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.schema_migrations
    (
        migration_id NVARCHAR(200) NOT NULL,
        script_name NVARCHAR(260) NOT NULL,
        checksum CHAR(64) NOT NULL,
        applied_at DATETIME2(0) NOT NULL CONSTRAINT df_schema_migrations_applied_at DEFAULT CAST(SYSUTCDATETIME() AT TIME ZONE 'UTC' AT TIME ZONE 'Ulaanbaatar Standard Time' AS DATETIME2(0)),
        execution_ms INT NOT NULL,
        success BIT NOT NULL CONSTRAINT df_schema_migrations_success DEFAULT 1,
        error_message NVARCHAR(MAX) NULL,
        CONSTRAINT pk_schema_migrations PRIMARY KEY (migration_id)
    );
END
"@

$migrationFiles = @(Get-ChildItem -LiteralPath $MigrationsPath -Filter "*.sql" -File |
    Sort-Object Name)

if ($migrationFiles.Count -eq 0) {
    Write-Host "No migration files found in $MigrationsPath"
    exit 0
}

$connection = New-Object System.Data.SqlClient.SqlConnection $ConnectionString
$connection.Open()

try {
    Invoke-SqlNonQuery -Connection $connection -Sql $trackingSql

    foreach ($file in $migrationFiles) {
        $migrationId = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)
        $sql = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
        $checksum = New-Sha256 -Text $sql

        $existingChecksum = Invoke-SqlScalar `
            -Connection $connection `
            -Sql "SELECT checksum FROM dbo.schema_migrations WHERE migration_id = @migration_id;" `
            -Parameters @((New-SqlParameter -Name "@migration_id" -Value $migrationId))

        if ($null -ne $existingChecksum -and $existingChecksum -ne [DBNull]::Value) {
            if ([string]$existingChecksum -ne $checksum) {
                throw "Applied migration checksum mismatch: $migrationId. Do not edit applied migrations; create a new migration instead."
            }

            Write-Host "Skipping applied migration: $($file.Name)"
            continue
        }

        if ($PlanOnly) {
            Write-Host "Would apply migration: $($file.Name)"
            continue
        }

        Write-Host "Applying migration: $($file.Name)"
        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        $transaction = $connection.BeginTransaction()

        try {
            foreach ($batch in (Split-SqlBatches -Sql $sql)) {
                if (-not [string]::IsNullOrWhiteSpace($batch)) {
                    Invoke-SqlNonQuery -Connection $connection -Transaction $transaction -Sql $batch
                }
            }

            $insert = $connection.CreateCommand()
            $insert.Transaction = $transaction
            $insert.CommandText = @"
INSERT INTO dbo.schema_migrations
    (migration_id, script_name, checksum, execution_ms, success)
VALUES
    (@migration_id, @script_name, @checksum, @execution_ms, 1);
"@
            [void]$insert.Parameters.Add((New-SqlParameter -Name "@migration_id" -Value $migrationId))
            [void]$insert.Parameters.Add((New-SqlParameter -Name "@script_name" -Value $file.Name))
            [void]$insert.Parameters.Add((New-SqlParameter -Name "@checksum" -Value $checksum))
            [void]$insert.Parameters.Add((New-SqlParameter -Name "@execution_ms" -Value ([int]$stopwatch.ElapsedMilliseconds)))
            [void]$insert.ExecuteNonQuery()

            $transaction.Commit()
            Write-Host "Applied: $($file.Name)"
        }
        catch {
            $transaction.Rollback()
            throw
        }
        finally {
            $stopwatch.Stop()
        }
    }
}
finally {
    $connection.Dispose()
}
