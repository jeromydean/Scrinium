$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
. "$PSScriptRoot\Compose.ps1"

if (-not (Test-Path ".env")) {
  Copy-Item ".env.example" ".env"
  Write-Host "Created .env from .env.example"
}

$certPath = Join-Path $PSScriptRoot "certs\keycloak.pfx"
$solrCertPath = Join-Path $PSScriptRoot "certs\solr.pfx"
if (-not (Test-Path $certPath) -or -not (Test-Path $solrCertPath)) {
  Write-Host "TLS certificate(s) not found (Keycloak and/or Solr)" -ForegroundColor Yellow
  Write-Host "Running generate-certificates.ps1 (requires Administrator)..." -ForegroundColor Cyan
  & "$PSScriptRoot\generate-certificates.ps1"
  if (-not (Test-Path $certPath) -or -not (Test-Path $solrCertPath)) {
    throw "Certificate generation did not produce required files. Run .\docker\generate-certificates.ps1 as Administrator, then try again."
  }
}

# Ensure host-visible data directories exist (see SCRINIUM_DATA_DIR in .env).
$dataDir = "C:\ProgramData\Scrinium"
if (Test-Path ".env") {
  $dataDirLine = Get-Content ".env" | Where-Object { $_ -match '^\s*SCRINIUM_DATA_DIR\s*=' } | Select-Object -First 1
  if ($dataDirLine -match '=\s*(.+)$') {
    $dataDir = $Matches[1].Trim().Trim('"').Trim("'") -replace '/', '\'
  }
}
$postgresDataDir = Join-Path $dataDir "postgres"
$solrDataDir = Join-Path $dataDir "solr"
$minioDataDir = Join-Path $dataDir "minio"
foreach ($dir in @($solrDataDir, $minioDataDir)) {
  New-Item -ItemType Directory -Force -Path $dir | Out-Null
}

$compose = Get-ComposeCommand
Write-Host "Using $($compose[0]) compose" -ForegroundColor Cyan
Invoke-Compose up -d @args

Write-Host ""
Write-Host "Services (see docker/README.md for details):" -ForegroundColor Green
Write-Host "  Keycloak admin     https://localhost:8443/admin"
Write-Host "  Keycloak realm     https://localhost:8443/realms/scrinium"
Write-Host "  pgAdmin            http://localhost:5050"
Write-Host "  Solr               https://localhost:8983/solr/"
Write-Host "  PostgreSQL         localhost:5432  (scrinium, keycloak)"
Write-Host "  MinIO API/console  http://localhost:9000 / http://localhost:9001"
Write-Host "  Data root (host)   $dataDir"
Write-Host "    solr             $solrDataDir"
Write-Host "    minio            $minioDataDir"
Write-Host "    postgres         named volume scrinium-postgres-data (NTFS bind mounts break initdb)"
Write-Host "  Scrinium API       http://localhost:5243/health  (dotnet run, not in compose)"
Write-Host ""
Write-Host "Next: bootstrap Keycloak realm, clients, and dev user (idempotent):" -ForegroundColor Yellow
Write-Host "  .\docker\setup-keycloak.ps1"
