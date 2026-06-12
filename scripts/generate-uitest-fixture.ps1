$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$fixtureDir = Join-Path $root "TrackMixerv2.UITests\Fixtures"
$multiTrackFixturePath = Join-Path $fixtureDir "uitest-clip.mp4"
$singleTrackFixturePath = Join-Path $fixtureDir "uitest-clip-single.mp4"

New-Item -ItemType Directory -Force -Path $fixtureDir | Out-Null

$ffmpeg = Get-Command ffmpeg -ErrorAction SilentlyContinue
if (-not $ffmpeg) {
    throw "ffmpeg not found in PATH. Install with: winget install -e --id Gyan.FFmpeg"
}

& $ffmpeg.Source -y `
    -f lavfi -i "color=c=black:s=160x120:d=1" `
    -f lavfi -i "sine=frequency=440:duration=1" `
    -f lavfi -i "sine=frequency=880:duration=1" `
    -f lavfi -i "sine=frequency=220:duration=1" `
    -map 0:v:0 -map 1:a:0 -map 2:a:0 -map 3:a:0 `
    -c:v libx264 -pix_fmt yuv420p -profile:v baseline `
    -c:a aac -b:a 64k `
    -metadata:s:a:0 title=Game `
    -metadata:s:a:1 title=Mic `
    -metadata:s:a:2 title=Discord `
    -movflags +faststart `
    $multiTrackFixturePath

& $ffmpeg.Source -y `
    -f lavfi -i "color=c=blue:s=160x120:d=1" `
    -f lavfi -i "sine=frequency=660:duration=1" `
    -map 0:v:0 -map 1:a:0 `
    -c:v libx264 -pix_fmt yuv420p -profile:v baseline `
    -c:a aac -b:a 64k `
    -metadata:s:a:0 title=Single `
    -movflags +faststart `
    $singleTrackFixturePath

Write-Host "Wrote $multiTrackFixturePath ($((Get-Item $multiTrackFixturePath).Length) bytes, 3 audio tracks)"
Write-Host "Wrote $singleTrackFixturePath ($((Get-Item $singleTrackFixturePath).Length) bytes, 1 audio track)"
