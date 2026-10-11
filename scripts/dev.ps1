<#
.SYNOPSIS
  Starts the whole system on this machine: database, platform API and the three front ends.

.DESCRIPTION
  See docs/setup.md. Press Ctrl+C to stop the API and the front ends; the database container
  keeps running (stop it with: docker compose -f deploy/compose/docker-compose.yml down).

.PARAMETER NoWeb
  Start the database and the API only.
#>
[CmdletBinding()]
param([switch]$NoWeb)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$compose = Join-Path $root 'deploy/compose/docker-compose.yml'
$envFile = Join-Path $root 'deploy/compose/.env'

function Step($message) { Write-Host "`n==> $message" -ForegroundColor Cyan }

function Invoke-Checked {
    & $args[0] @($args | Select-Object -Skip 1)
    if ($LASTEXITCODE -ne 0) { throw "Command failed ($LASTEXITCODE): $($args -join ' ')" }
}

Step 'Configuration'
if (-not (Test-Path $envFile)) {
    Copy-Item (Join-Path $root 'deploy/compose/.env.example') $envFile
    Write-Host 'Created deploy/compose/.env from .env.example (sample values).'
}
Get-Content $envFile -Encoding UTF8 | Where-Object { $_ -match '^\s*[^#\s][^=]*=' } | ForEach-Object {
    $name, $value = $_ -split '=', 2
    Set-Item -Path "Env:$($name.Trim())" -Value $value
}

Step 'Database'
Invoke-Checked docker compose -f $compose --env-file $envFile up -d --wait sqlserver
Invoke-Checked docker compose -f $compose --env-file $envFile up -d mailpit
Invoke-Checked docker compose -f $compose --env-file $envFile --profile init run --rm sqlserver-init

Step 'Structure'
Invoke-Checked dotnet run --project (Join-Path $root 'src/server/tools/Scs.Migrator') -c Release

Step 'Made-up clinics'
Invoke-Checked dotnet run --project (Join-Path $root 'src/server/tools/Scs.Migrator') -c Release --no-build -- seed

$children = @()
try {
    Step 'Platform API'
    $env:ASPNETCORE_URLS = 'http://localhost:5080'
    $children += Start-Process dotnet -PassThru -NoNewWindow -ArgumentList @(
        'run', '--project', (Join-Path $root 'src/server/host/Scs.Api'), '-c', 'Release', '--no-launch-profile')

    if (-not $NoWeb) {
        Step 'Front ends'
        $web = Join-Path $root 'src/web'
        if (-not (Test-Path (Join-Path $web 'node_modules'))) {
            Push-Location $web
            try { Invoke-Checked npm ci } finally { Pop-Location }
        }
        foreach ($app in 'clinic', 'patient', 'ops') {
            $children += Start-Process npm.cmd -PassThru -NoNewWindow -WorkingDirectory $web `
                -ArgumentList @('run', "start:$app")
        }
    }

    Write-Host @"

Smart Clinic OS is starting. When the builds finish:

  Platform   http://localhost:5080/health/ready
  Version    http://localhost:5080/v1/meta/version
  Clinic     http://localhost:4201        (sign in as owner@noor.example)
  Patient    http://localhost:4202/noor   (also /shifa)
  Operators  http://localhost:4203
  Jobs       http://localhost:5080/jobs
  Mail inbox http://localhost:8025   (sign-in codes arrive here)
  API docs   http://localhost:5080/openapi/v1.json

Press Ctrl+C to stop.
"@ -ForegroundColor Green

    Wait-Process -Id ($children | ForEach-Object Id)
}
finally {
    foreach ($child in $children) {
        if (-not $child.HasExited) {
            # /T also stops what the process started (the dev servers' own children).
            taskkill /PID $child.Id /T /F 2>$null | Out-Null
        }
    }
}
