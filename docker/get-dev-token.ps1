# Obtain a dev access token from the local Keycloak scrinium realm.
# Reads credentials from .env (populated by setup-keycloak.ps1).
#
# Usage:
#   .\docker\get-dev-token.ps1
#   .\docker\get-dev-token.ps1 -Quiet    # token only, for scripting

[CmdletBinding()]
param(
  [switch] $Quiet
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

. "$PSScriptRoot\keycloak\KeycloakAdmin.ps1"

$envFile = Join-Path $root ".env"
$config = Get-KeycloakEnv -EnvFile $envFile

$clientSecret = $null
if (Test-Path $envFile) {
  Get-Content $envFile | ForEach-Object {
    if ($_ -match '^\s*SCRINIUM_API_CLIENT_SECRET\s*=\s*(.+)\s*$') {
      $clientSecret = $Matches[1].Trim()
    }
  }
}

if (-not $clientSecret) {
  throw "SCRINIUM_API_CLIENT_SECRET not found in .env. Run .\docker\setup-keycloak.ps1 first."
}

$form = @{
  grant_type    = "password"
  client_id     = $config.ApiClientId
  client_secret = $clientSecret
  username      = $config.DevUser
  password      = $config.DevPassword
  scope         = "openid profile email"
}

$response = Invoke-KeycloakCurl -Method POST `
  -Url "$($config.BaseUrl)/realms/$($config.Realm)/protocol/openid-connect/token" `
  -Form $form

$json = $response | ConvertFrom-Json
if (-not $json.access_token) {
  throw "Token endpoint did not return access_token. Run .\docker\setup-keycloak.ps1 and verify Keycloak is up."
}

if ($Quiet) {
  Write-Output $json.access_token
}
else {
  Write-Host "Access token (expires in $($json.expires_in)s):" -ForegroundColor Green
  Write-Host $json.access_token
  Write-Host ""
  Write-Host "Example:" -ForegroundColor Cyan
  Write-Host "  curl.exe -sk http://localhost:5243/api/documents/<id>/status -H ""Authorization: Bearer $($json.access_token)"""
}
