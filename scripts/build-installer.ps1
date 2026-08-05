[CmdletBinding()]
param(
    [switch]$SkipValidation,
    [switch]$SkipPublish,

    [ValidatePattern('^[A-Fa-f0-9 ]{40,64}$')]
    [string]$CertificateThumbprint,

    [string]$PublicCertificatePath,

    [ValidatePattern('^https?://')]
    [string]$TimestampUrl = 'http://timestamp.digicert.com'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot 'src\SuperiorAes.App\SuperiorAes.App.csproj'
$publishRoot = Join-Path $repositoryRoot 'artifacts\publish\win-x64'
$installerOutputRoot = Join-Path $repositoryRoot 'artifacts\installer'
$releaseOutputRoot = Join-Path $repositoryRoot 'artifacts\release'
$dependencyRoot = Join-Path $repositoryRoot 'artifacts\windows-dependencies'
$signingOutputRoot = Join-Path $repositoryRoot 'artifacts\signing'
$installerScript = Join-Path $repositoryRoot 'installer\windows\SuperiorAesProgrammer.iss'
$certificateInstructionTemplate = Join-Path $repositoryRoot 'installer\windows\INSTALL-CERTIFICATE-FIRST.template.txt'
$signScript = Join-Path $PSScriptRoot 'sign-release.ps1'
$dependencyScript = Join-Path $PSScriptRoot 'fetch-windows-dependencies.ps1'

# This release is intentionally pinned to the user-supplied, vendor-signed
# CDM2123620_Setup.exe. Updating the FTDI dependency requires an explicit source
# change and a new independent signature/hash review.
$requiredFtdiSha256 = '6B4985913C5F8B0B9656E8BAA6F1193B2B7EF3385F67DDC09FBA83A670B5444D'
$requiredFtdiSignerThumbprint = '69B945B393E94C281BF255F09F569F91D3E25E07'

function Find-InnoCompiler {
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    $commandCandidate = if ($null -ne $command) { $command.Source } else { $null }

    $candidates = @(
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
        $commandCandidate,
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 7\ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 7\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 7\ISCC.exe')
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

    $uninstallRoots = @(
        'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*',
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*',
        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*'
    )
    $registeredInstallations = Get-ItemProperty $uninstallRoots -ErrorAction SilentlyContinue |
        Where-Object {
            $_.DisplayName -like 'Inno Setup*' -and
            -not [string]::IsNullOrWhiteSpace($_.InstallLocation)
        } |
        Sort-Object {
            if ($_.DisplayName -like 'Inno Setup 6*') { 0 } else { 1 }
        }, DisplayVersion -Descending
    foreach ($installation in $registeredInstallations) {
        $candidates += Join-Path $installation.InstallLocation 'ISCC.exe'
    }

    foreach ($candidate in $candidates | Select-Object -Unique) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return $candidate
        }
    }

    throw 'ISCC.exe was not found. Install Inno Setup 6.3 or later from https://jrsoftware.org/isinfo.php and rerun this script.'
}

$versionNode = Select-Xml -LiteralPath $projectPath -XPath '/Project/PropertyGroup/Version' |
    Select-Object -First 1
if ($null -eq $versionNode -or [string]::IsNullOrWhiteSpace($versionNode.Node.InnerText)) {
    throw "No application version was found in '$projectPath'."
}
$appVersion = $versionNode.Node.InnerText.Trim()

