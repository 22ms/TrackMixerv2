param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent

dotnet test "$root\TrackMixerv2.ScenarioTests\TrackMixerv2.ScenarioTests.csproj" -c $Configuration -p:Platform=x64
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# Real Media Foundation decoding and muted MediaPlayers; needs no window and plays no sound.
dotnet test "$root\TrackMixerv2.NativeTests\TrackMixerv2.NativeTests.csproj" -c $Configuration -p:Platform=x64
exit $LASTEXITCODE
