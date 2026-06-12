param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent

dotnet test "$root\TrackMixerv2.ScenarioTests\TrackMixerv2.ScenarioTests.csproj" -c $Configuration