Push-Location $repositoryRoot
try {
    if (-not $SkipValidation) {
        & (Join-Path $PSScriptRoot 'build.ps1') -Configuration Release
        if ($LASTEXITCODE -ne 0) {
            throw 'Release validation failed.'
        }
    }

    if (-not $SkipPublish) {
        & (Join-Path $PSScriptRoot 'publish.ps1')
        if ($LASTEXITCODE -ne 0) {
            throw 'Self-contained Windows publish failed.'
        }
    }

    $publishedExecutable = Join-Path $publishRoot 'AesProgrammer.Troubleshooter.exe'
    if (-not (Test-Path -LiteralPath $publishedExecutable -PathType Leaf)) {
        throw "The published application was not found at '$publishedExecutable'."
    }

    $stagedFtdiInstaller = Join-Path $dependencyRoot 'ftdi\FTDI-CDM-VCP-Setup.exe'
    if (-not (Test-Path -LiteralPath $stagedFtdiInstaller -PathType Leaf)) {
        throw (
            "The required FTDI VCP installer is missing. Stage the approved file " +
            "with scripts\fetch-windows-dependencies.ps1 before building setup."
        )
    }

    $ftdiVerification = & $dependencyScript -VerifyStaged
    $ftdiVerification | Out-Host
    if ($ftdiVerification.Sha256 -ne $requiredFtdiSha256) {
        throw (
            'The staged FTDI installer is not the pinned CDM 2.12.36.20 file. ' +
            "Expected $requiredFtdiSha256; found $($ftdiVerification.Sha256)."
        )
    }

    $ftdiSignature = Get-AuthenticodeSignature -LiteralPath $stagedFtdiInstaller
    if ($null -eq $ftdiSignature.SignerCertificate -or
        $ftdiSignature.SignerCertificate.Thumbprint -ne $requiredFtdiSignerThumbprint) {
        throw (
            'The staged FTDI installer does not have the pinned FTDI signer ' +
            "certificate thumbprint $requiredFtdiSignerThumbprint."
        )
    }

    $normalizedThumbprint = $null
    $publicCertificate = $null
    $resolvedPublicCertificatePath = $null
    $certificateInstructionsPath = $null

    if (-not [string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
        $normalizedThumbprint = ($CertificateThumbprint -replace '\s', '').ToUpperInvariant()
        if ($normalizedThumbprint -notmatch '^[A-F0-9]{40}$') {
            throw 'The signing certificate thumbprint must be a 40-character SHA-1 thumbprint.'
        }

        if ([string]::IsNullOrWhiteSpace($PublicCertificatePath)) {
            $PublicCertificatePath = Join-Path $signingOutputRoot 'AES-Programmer-Troubleshooter-Code-Signing.cer'
        }
        $resolvedPublicCertificatePath = (
            Resolve-Path -LiteralPath $PublicCertificatePath -ErrorAction Stop
        ).Path
        $publicCertificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new(
            $resolvedPublicCertificatePath
        )
        if ($publicCertificate.HasPrivateKey) {
            throw 'The release package certificate must be a public-only .cer file.'
        }
        if ($publicCertificate.Thumbprint -ne $normalizedThumbprint) {
            throw (
                'The public certificate does not match the requested signing ' +
                "certificate thumbprint $normalizedThumbprint."
            )
        }

        New-Item -ItemType Directory -Path $signingOutputRoot -Force | Out-Null
        $certificateInstructionsPath = Join-Path $signingOutputRoot 'INSTALL-CERTIFICATE-FIRST.txt'
        $instructionText = Get-Content -Raw -LiteralPath $certificateInstructionTemplate
        $instructionText = $instructionText.Replace(
            '{CERTIFICATE_FILE}',
            [System.IO.Path]::GetFileName($resolvedPublicCertificatePath)
        )
        $instructionText = $instructionText.Replace(
            '{CERTIFICATE_SUBJECT}',
            $publicCertificate.Subject
        )
        $instructionText = $instructionText.Replace(
            '{CERTIFICATE_THUMBPRINT}',
            $publicCertificate.Thumbprint
        )
        $instructionText = $instructionText.Replace(
            '{CERTIFICATE_SHA256}',
            (Get-FileHash -LiteralPath $resolvedPublicCertificatePath -Algorithm SHA256).Hash
        )
        $instructionText = $instructionText.Replace(
            '{CERTIFICATE_NOT_BEFORE}',
            $publicCertificate.NotBefore.ToString('yyyy-MM-dd HH:mm:ss zzz')
        )
        $instructionText = $instructionText.Replace(
            '{CERTIFICATE_NOT_AFTER}',
            $publicCertificate.NotAfter.ToString('yyyy-MM-dd HH:mm:ss zzz')
        )
        $instructionText = $instructionText.Replace(
            '{INSTALLER_FILE}',
            "AES-Programmer-Troubleshooter-v$appVersion-win-x64-setup.exe"
        )
        $instructionText = $instructionText.Replace('{FTDI_SHA256}', $requiredFtdiSha256)
        Set-Content -LiteralPath $certificateInstructionsPath -Value $instructionText -Encoding utf8

        & $signScript `
            -Path $publishedExecutable `
            -CertificateThumbprint $normalizedThumbprint `
            -TimestampUrl $TimestampUrl |
            Out-Host
    }

    New-Item -ItemType Directory -Path $installerOutputRoot -Force | Out-Null
    $innoCompiler = Find-InnoCompiler
    $innoArguments = @(
        "/DRepoRoot=$repositoryRoot",
        "/DAppVersion=$appVersion",
        "/DPublishRoot=$publishRoot",
        "/DInstallerOutputRoot=$installerOutputRoot",
        "/DDependencyRoot=$dependencyRoot"
    )
    if ($null -ne $publicCertificate) {
        $innoArguments += "/DSigningCertificate=$resolvedPublicCertificatePath"
        $innoArguments += "/DCertificateInstructions=$certificateInstructionsPath"
    }
    $innoArguments += $installerScript

    & $innoCompiler @innoArguments
    if ($LASTEXITCODE -ne 0) {
        throw 'Inno Setup compilation failed.'
    }

    $installerFileName = "AES-Programmer-Troubleshooter-v$appVersion-win-x64-setup.exe"
    $installerPath = Join-Path $installerOutputRoot $installerFileName
    if (-not (Test-Path -LiteralPath $installerPath -PathType Leaf)) {
        throw "The expected installer was not produced at '$installerPath'."
    }

    if ($null -ne $normalizedThumbprint) {
        & $signScript `
            -Path $installerPath `
            -CertificateThumbprint $normalizedThumbprint `
            -TimestampUrl $TimestampUrl |
            Out-Host
    }

    $installerHash = (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash
    $checksumPath = "$installerPath.sha256"
    "$installerHash  $installerFileName" |
        Set-Content -LiteralPath $checksumPath -Encoding ascii

    $bundleDirectory = Join-Path $releaseOutputRoot "AES-Programmer-Troubleshooter-v$appVersion-win-x64"
    New-Item -ItemType Directory -Path $bundleDirectory -Force | Out-Null

    $bundledInstaller = Join-Path $bundleDirectory $installerFileName
    $bundledInstallerChecksum = Join-Path $bundleDirectory "$installerFileName.sha256"
    Copy-Item -LiteralPath $installerPath -Destination $bundledInstaller -Force
    Copy-Item -LiteralPath $checksumPath -Destination $bundledInstallerChecksum -Force

    $releaseBundleFiles = @($bundledInstaller, $bundledInstallerChecksum)

    if ($null -ne $publicCertificate) {
        $bundledCertificate = Join-Path $bundleDirectory (
            [System.IO.Path]::GetFileName($resolvedPublicCertificatePath)
        )
        $bundledCertificateInstructions = Join-Path $bundleDirectory 'INSTALL-CERTIFICATE-FIRST.txt'
        Copy-Item -LiteralPath $resolvedPublicCertificatePath -Destination $bundledCertificate -Force
        Copy-Item -LiteralPath $certificateInstructionsPath -Destination $bundledCertificateInstructions -Force
        $releaseBundleFiles += @($bundledCertificate, $bundledCertificateInstructions)
    }

    $manifestPath = Join-Path $bundleDirectory 'RELEASE-MANIFEST.txt'
    $manifestLines = @(
        'AES Programmer & Troubleshooter Windows release',
        "Application version: $appVersion",
        "Installer: $installerFileName",
        "Installer SHA-256: $installerHash",
        'FTDI package: CDM2123620_Setup.exe (original retained for provenance)',
        'FTDI install path: verified unchanged x64 DPInst payload runs quietly first',
        "FTDI SHA-256: $requiredFtdiSha256",
        "FTDI signer thumbprint: $requiredFtdiSignerThumbprint",
        'Bundled API keys or credential files: NONE',
        'Geoapify setup: create a personal project at https://myprojects.geoapify.com/',
        "Built UTC: $([DateTimeOffset]::UtcNow.ToString('o'))"
    )
    if ($null -ne $publicCertificate) {
        $manifestLines += @(
            "Code-signing subject: $($publicCertificate.Subject)",
            "Code-signing thumbprint: $($publicCertificate.Thumbprint)",
            "Public certificate SHA-256: $((Get-FileHash -LiteralPath $resolvedPublicCertificatePath -Algorithm SHA256).Hash)",
            'Private key included: NO'
        )
    }
    Set-Content -LiteralPath $manifestPath -Value $manifestLines -Encoding utf8
    $releaseBundleFiles += $manifestPath

    $releaseZipPath = Join-Path $releaseOutputRoot "AES-Programmer-Troubleshooter-v$appVersion-win-x64.zip"
    if (Test-Path -LiteralPath $releaseZipPath -PathType Leaf) {
        Remove-Item -LiteralPath $releaseZipPath -Force
    }
    Compress-Archive -LiteralPath $releaseBundleFiles -DestinationPath $releaseZipPath -CompressionLevel Optimal
    $releaseZipHash = (Get-FileHash -LiteralPath $releaseZipPath -Algorithm SHA256).Hash
    $releaseZipChecksumPath = "$releaseZipPath.sha256"
    "$releaseZipHash  $([System.IO.Path]::GetFileName($releaseZipPath))" |
        Set-Content -LiteralPath $releaseZipChecksumPath -Encoding ascii

    [pscustomobject]@{
        Installer = $installerPath
        InstallerSha256 = $installerHash
        InstallerChecksumFile = $checksumPath
        Signed = $null -ne $normalizedThumbprint
        SigningCertificate = $resolvedPublicCertificatePath
        FtdiInstallerIncluded = $true
        FtdiSha256 = $requiredFtdiSha256
        ReleaseDirectory = $bundleDirectory
        ReleaseZip = $releaseZipPath
        ReleaseZipSha256 = $releaseZipHash
        ReleaseZipChecksumFile = $releaseZipChecksumPath
    }
}
finally {
    Pop-Location
}
