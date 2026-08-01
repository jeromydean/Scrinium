function Get-ComposeCommand {
  if (Get-Command podman -ErrorAction SilentlyContinue) {
    return @("podman", "compose")
  }
  if (Get-Command docker -ErrorAction SilentlyContinue) {
    return @("docker", "compose")
  }
  throw "Neither podman nor docker was found in PATH. Install Podman or Docker and try again."
}

function Invoke-Compose {
  param(
    [Parameter(Mandatory = $true, ValueFromRemainingArguments = $true)]
    [string[]] $Args
  )

  $compose = Get-ComposeCommand
  & $compose[0] $compose[1] @Args
  if ($LASTEXITCODE -ne 0) {
    throw "compose failed with exit code $LASTEXITCODE"
  }
}
