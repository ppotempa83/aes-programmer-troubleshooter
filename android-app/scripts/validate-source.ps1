[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$androidRoot = Split-Path -Parent $PSScriptRoot

$required = @(
    'SuperiorAes.Android.sln',
    'README.md',
    'AGENTS.md',
    'src\SuperiorAes.Android\SuperiorAes.Android.csproj',
    'src\SuperiorAes.Android\App.xaml',
    'src\SuperiorAes.Android\AppShell.xaml',
    'src\SuperiorAes.Android\Services\IAesTransport.cs',
    'src\SuperiorAes.Android\Services\IFtdiD2xxTransport.cs',
    'src\SuperiorAes.Android\Services\SimulatedAesTransport.cs',
    'src\SuperiorAes.Android\Pages\ProgrammingPage.xaml',
    'src\SuperiorAes.Android\Pages\ContactIdPage.xaml',
    'src\SuperiorAes.Android\Pages\TroubleshooterPage.xaml',
    'src\SuperiorAes.Android\Pages\SitePlanningPage.xaml',
    'src\SuperiorAes.Android\Pages\ReportsPage.xaml',
    'docs\ANDROID-SIGNING.md',
    'docs\FTDI-D2XX-INTEGRATION.md',
    'docs\PHYSICAL-VALIDATION.md'
)

$missing = @(
    foreach ($relativePath in $required)
    {
        $fullPath = Join-Path $androidRoot $relativePath
        if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf))
        {
            $relativePath
        }
    }
)
if ($missing.Count -gt 0)
{
    throw "Missing required Android application files: $($missing -join ', ')"
}

$forbiddenExtensions = @(
    '.apk', '.aab', '.apks', '.jar', '.aar', '.keystore', '.jks',
    '.pfx', '.p12', '.exe', '.dll', '.zip', '.7z'
)
$forbiddenFiles = @(
    Get-ChildItem -LiteralPath $androidRoot -Recurse -File |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
        Where-Object { $forbiddenExtensions -contains $_.Extension.ToLowerInvariant() } |
        Select-Object -ExpandProperty FullName
)
if ($forbiddenFiles.Count -gt 0)
{
    throw "Binary dependency, signing material, or build output found: $($forbiddenFiles -join ', ')"
}

$xmlExtensions = @('.xaml', '.xml', '.svg', '.csproj')
$xmlFiles = @(
    Get-ChildItem -LiteralPath $androidRoot -Recurse -File |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
        Where-Object { $xmlExtensions -contains $_.Extension.ToLowerInvariant() }
)
foreach ($file in $xmlFiles)
{
    try
    {
        [void][xml](Get-Content -Raw -LiteralPath $file.FullName)
    }
    catch
    {
        throw "Invalid XML in $($file.FullName): $($_.Exception.Message)"
    }
}

$textExtensions = @('.cs', '.xaml', '.xml', '.svg', '.csproj', '.md', '.txt', '.ps1', '.sln')
$textFiles = @(
    Get-ChildItem -LiteralPath $androidRoot -Recurse -File |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
        Where-Object { $textExtensions -contains $_.Extension.ToLowerInvariant() }
)
$combinedText = ($textFiles | ForEach-Object { Get-Content -Raw -LiteralPath $_.FullName }) -join "`n"

foreach ($requiredText in @(
    'AES Programmer & Troubleshooter',
    'FTDI USB hardware bench',
    'IntelliPro',
    'IntelliTap',
    'J1 pin 6'
))
{
    if (-not $combinedText.Contains($requiredText, [StringComparison]::Ordinal))
    {
        throw "Required application text is absent: $requiredText"
    }
}

if ($combinedText -match '(?i)\b[a-f0-9]{32,}\b')
{
    throw 'A long hexadecimal token was found. Review the application source for embedded credentials.'
}

Write-Output "Android application source validation passed: $($textFiles.Count) text files, $($xmlFiles.Count) XML-family files."
