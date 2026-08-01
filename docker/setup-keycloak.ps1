# Idempotent Keycloak bootstrap for local Scrinium development.
# Safe to re-run: creates or updates realm, clients, roles, and dev user.
#
# Usage (from repo root):
#   .\docker\setup-keycloak.ps1
#   .\docker\setup-keycloak.ps1 -PrintToken
#
# Prerequisites:
#   - Keycloak container running (.\docker\up.ps1)
#   - TLS cert generated (.\docker\generate-certificates.ps1)

[CmdletBinding()]
param(
  [switch] $PrintToken,
  [int] $WaitSeconds = 180
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

. "$PSScriptRoot\keycloak\KeycloakAdmin.ps1"

$envFile = Join-Path $root ".env"
$config = Get-KeycloakEnv -EnvFile $envFile

function Wait-KeycloakReady {
  param(
    [string] $BaseUrl,
    [int] $TimeoutSeconds
  )

  $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
  Write-Host "Waiting for Keycloak at $BaseUrl ..." -ForegroundColor Cyan

  while ((Get-Date) -lt $deadline) {
    try {
      Invoke-KeycloakCurl -Method GET -Url "$BaseUrl/realms/master" | Out-Null
      Write-Host "Keycloak is ready." -ForegroundColor Green
      return
    }
    catch {
      Start-Sleep -Seconds 3
    }
  }

  throw "Keycloak did not become ready within $TimeoutSeconds seconds. Run .\docker\up.ps1 first."
}

function Ensure-KeycloakRealmRole {
  param(
    [string] $BaseUrl,
    [string] $Realm,
    [string] $AccessToken,
    [string] $RoleName,
    [string] $Description
  )

  try {
    Invoke-KeycloakCurl -Method GET -Url "$BaseUrl/admin/realms/$Realm/roles/$RoleName" -AccessToken $AccessToken | Out-Null
    return "exists"
  }
  catch {
    $payload = @{
      name        = $RoleName
      description = $Description
    } | ConvertTo-Json

    $tempFile = [System.IO.Path]::GetTempFileName()
    try {
      Write-KeycloakJsonFile -Path $tempFile -Json $payload
      Invoke-KeycloakCurl -Method POST -Url "$BaseUrl/admin/realms/$Realm/roles" -AccessToken $AccessToken -BodyFile $tempFile | Out-Null
      return "created"
    }
    finally {
      Remove-Item $tempFile -Force -ErrorAction SilentlyContinue
    }
  }
}

function Merge-Hashtable {
  param(
    [hashtable] $Target,
    [hashtable] $Updates
  )

  foreach ($key in $Updates.Keys) {
    $Target[$key] = $Updates[$key]
  }

  return $Target
}

function Get-KeycloakClientWithRetry {
  param(
    [string] $BaseUrl,
    [string] $Realm,
    [string] $AccessToken,
    [string] $ClientId,
    [int] $MaxAttempts = 5
  )

  for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
    $client = Get-KeycloakClient -BaseUrl $BaseUrl -Realm $Realm -AccessToken $AccessToken -ClientId $ClientId
    if ($null -ne $client) {
      return $client
    }

    Start-Sleep -Milliseconds 500
  }

  return $null
}

function Ensure-KeycloakOpenIdClient {
  param(
    [string] $BaseUrl,
    [string] $Realm,
    [string] $AccessToken,
    [hashtable] $Desired
  )

  $clientId = $Desired.clientId
  $existing = Get-KeycloakClient -BaseUrl $BaseUrl -Realm $Realm -AccessToken $AccessToken -ClientId $clientId

  if ($null -eq $existing) {
    try {
      New-KeycloakClient -BaseUrl $BaseUrl -Realm $Realm -AccessToken $AccessToken -Client $Desired
    }
    catch {
      # Another run may have created the client between GET and POST.
    }

    $existing = Get-KeycloakClientWithRetry -BaseUrl $BaseUrl -Realm $Realm -AccessToken $AccessToken -ClientId $clientId
    if ($null -eq $existing) {
      throw "Client '$clientId' was not found after create."
    }

    return "created"
  }

  $merged = @{}
  foreach ($prop in $existing.PSObject.Properties) {
    if ($null -ne $prop.Value) {
      $merged[$prop.Name] = $prop.Value
    }
  }

  $merged = Merge-Hashtable -Target $merged -Updates $Desired
  Set-KeycloakClient -BaseUrl $BaseUrl -Realm $Realm -AccessToken $AccessToken -InternalId $existing.id -Client $merged
  return "updated"
}

