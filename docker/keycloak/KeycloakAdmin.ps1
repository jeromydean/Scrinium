# Shared Keycloak Admin REST helpers for local dev scripts.
# Uses curl.exe so self-signed localhost certs work on Windows PowerShell 5.1+.

function Write-KeycloakJsonFile {
  param(
    [Parameter(Mandatory = $true)]
    [string] $Path,

    [Parameter(Mandatory = $true)]
    [string] $Json
  )

  # Keycloak rejects JSON with a UTF-8 BOM (PowerShell Set-Content -Encoding UTF8 adds one).
  [System.IO.File]::WriteAllText($Path, $Json, [System.Text.UTF8Encoding]::new($false))
}

function Get-KeycloakEnv {
  param(
    [string] $EnvFile
  )

  $values = @{
    BaseUrl           = "https://localhost:8443"
    Realm             = "scrinium"
    AdminUser         = "admin"
    AdminPassword     = "secret"
    DevUser           = "devuser"
    DevPassword       = "devpass"
    ApiClientId       = "scrinium-api"
    DesktopClientId   = "scrinium-avalonia"
  }

  if (-not (Test-Path $EnvFile)) {
    return $values
  }

  Get-Content $EnvFile | ForEach-Object {
    $line = $_.Trim()
    if ($line -eq "" -or $line.StartsWith("#")) { return }

    $parts = $line -split "=", 2
    if ($parts.Count -ne 2) { return }

    $name = $parts[0].Trim()
    $value = $parts[1].Trim()
    switch ($name) {
      "KEYCLOAK_BASE_URL" { $values.BaseUrl = $value }
      "KEYCLOAK_REALM" { $values.Realm = $value }
      "KEYCLOAK_ADMIN" { $values.AdminUser = $value }
      "KEYCLOAK_ADMIN_PASSWORD" { $values.AdminPassword = $value }
      "SCRINIUM_DEV_USER" { $values.DevUser = $value }
      "SCRINIUM_DEV_PASSWORD" { $values.DevPassword = $value }
      "SCRINIUM_API_CLIENT_ID" { $values.ApiClientId = $value }
      "SCRINIUM_DESKTOP_CLIENT_ID" { $values.DesktopClientId = $value }
    }
  }

  return $values
}

function Invoke-KeycloakCurl {
  param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("GET", "POST", "PUT", "DELETE")]
    [string] $Method,

    [Parameter(Mandatory = $true)]
    [string] $Url,

    [string] $AccessToken,

    [string] $BodyFile,

    [hashtable] $Form,

    [switch] $IncludeHeaders
  )

  $args = @("-sk", "-f", "-X", $Method, $Url)
  if ($AccessToken) {
    $args += @("-H", "Authorization: Bearer $AccessToken")
  }

  if ($BodyFile) {
    $args += @("-H", "Content-Type: application/json", "--data-binary", "@$BodyFile")
  }
  elseif ($Form) {
    foreach ($entry in $Form.GetEnumerator()) {
      $args += @("-d", "$($entry.Key)=$($entry.Value)")
    }
  }

  if ($IncludeHeaders) {
    $args += @("-D", "-", "-o", "-")
  }

  $output = & curl.exe @args 2>&1
  if ($LASTEXITCODE -ne 0) {
    throw "curl failed ($Method $Url): $output"
  }

  return ($output -join "`n")
}

function Get-KeycloakAdminToken {
  param(
    [string] $BaseUrl,
    [string] $AdminUser,
    [string] $AdminPassword
  )

  $response = Invoke-KeycloakCurl -Method POST -Url "$BaseUrl/realms/master/protocol/openid-connect/token" -Form @{
    grant_type = "password"
    client_id  = "admin-cli"
    username   = $AdminUser
    password   = $AdminPassword
  }

  $token = ($response | ConvertFrom-Json).access_token
  if (-not $token) {
    throw "Failed to obtain Keycloak admin token. Check KEYCLOAK_ADMIN credentials and that Keycloak is running."
  }

  return $token
}

function Test-KeycloakRealm {
  param(
    [string] $BaseUrl,
    [string] $Realm,
    [string] $AccessToken
  )

  try {
    Invoke-KeycloakCurl -Method GET -Url "$BaseUrl/admin/realms/$Realm" -AccessToken $AccessToken | Out-Null
    return $true
  }
  catch {
    return $false
  }
}

function New-KeycloakRealm {
  param(
    [string] $BaseUrl,
    [string] $Realm,
    [string] $AccessToken
  )

  $payload = @{
    realm         = $Realm
    enabled       = $true
    displayName   = "Scrinium"
    loginWithEmailAllowed = $true
    registrationAllowed   = $false
    resetPasswordAllowed  = $true
    sslRequired   = "external"
  } | ConvertTo-Json -Depth 5

  $tempFile = [System.IO.Path]::GetTempFileName()
  try {
    Write-KeycloakJsonFile -Path $tempFile -Json $payload
    Invoke-KeycloakCurl -Method POST -Url "$BaseUrl/admin/realms" -AccessToken $AccessToken -BodyFile $tempFile | Out-Null
  }
  finally {
    Remove-Item $tempFile -Force -ErrorAction SilentlyContinue
  }
}

