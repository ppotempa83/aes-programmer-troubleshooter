[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$localSdk = Join-Path $env:USERPROFILE '.codex\tools\dotnet8-sdk\dotnet.exe'
$dotnet = if (Test-Path -LiteralPath $localSdk) {
    $localSdk
} else {
    (Get-Command dotnet -ErrorAction Stop).Source
}
$publishDirectory = Join-Path $repositoryRoot 'artifacts\publish\win-x64'

Push-Location $repositoryRoot
try {
    & $dotnet publish src\SuperiorAes.App\SuperiorAes.App.csproj `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -o $publishDirectory `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=None `
        -p:DebugSymbols=false
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }
}
finally {
    Pop-Location
}

