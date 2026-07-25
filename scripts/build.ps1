[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$localSdk = Join-Path $env:USERPROFILE '.codex\tools\dotnet8-sdk\dotnet.exe'
$dotnet = if (Test-Path -LiteralPath $localSdk) {
    $localSdk
} else {
    (Get-Command dotnet -ErrorAction Stop).Source
}

Push-Location $repositoryRoot
try {
    & $dotnet restore SuperiorAes.sln
    if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }

    & $dotnet build SuperiorAes.sln -c $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed.' }

    & $dotnet test tests\SuperiorAes.Core.Tests\SuperiorAes.Core.Tests.csproj -c $Configuration --no-build
    if ($LASTEXITCODE -ne 0) { throw 'dotnet test failed.' }
}
finally {
    Pop-Location
}