function Ensure-KeycloakDevUser {
  param(
    [string] $BaseUrl,
    [string] $Realm,
    [string] $AccessToken,
    [string] $Username,
    [string] $Password,
    [string] $Email,
    [string] $FirstName,
    [string] $LastName,
    [string[]] $RoleNames
  )

  $user = Get-KeycloakUser -BaseUrl $BaseUrl -Realm $Realm -AccessToken $AccessToken -Username $Username
  if ($null -eq $user) {
    New-KeycloakUser -BaseUrl $BaseUrl -Realm $Realm -AccessToken $AccessToken `
      -Username $Username -Password $Password -Email $Email -FirstName $FirstName -LastName $LastName
    $user = Get-KeycloakUser -BaseUrl $BaseUrl -Realm $Realm -AccessToken $AccessToken -Username $Username
    $created = $true
  }
  else {
    Set-KeycloakUserPassword -BaseUrl $BaseUrl -Realm $Realm -AccessToken $AccessToken -UserId $user.id -Password $Password
    $created = $false
  }

  if ($null -eq $user) {
    throw "Failed to locate dev user '$Username' after create/update."
  }

  Add-KeycloakUserRealmRoles -BaseUrl $BaseUrl -Realm $Realm -AccessToken $AccessToken -UserId $user.id -RoleNames $RoleNames

  if ($created) { return "created" }
  return "updated"
}

function Get-KeycloakPasswordToken {
  param(
    [string] $BaseUrl,
    [string] $Realm,
    [string] $ClientId,
    [string] $ClientSecret,
    [string] $Username,
    [string] $Password
  )

  $form = @{
    grant_type = "password"
    client_id  = $ClientId
    username   = $Username
    password   = $Password
    scope      = "openid profile email"
  }

  if ($ClientSecret) {
    $form.client_secret = $ClientSecret
  }

  $response = Invoke-KeycloakCurl -Method POST -Url "$BaseUrl/realms/$Realm/protocol/openid-connect/token" -Form $form
  $token = ($response | ConvertFrom-Json).access_token
  if (-not $token) {
    throw "Failed to obtain access token for dev user '$Username'."
  }

  return $token
}

Wait-KeycloakReady -BaseUrl $config.BaseUrl -TimeoutSeconds $WaitSeconds

$adminToken = Get-KeycloakAdminToken -BaseUrl $config.BaseUrl `
  -AdminUser $config.AdminUser -AdminPassword $config.AdminPassword

Write-Host ""
Write-Host "Realm: $($config.Realm)" -ForegroundColor Cyan

if (Test-KeycloakRealm -BaseUrl $config.BaseUrl -Realm $config.Realm -AccessToken $adminToken) {
  Write-Host "  realm already exists (ok)" -ForegroundColor DarkGray
}
else {
  New-KeycloakRealm -BaseUrl $config.BaseUrl -Realm $config.Realm -AccessToken $adminToken
  Write-Host "  realm created" -ForegroundColor Green
}

$realmRoles = @(
  @{ Name = "admin"; Description = "System administrator" },
  @{ Name = "manager"; Description = "Workflow and folder manager" },
  @{ Name = "user"; Description = "Standard user" },
  @{ Name = "document:read"; Description = "Read documents" },
  @{ Name = "document:write"; Description = "Create and update documents" },
  @{ Name = "document:delete"; Description = "Delete documents" },
  @{ Name = "workflow:execute"; Description = "Execute workflows" },
  @{ Name = "workflow:manage"; Description = "Manage workflow definitions" },
  @{ Name = "folder:manage"; Description = "Manage folders and permissions" }
)

Write-Host "Roles:" -ForegroundColor Cyan
foreach ($role in $realmRoles) {
  $result = Ensure-KeycloakRealmRole -BaseUrl $config.BaseUrl -Realm $config.Realm `
    -AccessToken $adminToken -RoleName $role.Name -Description $role.Description
  Write-Host "  $($role.Name) ($result)" -ForegroundColor DarkGray
}

$apiClientDesired = @{
  clientId                 = $config.ApiClientId
  name                     = "Scrinium API"
  description              = "Confidential client for API JWT validation and local dev password grant"
  enabled                  = $true
  protocol                 = "openid-connect"
  publicClient             = $false
  bearerOnly               = $false
  standardFlowEnabled      = $true
  directAccessGrantsEnabled = $true
  serviceAccountsEnabled   = $false
  fullScopeAllowed         = $true
  redirectUris             = @("http://localhost/*", "https://localhost/*")
  webOrigins               = @("+")
}

Write-Host "Clients:" -ForegroundColor Cyan
$apiResult = Ensure-KeycloakOpenIdClient -BaseUrl $config.BaseUrl -Realm $config.Realm `
  -AccessToken $adminToken -Desired $apiClientDesired
Write-Host "  $($config.ApiClientId) ($apiResult)" -ForegroundColor DarkGray

$apiClient = Get-KeycloakClientWithRetry -BaseUrl $config.BaseUrl -Realm $config.Realm `
  -AccessToken $adminToken -ClientId $config.ApiClientId
if ($null -eq $apiClient) {
  throw "API client '$($config.ApiClientId)' was not found after ensure."
}

Add-KeycloakAudienceMapper -BaseUrl $config.BaseUrl -Realm $config.Realm `
  -AccessToken $adminToken -ClientInternalId $apiClient.id -Audience $config.ApiClientId
Write-Host "  $($config.ApiClientId) audience mapper (ok)" -ForegroundColor DarkGray

$apiSecret = Get-KeycloakClientSecret -BaseUrl $config.BaseUrl -Realm $config.Realm `
  -AccessToken $adminToken -ClientInternalId $apiClient.id
if (-not $apiSecret) {
  $apiSecret = Reset-KeycloakClientSecret -BaseUrl $config.BaseUrl -Realm $config.Realm `
    -AccessToken $adminToken -ClientInternalId $apiClient.id
}

$desktopClientDesired = @{
  clientId                 = $config.DesktopClientId
  name                     = "Scrinium Avalonia"
  description              = "Public desktop client (Authorization Code + PKCE)"
  enabled                  = $true
  protocol                 = "openid-connect"
  publicClient             = $true
  bearerOnly               = $false
  standardFlowEnabled      = $true
  directAccessGrantsEnabled = $false
  implicitFlowEnabled      = $false
  serviceAccountsEnabled   = $false
  fullScopeAllowed         = $true
  redirectUris             = @(
    "http://127.0.0.1:*",
    "http://localhost:*",
    "scrinium://auth/callback"
  )
  webOrigins               = @("+")
  attributes               = @{
    "pkce.code.challenge.method" = "S256"
  }
}

$desktopResult = Ensure-KeycloakOpenIdClient -BaseUrl $config.BaseUrl -Realm $config.Realm `
  -AccessToken $adminToken -Desired $desktopClientDesired
Write-Host "  $($config.DesktopClientId) ($desktopResult)" -ForegroundColor DarkGray

$desktopClient = Get-KeycloakClientWithRetry -BaseUrl $config.BaseUrl -Realm $config.Realm `
  -AccessToken $adminToken -ClientId $config.DesktopClientId
if ($null -eq $desktopClient) {
  throw "Desktop client '$($config.DesktopClientId)' was not found after ensure."
}

Add-KeycloakAudienceMapper -BaseUrl $config.BaseUrl -Realm $config.Realm `
  -AccessToken $adminToken -ClientInternalId $desktopClient.id -Audience $config.ApiClientId
Write-Host "  $($config.DesktopClientId) audience mapper (ok)" -ForegroundColor DarkGray

Write-Host "Dev user:" -ForegroundColor Cyan
$userResult = Ensure-KeycloakDevUser -BaseUrl $config.BaseUrl -Realm $config.Realm `
  -AccessToken $adminToken -Username $config.DevUser -Password $config.DevPassword `
  -Email "$($config.DevUser)@local.dev" -FirstName "Dev" -LastName "User" `
  -RoleNames @("user", "document:read", "document:write")
Write-Host "  $($config.DevUser) ($userResult)" -ForegroundColor DarkGray

if (-not (Test-Path $envFile)) {
  Copy-Item (Join-Path $root ".env.example") $envFile
  Write-Host ""
  Write-Host "Created .env from .env.example" -ForegroundColor Yellow
}

Set-EnvValue -EnvFile $envFile -Name "KEYCLOAK_REALM" -Value $config.Realm
Set-EnvValue -EnvFile $envFile -Name "SCRINIUM_DEV_USER" -Value $config.DevUser
Set-EnvValue -EnvFile $envFile -Name "SCRINIUM_DEV_PASSWORD" -Value $config.DevPassword
Set-EnvValue -EnvFile $envFile -Name "SCRINIUM_API_CLIENT_ID" -Value $config.ApiClientId
Set-EnvValue -EnvFile $envFile -Name "SCRINIUM_API_CLIENT_SECRET" -Value $apiSecret
Set-EnvValue -EnvFile $envFile -Name "SCRINIUM_DESKTOP_CLIENT_ID" -Value $config.DesktopClientId

Write-Host ""
Write-Host "Keycloak bootstrap complete (idempotent)." -ForegroundColor Green
Write-Host ""
Write-Host "OIDC authority : $($config.BaseUrl)/realms/$($config.Realm)"
Write-Host "API audience   : $($config.ApiClientId)"
Write-Host "Dev user       : $($config.DevUser) / $($config.DevPassword)"
Write-Host "Client secret  : written to .env as SCRINIUM_API_CLIENT_SECRET"
Write-Host ""
Write-Host "Get a dev token:" -ForegroundColor Cyan
Write-Host "  .\docker\get-dev-token.ps1"
Write-Host ""
Write-Host "Test ingestion:" -ForegroundColor Cyan
Write-Host "  `$token = .\docker\get-dev-token.ps1 -Quiet"
Write-Host "  curl.exe -sk -X POST http://localhost:5243/api/ingestion -H ""Authorization: Bearer `$token"" -F ""file=@your.pdf"""

if ($PrintToken) {
  Write-Host ""
  Write-Host "Access token:" -ForegroundColor Cyan
  $token = Get-KeycloakPasswordToken -BaseUrl $config.BaseUrl -Realm $config.Realm `
    -ClientId $config.ApiClientId -ClientSecret $apiSecret `
    -Username $config.DevUser -Password $config.DevPassword
  Write-Host $token
}
