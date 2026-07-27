[CmdletBinding(DefaultParameterSetName = 'Inspect')]
param(
    [Parameter(Mandatory, ParameterSetName = 'Stage')]
    [string]$FtdiInstallerPath,

    [Parameter(Mandatory, ParameterSetName = 'Stage')]
    [ValidatePattern('^[A-Fa-f0-9]{64}$')]
    [string]$ExpectedSha256,

    [Parameter(Mandatory, ParameterSetName = 'Verify')]
    [switch]$VerifyStaged,

    [string]$SevenZipPath
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$officialDownloadPage = 'https://ftdichip.com/drivers/vcp-drivers/'
$stagingDirectory = Join-Path $repositoryRoot 'artifacts\windows-dependencies\ftdi'
$stagedInstaller = Join-Path $stagingDirectory 'FTDI-CDM-VCP-Setup.exe'
$checksumPath = "$stagedInstaller.sha256"
$signatureMetadataPath = "$stagedInstaller.authenticode.json"
$payloadDirectory = Join-Path $stagingDirectory 'payload'
$requiredPayloadFiles = @(
    'amd64\ftbusui.dll',
    'amd64\ftcserco.dll',
    'amd64\ftd2xx.lib',
    'amd64\FTD2XX64.dll',
    'amd64\ftdibus.sys',
    'amd64\ftlang.dll',
    'amd64\ftser2k.sys',
    'amd64\ftserui2.dll',
    'dp-chooser.exe',
    'dpinst-amd64.exe',
    'dpinst-x86.exe',
    'dpinst.xml',
    'ftd2xx.h',
    'ftdibus.cat',
    'ftdibus.inf',
    'ftdiport.cat',
    'ftdiport.inf',
    'i386\ftbusui.dll',
    'i386\ftcserco.dll',
    'i386\ftd2xx.dll',
    'i386\ftd2xx.lib',
    'i386\ftdibus.sys',
    'i386\ftlang.dll',
    'i386\ftser2k.sys',
    'i386\ftserui2.dll',
    'licence.txt',
    'Static\amd64\FTD2XX.lib',
    'Static\i386\ftd2xx.lib'
)
$requiredDpInstAmd64Sha256 = '88A83AE4F59B499961C50E8FD6EDB66E73A4D7F10E818D456303200BE2920B84'
$requiredDpInstX86Sha256 = '8B3D65FF519DA3DC285E1CED3F3442FF2E9EA4C809605417F40BA236410A452A'
$requiredBusCatalogSha256 = 'B5376AE984033E0A503B793E31121F3208453A505BF9AC150F466690504782EF'
$requiredPortCatalogSha256 = '134B692A4AF2148D60C6E1874FA6E6AF12507B3C5DC64A2A568E44EC1D64FE9F'

function Find-SevenZip {
    if (-not [string]::IsNullOrWhiteSpace($SevenZipPath)) {
        return (Resolve-Path -LiteralPath $SevenZipPath -ErrorAction Stop).Path
    }

    foreach ($commandName in @('7z.exe', '7za.exe')) {
        $command = Get-Command $commandName -ErrorAction SilentlyContinue
        if ($null -ne $command) {
            return $command.Source
        }
    }

    $candidates = @(
        (Join-Path $env:ProgramFiles '7-Zip\7z.exe'),
        (Join-Path ${env:ProgramFiles(x86)} '7-Zip\7z.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'JMStudio\2.6.13.0200\Tools\7-Zip\7z.exe')
    )
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return $candidate
        }
    }

    $jmStudioRoot = Join-Path ${env:ProgramFiles(x86)} 'JMStudio'
    if (Test-Path -LiteralPath $jmStudioRoot -PathType Container) {
        $candidate = Get-ChildItem -LiteralPath $jmStudioRoot -Filter 7z.exe -File -Recurse |
            Sort-Object FullName -Descending |
            Select-Object -First 1
        if ($null -ne $candidate) {
            return $candidate.FullName
        }
    }

    throw (
        '7z.exe was not found. It is required only to extract the verified FTDI ' +
        'self-extracting archive into an unchanged silent-install payload.'
    )
}

function Assert-ChildPath {
    param(
        [Parameter(Mandatory)]
        [string]$Parent,

        [Parameter(Mandatory)]
        [string]$Child
    )

    $parentFullPath = [System.IO.Path]::GetFullPath($Parent)
    $parentFullPath = $parentFullPath.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar
    ) + [System.IO.Path]::DirectorySeparatorChar
    $childFullPath = [System.IO.Path]::GetFullPath($Child)
    if (-not $childFullPath.StartsWith(
        $parentFullPath,
        [System.StringComparison]::OrdinalIgnoreCase
    )) {
        throw "Unsafe dependency staging path: '$childFullPath'."
    }
}

