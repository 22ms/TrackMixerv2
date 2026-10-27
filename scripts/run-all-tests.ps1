param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [switch]$SkipUi
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent

& "$root\scripts\run-scenario-tests.ps1" -Configuration $Configuration
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if (-not $SkipUi) {
    & "$root\scripts\run-uitests.ps1" -Configuration $Configuration
}
