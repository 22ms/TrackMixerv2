$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$fixtureDir = Join-Path $root "TrackMixerv2.UITests\Fixtures"
$singleTrackFixturePath = Join-Path $fixtureDir "uitest-clip-single.mp4"

New-Item -ItemType Directory -Force -Path $fixtureDir | Out-Null

$ffmpeg = Get-Command ffmpeg -ErrorAction SilentlyContinue
if (-not $ffmpeg) {
    throw "ffmpeg not found in PATH. Install with: winget install -e --id Gyan.FFmpeg"
}

# Four audio tracks like an OBS replay, each a pure sine at a distinct frequency so tests
# can tell from decoded samples which track a player is actually rendering.
# Keep in sync with TrackMixerv2.NativeTests/Fixtures.cs and TrackMixerPaths.cs.
$frequencies = 220, 440, 660, 880
$titles = "Mic", "Discord", "Game", "System"
$seconds = 20

foreach ($container in "mkv", "mp4") {
    $path = Join-Path $fixtureDir "uitest-4track.$container"
    $inputs = @("-f", "lavfi", "-i", "testsrc2=s=320x180:r=30:d=$seconds")
    $maps = @("-map", "0:v:0")
    $metadata = @()
    for ($i = 0; $i -lt $frequencies.Count; $i++) {
        $inputs += @("-f", "lavfi", "-i", "sine=frequency=$($frequencies[$i]):sample_rate=48000:duration=$seconds")
        $maps += @("-map", "$($i + 1):a:0")
        $metadata += @("-metadata:s:a:$i", "title=$($titles[$i])")
    }

    $extra = @()
    if ($container -eq "mp4") { $extra = @("-movflags", "+faststart") }

    & $ffmpeg.Source -v error -y @inputs @maps `
        -c:v libx264 -preset veryfast -crf 38 -pix_fmt yuv420p `
        -c:a aac -b:a 48k -ac 2 `
        @metadata @extra $path
    Write-Host "Wrote $path ($((Get-Item $path).Length) bytes, $($frequencies.Count) audio tracks, $seconds s)"
}

& $ffmpeg.Source -v error -y `
    -f lavfi -i "color=c=blue:s=160x120:d=2" `
    -f lavfi -i "sine=frequency=660:duration=2" `
    -map 0:v:0 -map 1:a:0 `
    -c:v libx264 -pix_fmt yuv420p -profile:v baseline `
    -c:a aac -b:a 64k `
    -metadata:s:a:0 title=Single `
    -movflags +faststart `
    $singleTrackFixturePath
Write-Host "Wrote $singleTrackFixturePath ($((Get-Item $singleTrackFixturePath).Length) bytes, 1 audio track)"
