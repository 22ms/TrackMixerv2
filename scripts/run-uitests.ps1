param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent

Write-Host "Building TrackMixerv2 for UI automation (unpackaged)..."
dotnet build "$root\TrackMixerv2\TrackMixerv2.csproj" -c $Configuration -p:Platform=x64 -p:UiTestBuild=true

Write-Host "Running UI automation tests..."
dotnet test "$root\TrackMixerv2.UITests\TrackMixerv2.UITests.csproj" -c $Configuration -p:Platform=x64 --filter "Category=UI"
