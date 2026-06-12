param(
    [string]$AssetsDir = (Join-Path $PSScriptRoot "..\TrackMixerv2\Assets"),
    [string]$TintColor = "#F28C28",
    [int]$ColorizePercent = 42
)

if (-not (Get-Command magick -ErrorAction SilentlyContinue)) {
    throw "ImageMagick is required. Install with: winget install ImageMagick.ImageMagick"
}

Get-ChildItem $AssetsDir -Filter *.png |
    Where-Object { $_.Name -notlike '*.backup.png' } |
    ForEach-Object {
        magick $_.FullName -fill $TintColor -colorize "${ColorizePercent}%" $_.FullName
        Write-Host "Tinted $($_.Name)"
    }

$appIcon = Join-Path $AssetsDir "trackmixerv2.ico"
if (Test-Path $appIcon) {
    magick $appIcon -fill $TintColor -colorize "${ColorizePercent}%" $appIcon
    Write-Host "Tinted trackmixerv2.ico"
}
