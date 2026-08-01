# Generate self-signed certificates for Keycloak and Solr HTTPS in development.
# Run once before starting the stack. Requires Administrator (script will elevate if needed).
# Output: docker/certs/keycloak.pfx, docker/certs/solr.pfx, docker/certs/localhost.cer (password: password)

$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
  Write-Host "Not running as Administrator. Restarting with elevated privileges..." -ForegroundColor Yellow
  $scriptPath = $MyInvocation.MyCommand.Path
  Start-Process powershell.exe -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$scriptPath`"" -Verb RunAs
  exit
}

$hostname = "localhost"
$certPassword = "password"
$validityDays = 365
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptPath
New-Item -ItemType Directory -Force -Path "certs" | Out-Null

Write-Host "Generating self-signed certificate for $hostname..." -ForegroundColor Cyan

$cert = New-SelfSignedCertificate `
  -DnsName $hostname, "localhost", "127.0.0.1" `
  -CertStoreLocation "Cert:\LocalMachine\My" `
  -NotAfter (Get-Date).AddDays($validityDays) `
  -KeyAlgorithm RSA `
  -KeyLength 2048 `
  -HashAlgorithm SHA256 `
  -KeyUsage DigitalSignature, KeyEncipherment `
  -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.1") `
  -FriendlyName "Scrinium Dev TLS (localhost)"

Write-Host "Certificate created with thumbprint: $($cert.Thumbprint)" -ForegroundColor Green

Write-Host "Adding certificate to Trusted Root store..." -ForegroundColor Cyan
$store = New-Object System.Security.Cryptography.X509Certificates.X509Store("Root", "LocalMachine")
$store.Open("ReadWrite")
$store.Add($cert)
$store.Close()
Write-Host "Certificate trusted successfully!" -ForegroundColor Green

Write-Host "Removing certificate from Personal store..." -ForegroundColor Cyan
Remove-Item -Path "Cert:\LocalMachine\My\$($cert.Thumbprint)" -Force

$certPath = "certs\keycloak.pfx"
$solrCertPath = "certs\solr.pfx"
$securePassword = ConvertTo-SecureString -String $certPassword -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath $certPath -Password $securePassword | Out-Null
Copy-Item -Path $certPath -Destination $solrCertPath -Force
Export-Certificate -Cert $cert -FilePath "certs\localhost.cer" | Out-Null
Write-Host "Exported certificate to: $certPath" -ForegroundColor Green
Write-Host "Exported certificate to: $solrCertPath" -ForegroundColor Green
Write-Host "Exported certificate to: certs\localhost.cer (import into Firefox if needed)" -ForegroundColor Green
Write-Host "Keystore password: $certPassword (used by docker-compose)" -ForegroundColor Cyan
Write-Host ""
Write-Host "Windows: trusted in LocalMachine Root (Edge/Chrome)." -ForegroundColor Cyan
Write-Host "Firefox: set about:config security.enterprise_roots.enabled = true, or import certs\localhost.cer" -ForegroundColor Cyan
