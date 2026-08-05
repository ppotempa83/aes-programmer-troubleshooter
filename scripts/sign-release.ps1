[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)]
    [string[]]$Path,

    [Parameter(Mandatory)]
    [ValidatePattern('^[A-Fa-f0-9 ]{40,64}$')]
    [string]$CertificateThumbprint,

    [ValidatePattern('^https?://')]
    [string]$TimestampUrl = 'http://timestamp.digicert.com',

    [string]$Description = 'AES Programmer & Troubleshooter',

    [ValidatePattern('^https?://')]
    [string]$DescriptionUrl
)

$ErrorActionPreference = 'Stop'
$normalizedThumbprint = ($CertificateThumbprint -replace '\s', '').ToUpperInvariant()
if ($normalizedThumbprint -notmatch '^[A-F0-9]{40}$') {
    throw 'The certificate thumbprint must be the 40-character SHA-1 thumbprint shown by the Windows certificate store.'
}

function Find-SignTool {
    $command = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    $windowsKitsRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
    if (Test-Path -LiteralPath $windowsKitsRoot) {
        $candidate = Get-ChildItem -LiteralPath $windowsKitsRoot -Filter signtool.exe -File -Recurse -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match '\\x64\\signtool\.exe$' } |
            Sort-Object FullName -Descending |
            Select-Object -First 1
        if ($null -ne $candidate) {
            return $candidate.FullName
        }
    }

    throw 'SignTool.exe was not found. Install the Windows SDK Signing Tools feature and try again.'
}

$certificate = $null
$useMachineStore = $false
$currentUserCertificatePath = "Cert:\CurrentUser\My\$normalizedThumbprint"
$localMachineCertificatePath = "Cert:\LocalMachine\My\$normalizedThumbprint"

if (Test-Path -LiteralPath $currentUserCertificatePath) {
    $certificate = Get-Item -LiteralPath $currentUserCertificatePath
}
elseif (Test-Path -LiteralPath $localMachineCertificatePath) {
    $certificate = Get-Item -LiteralPath $localMachineCertificatePath
    $useMachineStore = $true
}
else {
    throw "No certificate with thumbprint '$normalizedThumbprint' was found in the CurrentUser or LocalMachine Personal certificate store."
}

if (-not $certificate.HasPrivateKey) {
    throw 'The selected certificate does not have an accessible private key.'
}

$now = Get-Date
if ($certificate.NotBefore -gt $now -or $certificate.NotAfter -le $now) {
    throw "The selected certificate is not currently valid. Valid from $($certificate.NotBefore) through $($certificate.NotAfter)."
}

$codeSigningOid = '1.3.6.1.5.5.7.3.3'
if ($certificate.EnhancedKeyUsageList.Count -gt 0 -and
    $certificate.EnhancedKeyUsageList.ObjectId -notcontains $codeSigningOid) {
    throw 'The selected certificate is not authorized for code signing.'
}
$isSelfSignedCertificate = $certificate.Subject -eq $certificate.Issuer

$resolvedTargets = foreach ($target in $Path) {
    $resolvedTarget = Resolve-Path -LiteralPath $target -ErrorAction Stop
    if (-not (Test-Path -LiteralPath $resolvedTarget.Path -PathType Leaf)) {
        throw "Signing target is not a file: '$target'."
    }
    $resolvedTarget.Path
}

$signTool = Find-SignTool
$results = foreach ($target in $resolvedTargets) {
    $signArguments = @(
        'sign',
        '/sha1', $normalizedThumbprint,
        '/s', 'My',
        '/fd', 'SHA256',
        '/tr', $TimestampUrl,
        '/td', 'SHA256',
        '/d', $Description
    )

    if ($useMachineStore) {
        $signArguments += '/sm'
    }

    if (-not [string]::IsNullOrWhiteSpace($DescriptionUrl)) {
        $signArguments += @('/du', $DescriptionUrl)
    }

    $signArguments += $target
    & $signTool @signArguments
    if ($LASTEXITCODE -ne 0) {
        throw "SignTool failed while signing '$target'."
    }

    $authenticodeSignature = Get-AuthenticodeSignature -LiteralPath $target
    if ($null -eq $authenticodeSignature.SignerCertificate) {
        throw "The signed file does not contain a signer certificate: '$target'."
    }

    if ($authenticodeSignature.SignerCertificate.Thumbprint -ne $normalizedThumbprint) {
        throw "The signed file is not signed by the requested certificate: '$target'."
    }

    if ($null -eq $authenticodeSignature.TimeStamperCertificate) {
        throw "The signed file does not contain the required RFC 3161 timestamp: '$target'."
    }

    $trustedSignature = $authenticodeSignature.Status -eq
        [System.Management.Automation.SignatureStatus]::Valid
    $expectedSelfSignedTrustStatus = $isSelfSignedCertificate -and
        $authenticodeSignature.Status -in @(
            [System.Management.Automation.SignatureStatus]::UnknownError,
            [System.Management.Automation.SignatureStatus]::NotTrusted
        )

    if (-not $trustedSignature -and -not $expectedSelfSignedTrustStatus) {
        throw (
            "The Authenticode signature failed verification for '$target'. " +
            "Status: $($authenticodeSignature.Status). " +
            "Message: $($authenticodeSignature.StatusMessage)"
        )
    }

    if ($trustedSignature) {
        & $signTool verify /pa /all /v /tw $target
        if ($LASTEXITCODE -ne 0) {
            throw "The Authenticode verification failed for '$target'."
        }
    }
    else {
        Write-Warning (
            "The cryptographic signature and timestamp were verified for '$target', " +
            'but the self-signed root is intentionally not trusted on this build ' +
            'workstation. Import the bundled public certificate on target computers.'
        )
    }

    [pscustomobject]@{
        Path = $target
        CertificateSubject = $certificate.Subject
        CertificateThumbprint = $normalizedThumbprint
        TimestampServer = $TimestampUrl
        TimestampCertificate = $authenticodeSignature.TimeStamperCertificate.Subject
        VerificationStatus = $authenticodeSignature.Status.ToString()
        RequiresBundledCertificateTrust = -not $trustedSignature
        Sha256 = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
    }
}

$results