function Get-KeycloakClient {
  param(
    [string] $BaseUrl,
    [string] $Realm,
    [string] $AccessToken,
    [string] $ClientId
  )

  $encoded = [uri]::EscapeDataString($ClientId)
  $response = Invoke-KeycloakCurl -Method GET -Url "$BaseUrl/admin/realms/$Realm/clients?clientId=$encoded" -AccessToken $AccessToken
  $clients = @($response | ConvertFrom-Json)
  if ($clients.Count -gt 0) {
    return $clients[0]
  }

  return $null
}

function New-KeycloakClient {
  param(
    [string] $BaseUrl,
    [string] $Realm,
    [string] $AccessToken,
    [hashtable] $Client
  )

  $tempFile = [System.IO.Path]::GetTempFileName()
  try {
    Write-KeycloakJsonFile -Path $tempFile -Json ($Client | ConvertTo-Json -Depth 8)
    Invoke-KeycloakCurl -Method POST -Url "$BaseUrl/admin/realms/$Realm/clients" -AccessToken $AccessToken -BodyFile $tempFile | Out-Null
  }
  finally {
    Remove-Item $tempFile -Force -ErrorAction SilentlyContinue
  }
}

function Set-KeycloakClient {
  param(
    [string] $BaseUrl,
    [string] $Realm,
    [string] $AccessToken,
    [string] $InternalId,
    [hashtable] $Client
  )

  $tempFile = [System.IO.Path]::GetTempFileName()
  try {
    Write-KeycloakJsonFile -Path $tempFile -Json ($Client | ConvertTo-Json -Depth 8)
    Invoke-KeycloakCurl -Method PUT -Url "$BaseUrl/admin/realms/$Realm/clients/$InternalId" -AccessToken $AccessToken -BodyFile $tempFile | Out-Null
  }
  finally {
    Remove-Item $tempFile -Force -ErrorAction SilentlyContinue
  }
}

function Add-KeycloakAudienceMapper {
  param(
    [string] $BaseUrl,
    [string] $Realm,
    [string] $AccessToken,
    [string] $ClientInternalId,
    [string] $Audience
  )

  $existing = Invoke-KeycloakCurl -Method GET -Url "$BaseUrl/admin/realms/$Realm/clients/$ClientInternalId/protocol-mappers/models" -AccessToken $AccessToken
  $mappers = $existing | ConvertFrom-Json
  if ($mappers | Where-Object { $_.name -eq "scrinium-api-audience" }) {
    return
  }

  $mapper = @{
    name           = "scrinium-api-audience"
    protocol       = "openid-connect"
    protocolMapper = "oidc-audience-mapper"
    config         = @{
      "included.client.audience" = $Audience
      "id.token.claim"             = "false"
      "access.token.claim"         = "true"
    }
  }

  $tempFile = [System.IO.Path]::GetTempFileName()
  try {
    Write-KeycloakJsonFile -Path $tempFile -Json ($mapper | ConvertTo-Json -Depth 8)
    Invoke-KeycloakCurl -Method POST -Url "$BaseUrl/admin/realms/$Realm/clients/$ClientInternalId/protocol-mappers/models" -AccessToken $AccessToken -BodyFile $tempFile | Out-Null
  }
  finally {
    Remove-Item $tempFile -Force -ErrorAction SilentlyContinue
  }
}

function Get-KeycloakClientSecret {
  param(
    [string] $BaseUrl,
    [string] $Realm,
    [string] $AccessToken,
    [string] $ClientInternalId
  )

  $response = Invoke-KeycloakCurl -Method GET -Url "$BaseUrl/admin/realms/$Realm/clients/$ClientInternalId/client-secret" -AccessToken $AccessToken
  return ($response | ConvertFrom-Json).value
}

function Reset-KeycloakClientSecret {
  param(
    [string] $BaseUrl,
    [string] $Realm,
    [string] $AccessToken,
    [string] $ClientInternalId
  )

  $response = Invoke-KeycloakCurl -Method POST -Url "$BaseUrl/admin/realms/$Realm/clients/$ClientInternalId/client-secret" -AccessToken $AccessToken
  return ($response | ConvertFrom-Json).value
}

