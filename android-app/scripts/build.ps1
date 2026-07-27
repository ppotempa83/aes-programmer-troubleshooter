[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$androidRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $androidRoot 'src\SuperiorAes.Android\SuperiorAes.Android.csproj'
$dotnet = (Get-Command dotnet -ErrorAction Stop).Source

$sdkList = & $dotnet --list-sdks
if (-not ($sdkList | Select-String -Pattern '^10\.'))
{
    throw '.NET 10 SDK is required. No workload or SDK was installed automatically.'
}

$workloads = & $dotnet workload list
if (-not ($workloads | Select-String -Pattern '\b(maui-android|android)\b'))
{
    throw 'The .NET MAUI Android workload is required. See android-app\README.md.'
}

& $dotnet build $project -f net10.0-android -c $Configuration
if ($LASTEXITCODE -ne 0)
{
    throw 'Android build failed.'
}

