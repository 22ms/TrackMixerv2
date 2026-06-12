param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [string]$ClipPath
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$exe = Join-Path $root "TrackMixerv2\bin\x64\$Configuration\net10.0-windows10.0.26100.0\win-x64\TrackMixerv2.exe"

Write-Host "Building unpackaged UI-test binary..."
dotnet build "$root\TrackMixerv2\TrackMixerv2.csproj" -c $Configuration -p:Platform=x64 -p:UiTestBuild=true | Out-Null

if (-not (Test-Path $exe)) {
    throw "Build output not found: $exe"
}

Get-Process TrackMixerv2 -ErrorAction SilentlyContinue | Stop-Process -Force

$env:TRACKMIXER_UITEST = "1"
$env:TRACKMIXER_SUPPRESS_ROOT_PROMPT = "1"
if ($ClipPath) {
    $env:TRACKMIXER_LAUNCH_FILE = (Resolve-Path $ClipPath).Path
    $env:TRACKMIXER_ROOT_FOLDER = Split-Path $env:TRACKMIXER_LAUNCH_FILE -Parent
}

Write-Host "Starting TrackMixerv2 (title should be 'Track Mixer UI Test')..."
Write-Host "Exe: $exe"
$proc = Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe) -PassThru

$stableSeconds = 0
for ($i = 1; $i -le 15; $i++) {
    Start-Sleep -Seconds 1
    $alive = Get-Process TrackMixerv2 -ErrorAction SilentlyContinue
    if ($proc.HasExited -and -not $alive) {
        $crashLog = Join-Path $env:TEMP "TrackMixer-uitest-crash.txt"
        $detail = if (Test-Path $crashLog) { Get-Content $crashLog -Raw } else { "no managed crash log" }
        $code = if ($proc.ExitCode -ne $null) { "0x{0:X}" -f [uint32]$proc.ExitCode } else { "unknown" }
        throw "TrackMixerv2 exited after $i second(s) (exit $code).`n$detail"
    }

    if ($alive) {
        $stableSeconds++
        if ($stableSeconds -ge 3) {
            $titles = ($alive | ForEach-Object { if ($_.MainWindowTitle) { $_.MainWindowTitle } }) -join ", "
            Write-Host "Process alive for $stableSeconds s. PIDs: $($alive.Id -join ', '). Window title(s): '$titles'"
            break
        }
    }
}

if ($stableSeconds -lt 3) {
    throw "TrackMixerv2 did not stay running for 3 seconds."
}

Write-Host "You should see the window now. Press Enter here to close the app."
[void][System.Console]::ReadLine()

Get-Process TrackMixerv2 -ErrorAction SilentlyContinue | Stop-Process -Force
Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue

Remove-Item Env:TRACKMIXER_UITEST -ErrorAction SilentlyContinue
Remove-Item Env:TRACKMIXER_SUPPRESS_ROOT_PROMPT -ErrorAction SilentlyContinue
Remove-Item Env:TRACKMIXER_LAUNCH_FILE -ErrorAction SilentlyContinue
Remove-Item Env:TRACKMIXER_ROOT_FOLDER -ErrorAction SilentlyContinue