function Add-KeycloakRealmRole {
  param(
    [string] $BaseUrl,
    [string] $Realm,
    [string] $AccessToken,
    [string] $RoleName
  )

  $existing = Invoke-KeycloakCurl -Method GET -Url "$BaseUrl/admin/realms/$Realm/roles/$RoleName" -AccessToken $AccessToken -ErrorAction SilentlyContinue
  if ($LASTEXITCODE -eq 0 -and $existing) {
    return
  }

  $payload = @{ name = $RoleName } | ConvertTo-Json
  $tempFile = [System.IO.Path]::GetTempFileName()
  try {
    Write-KeycloakJsonFile -Path $tempFile -Json $payload
    Invoke-KeycloakCurl -Method POST -Url "$BaseUrl/admin/realms/$Realm/roles" -AccessToken $AccessToken -BodyFile $tempFile | Out-Null
  }
  catch {
    # Role may already exist on race/re-run.
  }
  finally {
    Remove-Item $tempFile -Force -ErrorAction SilentlyContinue
  }
}

function Get-KeycloakUser {
  param(
    [string] $BaseUrl,
    [string] $Realm,
    [string] $AccessToken,
    [string] $Username
  )

  $encoded = [uri]::EscapeDataString($Username)
  $response = Invoke-KeycloakCurl -Method GET -Url "$BaseUrl/admin/realms/$Realm/users?username=$encoded&exact=true" -AccessToken $AccessToken
  $users = @($response | ConvertFrom-Json)
  if ($users.Count -gt 0) {
    return $users[0]
  }

  return $null
}

function New-KeycloakUser {
  param(
    [string] $BaseUrl,
    [string] $Realm,
    [string] $AccessToken,
    [string] $Username,
    [string] $Password,
    [string] $Email,
    [string] $FirstName,
    [string] $LastName
  )

  $payload = @{
    username      = $Username
    enabled       = $true
    email         = $Email
    emailVerified = $true
    firstName     = $FirstName
    lastName      = $LastName
    credentials   = @(
      @{
        type      = "password"
        value     = $Password
        temporary = $false
      }
    )
  } | ConvertTo-Json -Depth 5

  $tempFile = [System.IO.Path]::GetTempFileName()
  try {
    Write-KeycloakJsonFile -Path $tempFile -Json $payload
    Invoke-KeycloakCurl -Method POST -Url "$BaseUrl/admin/realms/$Realm/users" -AccessToken $AccessToken -BodyFile $tempFile | Out-Null
  }
  finally {
    Remove-Item $tempFile -Force -ErrorAction SilentlyContinue
  }
}

function Set-KeycloakUserPassword {
  param(
    [string] $BaseUrl,
    [string] $Realm,
    [string] $AccessToken,
    [string] $UserId,
    [string] $Password
  )

  $payload = @{
    type      = "password"
    value     = $Password
    temporary = $false
  } | ConvertTo-Json

  $tempFile = [System.IO.Path]::GetTempFileName()
  try {
    Write-KeycloakJsonFile -Path $tempFile -Json $payload
    Invoke-KeycloakCurl -Method PUT -Url "$BaseUrl/admin/realms/$Realm/users/$UserId/reset-password" -AccessToken $AccessToken -BodyFile $tempFile | Out-Null
  }
  finally {
    Remove-Item $tempFile -Force -ErrorAction SilentlyContinue
  }
}

function Add-KeycloakUserRealmRoles {
  param(
    [string] $BaseUrl,
    [string] $Realm,
    [string] $AccessToken,
    [string] $UserId,
    [string[]] $RoleNames
  )

  $roles = @()
  foreach ($roleName in $RoleNames) {
    $roleJson = Invoke-KeycloakCurl -Method GET -Url "$BaseUrl/admin/realms/$Realm/roles/$roleName" -AccessToken $AccessToken
    $roles += ($roleJson | ConvertFrom-Json)
  }

  if ($roles.Count -eq 0) {
    return
  }

  $tempFile = [System.IO.Path]::GetTempFileName()
  try {
    Write-KeycloakJsonFile -Path $tempFile -Json ($roles | ConvertTo-Json -Depth 5)
    Invoke-KeycloakCurl -Method POST -Url "$BaseUrl/admin/realms/$Realm/users/$UserId/role-mappings/realm" -AccessToken $AccessToken -BodyFile $tempFile | Out-Null
  }
  finally {
    Remove-Item $tempFile -Force -ErrorAction SilentlyContinue
  }
}

function Set-EnvValue {
  param(
    [string] $EnvFile,
    [string] $Name,
    [string] $Value
  )

  $lines = @()
  $found = $false
  if (Test-Path $EnvFile) {
    $lines = Get-Content $EnvFile
    for ($i = 0; $i -lt $lines.Count; $i++) {
      if ($lines[$i] -match "^\s*$([regex]::Escape($Name))\s*=") {
        $lines[$i] = "$Name=$Value"
        $found = $true
        break
      }
    }
  }

  if (-not $found) {
    if ($lines.Count -gt 0 -and $lines[-1].Trim() -ne "") {
      $lines += ""
    }
    $lines += "# Keycloak dev bootstrap (setup-keycloak.ps1)"
    $lines += "$Name=$Value"
  }

  Set-Content -Path $EnvFile -Value $lines -Encoding UTF8
}