function Get-VerifiedFtdiSignature {
    param(
        [Parameter(Mandatory)]
        [string]$LiteralPath
    )

    $signature = Get-AuthenticodeSignature -LiteralPath $LiteralPath
    if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid) {
        throw "The FTDI installer does not have a valid Authenticode signature. Status: $($signature.Status)."
    }

    $certificate = $signature.SignerCertificate
    if ($null -eq $certificate) {
        throw 'The FTDI installer signature does not contain a signer certificate.'
    }

    if ($certificate.Subject -notmatch '(?i)(FTDI|Future Technology Devices)') {
        throw "The staged installer signer is not recognized as FTDI: $($certificate.Subject)"
    }

    return $signature
}

function Test-PayloadSignature {
    param(
        [Parameter(Mandatory)]
        [string]$LiteralPath,

        [Parameter(Mandatory)]
        [string]$SignerPattern
    )

    $signature = Get-AuthenticodeSignature -LiteralPath $LiteralPath
    if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid) {
        throw "The extracted FTDI payload signature is invalid: '$LiteralPath'."
    }
    if ($null -eq $signature.SignerCertificate -or
        $signature.SignerCertificate.Subject -notmatch $SignerPattern) {
        throw "The extracted FTDI payload signer is unexpected: '$LiteralPath'."
    }
}

function Test-ExtractedPayload {
    param(
        [Parameter(Mandatory)]
        [string]$LiteralPath
    )

    if (-not (Test-Path -LiteralPath $LiteralPath -PathType Container)) {
        throw "The extracted FTDI payload is missing: '$LiteralPath'."
    }

    $payloadRoot = [System.IO.Path]::GetFullPath($LiteralPath)
    $payloadRoot = $payloadRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar)
    $actualFiles = Get-ChildItem -LiteralPath $payloadRoot -File -Recurse |
        ForEach-Object {
            $_.FullName.Substring($payloadRoot.Length + 1)
        } |
        Sort-Object
    $differences = Compare-Object `
        -ReferenceObject ($requiredPayloadFiles | Sort-Object) `
        -DifferenceObject $actualFiles
    if ($null -ne $differences) {
        throw (
            'The extracted FTDI payload does not match the exact 28-file ' +
            "allowlist:`n$($differences | Out-String)"
        )
    }

    $hashRequirements = @{
        'dpinst-amd64.exe' = $requiredDpInstAmd64Sha256
        'dpinst-x86.exe' = $requiredDpInstX86Sha256
        'ftdibus.cat' = $requiredBusCatalogSha256
        'ftdiport.cat' = $requiredPortCatalogSha256
    }
    foreach ($entry in $hashRequirements.GetEnumerator()) {
        $path = Join-Path $payloadRoot $entry.Key
        $actualHash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
        if ($actualHash -ne $entry.Value) {
            throw "The extracted FTDI payload hash is invalid for '$($entry.Key)'."
        }
    }

    Test-PayloadSignature `
        -LiteralPath (Join-Path $payloadRoot 'dpinst-amd64.exe') `
        -SignerPattern '(?i)(FTDI|Future Technology Devices)'
    Test-PayloadSignature `
        -LiteralPath (Join-Path $payloadRoot 'dpinst-x86.exe') `
        -SignerPattern '(?i)(FTDI|Future Technology Devices)'
    Test-PayloadSignature `
        -LiteralPath (Join-Path $payloadRoot 'ftdibus.cat') `
        -SignerPattern '(?i)Microsoft Windows Hardware Compatibility Publisher'
    Test-PayloadSignature `
        -LiteralPath (Join-Path $payloadRoot 'ftdiport.cat') `
        -SignerPattern '(?i)Microsoft Windows Hardware Compatibility Publisher'

    foreach ($driverPath in Get-ChildItem -LiteralPath $payloadRoot -Filter *.sys -File -Recurse) {
        Test-PayloadSignature `
            -LiteralPath $driverPath.FullName `
            -SignerPattern '(?i)Microsoft Windows Hardware Compatibility Publisher'
    }

    [pscustomobject]@{
        Payload = $payloadRoot
        FileCount = $actualFiles.Count
        DpInstAmd64Sha256 = $requiredDpInstAmd64Sha256
        BusCatalogSha256 = $requiredBusCatalogSha256
        PortCatalogSha256 = $requiredPortCatalogSha256
    }
}

function Expand-VerifiedFtdiPayload {
    $sevenZip = Find-SevenZip
    $temporaryPayloadDirectory = Join-Path $stagingDirectory (
        'payload-staging-' + [Guid]::NewGuid().ToString('N')
    )
    Assert-ChildPath -Parent $stagingDirectory -Child $temporaryPayloadDirectory
    New-Item -ItemType Directory -Path $temporaryPayloadDirectory | Out-Null

    try {
        & $sevenZip t $stagedInstaller | Out-Host
        if ($LASTEXITCODE -ne 0) {
            throw '7-Zip reported that the FTDI self-extracting archive is invalid.'
        }

        & $sevenZip x -y -bb0 "-o$temporaryPayloadDirectory" $stagedInstaller | Out-Host
        if ($LASTEXITCODE -ne 0) {
            throw '7-Zip could not extract the verified FTDI payload.'
        }

        Test-ExtractedPayload -LiteralPath $temporaryPayloadDirectory | Out-Host

        if (Test-Path -LiteralPath $payloadDirectory -PathType Container) {
            Assert-ChildPath -Parent $stagingDirectory -Child $payloadDirectory
            Remove-Item -LiteralPath $payloadDirectory -Recurse -Force
        }
        Move-Item -LiteralPath $temporaryPayloadDirectory -Destination $payloadDirectory
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPayloadDirectory -PathType Container) {
            Assert-ChildPath -Parent $stagingDirectory -Child $temporaryPayloadDirectory
            Remove-Item -LiteralPath $temporaryPayloadDirectory -Recurse -Force
        }
    }

    Test-ExtractedPayload -LiteralPath $payloadDirectory
}

