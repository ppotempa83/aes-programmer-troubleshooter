[CmdletBinding()]
param(
    [string]$Subject = 'CN=Superior Fire & Security AES Programmer Internal Code Signing',

    [string]$FriendlyName = 'Superior AES Programmer Internal Code Signing',

    [ValidateRange(1, 5)]
    [int]$ValidYears = 3,

    [switch]$ForceNew
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$signingOutputRoot = Join-Path $repositoryRoot 'artifacts\signing'
$publicCertificatePath = Join-Path $signingOutputRoot 'Superior-AES-Programmer-Code-Signing.cer'
$metadataPath = Join-Path $signingOutputRoot 'Superior-AES-Programmer-Code-Signing.json'
$codeSigningOid = '1.3.6.1.5.5.7.3.3'
$now = Get-Date

$certificate = $null
if (-not $ForceNew) {
    $certificate = Get-ChildItem Cert:\CurrentUser\My |
        Where-Object {
            $_.Subject -eq $Subject -and
            $_.HasPrivateKey -and
            $_.NotBefore -le $now -and
            $_.NotAfter -gt $now.AddDays(60) -and
            $_.EnhancedKeyUsageList.ObjectId -contains $codeSigningOid
        } |
        Sort-Object NotAfter -Descending |
        Select-Object -First 1
}

if ($null -eq $certificate) {
    $certificate = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject $Subject `
        -FriendlyName $FriendlyName `
        -CertStoreLocation 'Cert:\CurrentUser\My' `
        -KeyAlgorithm RSA `
        -KeyLength 3072 `
        -HashAlgorithm SHA256 `
        -KeyExportPolicy NonExportable `
        -NotAfter $now.AddYears($ValidYears)
}

if (-not $certificate.HasPrivateKey) {
    throw 'The generated code-signing certificate does not have a private key.'
}

if ($certificate.EnhancedKeyUsageList.ObjectId -notcontains $codeSigningOid) {
    throw 'The generated certificate is not authorized for code signing.'
}

New-Item -ItemType Directory -Path $signingOutputRoot -Force | Out-Null
Export-Certificate `
    -Cert $certificate `
    -FilePath $publicCertificatePath `
    -Type CERT `
    -Force |
    Out-Null

$exportedCertificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new(
    $publicCertificatePath
)
if ($exportedCertificate.HasPrivateKey) {
    throw 'The exported .cer unexpectedly contains a private key.'
}

$publicCertificateSha256 = (
    Get-FileHash -LiteralPath $publicCertificatePath -Algorithm SHA256
).Hash

[ordered]@{
    Subject = $certificate.Subject
    FriendlyName = $certificate.FriendlyName
    Thumbprint = $certificate.Thumbprint
    NotBefore = $certificate.NotBefore.ToString('o')
    NotAfter = $certificate.NotAfter.ToString('o')
    EnhancedKeyUsage = @($certificate.EnhancedKeyUsageList |
        ForEach-Object { $_.ObjectId })
    PrivateKeyLocation = "Cert:\CurrentUser\My\$($certificate.Thumbprint)"
    PrivateKeyExported = $false
    PublicCertificate = $publicCertificatePath
    PublicCertificateSha256 = $publicCertificateSha256
    GeneratedUtc = [DateTimeOffset]::UtcNow.ToString('o')
} |
    ConvertTo-Json -Depth 4 |
    Set-Content -LiteralPath $metadataPath -Encoding utf8

[pscustomobject]@{
    CertificateSubject = $certificate.Subject
    CertificateThumbprint = $certificate.Thumbprint
    CertificateNotAfter = $certificate.NotAfter
    PrivateKeyStore = "Cert:\CurrentUser\My\$($certificate.Thumbprint)"
    PrivateKeyExported = $false
    PublicCertificate = $publicCertificatePath
    PublicCertificateSha256 = $publicCertificateSha256
    Metadata = $metadataPath
}
