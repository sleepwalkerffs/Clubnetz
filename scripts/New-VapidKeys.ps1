# Creates a VAPID key pair for Web Push (push notifications of the installed app).
#
# Run it ONCE per environment and store the values as app settings (Azure: Push__PublicKey,
# Push__PrivateKey, Push__Subject). Never replace the keys of a running environment: the
# subscriptions of all devices are bound to the public key and would stop working.
#
# Requires PowerShell 7+:  pwsh ./scripts/New-VapidKeys.ps1

function ConvertTo-Base64Url([byte[]] $bytes) {
    [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

$ecdsa = [System.Security.Cryptography.ECDsa]::Create([System.Security.Cryptography.ECCurve+NamedCurves]::nistP256)
$parameters = $ecdsa.ExportParameters($true)

# Public key: uncompressed P-256 point (0x04 || X || Y)
$publicKey = [byte[]](, 0x04 + $parameters.Q.X + $parameters.Q.Y)

Write-Output "Push__PublicKey  = $(ConvertTo-Base64Url $publicKey)"
Write-Output "Push__PrivateKey = $(ConvertTo-Base64Url $parameters.D)"
Write-Output "Push__Subject    = mailto:<your contact address>"