function Test-StagedInstaller {
    if (-not (Test-Path -LiteralPath $stagedInstaller -PathType Leaf)) {
        throw "No staged FTDI installer was found at '$stagedInstaller'."
    }

    if (-not (Test-Path -LiteralPath $checksumPath -PathType Leaf)) {
        throw "The staged FTDI checksum record is missing: '$checksumPath'."
    }

    $checksumText = Get-Content -Raw -LiteralPath $checksumPath
    $checksumMatch = [regex]::Match($checksumText, '(?i)\b[A-F0-9]{64}\b')
    if (-not $checksumMatch.Success) {
        throw "The staged checksum record is invalid: '$checksumPath'."
    }

    $actualHash = (Get-FileHash -LiteralPath $stagedInstaller -Algorithm SHA256).Hash
    if ($actualHash -ne $checksumMatch.Value.ToUpperInvariant()) {
        throw "The staged FTDI installer hash does not match its recorded SHA-256 value. Actual: $actualHash"
    }

    $signature = Get-VerifiedFtdiSignature -LiteralPath $stagedInstaller
    $payload = Test-ExtractedPayload -LiteralPath $payloadDirectory
    [pscustomobject]@{
        Installer = $stagedInstaller
        Sha256 = $actualHash
        SignatureStatus = $signature.Status
        Signer = $signature.SignerCertificate.Subject
        Payload = $payload.Payload
        PayloadFiles = $payload.FileCount
        OfficialSource = $officialDownloadPage
    }
}

if ($PSCmdlet.ParameterSetName -eq 'Inspect') {
    [pscustomobject]@{
        Action = 'No files downloaded'
        OfficialSource = $officialDownloadPage
        StagingDestination = $stagedInstaller
        Instructions = 'Download the current Windows setup executable manually from FTDI, record its SHA-256, then rerun this script with -FtdiInstallerPath and -ExpectedSha256.'
    }
    return
}

if ($PSCmdlet.ParameterSetName -eq 'Verify') {
    Test-StagedInstaller
    return
}

$resolvedSource = (Resolve-Path -LiteralPath $FtdiInstallerPath -ErrorAction Stop).Path
if ([System.IO.Path]::GetExtension($resolvedSource) -ne '.exe') {
    throw 'The FTDI dependency must be the official Windows setup executable.'
}

$expectedHash = $ExpectedSha256.ToUpperInvariant()
$sourceHash = (Get-FileHash -LiteralPath $resolvedSource -Algorithm SHA256).Hash
if ($sourceHash -ne $expectedHash) {
    throw "The supplied FTDI installer does not match the expected SHA-256 value. Actual: $sourceHash"
}

$sourceSignature = Get-VerifiedFtdiSignature -LiteralPath $resolvedSource

New-Item -ItemType Directory -Path $stagingDirectory -Force | Out-Null
Copy-Item -LiteralPath $resolvedSource -Destination $stagedInstaller -Force

$stagedHash = (Get-FileHash -LiteralPath $stagedInstaller -Algorithm SHA256).Hash
if ($stagedHash -ne $expectedHash) {
    throw 'The FTDI installer changed while being copied to the local staging area.'
}

"$stagedHash  FTDI-CDM-VCP-Setup.exe" |
    Set-Content -LiteralPath $checksumPath -Encoding ascii

$payload = Expand-VerifiedFtdiPayload

[ordered]@{
    FileName = 'FTDI-CDM-VCP-Setup.exe'
    Sha256 = $stagedHash
    SignatureStatus = $sourceSignature.Status.ToString()
    SignerSubject = $sourceSignature.SignerCertificate.Subject
    SignerThumbprint = $sourceSignature.SignerCertificate.Thumbprint
    Payload = $payload.Payload
    PayloadFiles = $payload.FileCount
    DpInstAmd64Sha256 = $payload.DpInstAmd64Sha256
    BusCatalogSha256 = $payload.BusCatalogSha256
    PortCatalogSha256 = $payload.PortCatalogSha256
    OfficialSource = $officialDownloadPage
    StagedUtc = [DateTimeOffset]::UtcNow.ToString('O')
} |
    ConvertTo-Json |
    Set-Content -LiteralPath $signatureMetadataPath -Encoding utf8

Test-StagedInstaller
