param(
    [string]$Destination = (Join-Path $PSScriptRoot '..\v2rayN\runtime-assets\cores')
)

$ErrorActionPreference = 'Stop'
$destinationPath = [System.IO.Path]::GetFullPath($Destination)
$temporaryRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("new-eridu-cores-" + [guid]::NewGuid().ToString('N'))

function Get-VerifiedArchive {
    param(
        [string]$Uri,
        [string]$Sha256,
        [string]$OutputPath
    )

    Invoke-WebRequest -Uri $Uri -OutFile $OutputPath -UseBasicParsing
    $actualHash = (Get-FileHash -LiteralPath $OutputPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -ne $Sha256.ToLowerInvariant()) {
        throw "SHA-256 mismatch for $Uri. Expected $Sha256, got $actualHash."
    }
}

try {
    New-Item -ItemType Directory -Path $temporaryRoot | Out-Null
    $xrayArchive = Join-Path $temporaryRoot 'xray.zip'
    $singBoxArchive = Join-Path $temporaryRoot 'sing-box.zip'
    $xrayExtract = Join-Path $temporaryRoot 'xray'
    $singBoxExtract = Join-Path $temporaryRoot 'sing-box'

    Get-VerifiedArchive `
        -Uri 'https://github.com/XTLS/Xray-core/releases/download/v26.10.10/Xray-windows-64.zip' `
        -Sha256 'bfefce8e9804e53b7d75b7c49d958b34e89890d9d5e672329089d1b3111647af' `
        -OutputPath $xrayArchive
    Get-VerifiedArchive `
        -Uri 'https://github.com/SagerNet/sing-box/releases/download/v1.14.3/sing-box-1.14.3-windows-amd64.zip' `
        -Sha256 'd8b86ef58936dba9230ec6a57ffa4243933f3c14ef4809828b6c553440ed1518' `
        -OutputPath $singBoxArchive

    Expand-Archive -LiteralPath $xrayArchive -DestinationPath $xrayExtract
    Expand-Archive -LiteralPath $singBoxArchive -DestinationPath $singBoxExtract

    $xrayDestination = Join-Path $destinationPath 'xray'
    $singBoxDestination = Join-Path $destinationPath 'sing_box'
    $sharedDestination = Join-Path $destinationPath 'shared'
    New-Item -ItemType Directory -Force -Path $xrayDestination, $singBoxDestination, $sharedDestination | Out-Null

    Copy-Item -LiteralPath (Join-Path $xrayExtract 'xray.exe') -Destination (Join-Path $xrayDestination 'xray.exe') -Force
    Copy-Item -LiteralPath (Join-Path $xrayExtract 'geoip.dat') -Destination (Join-Path $sharedDestination 'geoip.dat') -Force
    Copy-Item -LiteralPath (Join-Path $xrayExtract 'geosite.dat') -Destination (Join-Path $sharedDestination 'geosite.dat') -Force

    $singBoxExe = Get-ChildItem -LiteralPath $singBoxExtract -Recurse -Filter 'sing-box.exe' | Select-Object -First 1
    if ($null -eq $singBoxExe) {
        throw 'sing-box.exe was not found in the verified archive.'
    }
    Copy-Item -LiteralPath $singBoxExe.FullName -Destination (Join-Path $singBoxDestination 'sing-box.exe') -Force

    Write-Output "Prepared Xray v26.10.10 and sing-box v1.14.3 under $destinationPath"
}
finally {
    $resolvedTempRoot = [System.IO.Path]::GetFullPath($temporaryRoot)
    $systemTempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
    if ($resolvedTempRoot.StartsWith($systemTempRoot, [System.StringComparison]::OrdinalIgnoreCase) -and
        (Test-Path -LiteralPath $resolvedTempRoot)) {
        Remove-Item -LiteralPath $resolvedTempRoot -Recurse -Force
    }
}
