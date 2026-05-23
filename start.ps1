# JobScout launcher — Windows / PowerShell
# Requires .NET 10 SDK. Boots the API (which hosts the Blazor UI) and opens a browser.

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "❌ .NET is not installed." -ForegroundColor Red
    Write-Host "   Download .NET 10 from https://dotnet.microsoft.com/download and try again."
    exit 1
}

$sdks = (dotnet --list-sdks) -split "`n"
$latestMajor = ($sdks | ForEach-Object {
    if ($_ -match '^(\d+)\.') { [int]$Matches[1] }
} | Sort-Object -Descending | Select-Object -First 1)

if (-not $latestMajor -or $latestMajor -lt 10) {
    Write-Host "❌ .NET 10 SDK or newer is required." -ForegroundColor Red
    Write-Host "   Installed SDKs:"
    $sdks | ForEach-Object { Write-Host "     $_" }
    exit 1
}

Write-Host "▶ Restoring dependencies…"
dotnet restore | Out-Null

Write-Host "▶ Building…"
dotnet build --no-restore --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$url = if ($env:JOBSCOUT_URL) { $env:JOBSCOUT_URL } else { "http://localhost:5000" }
Write-Host "▶ Starting JobScout at $url" -ForegroundColor Green

# Open browser after a short delay in a background job.
Start-Job -ScriptBlock {
    param($u)
    Start-Sleep -Seconds 3
    Start-Process $u
} -ArgumentList $url | Out-Null

dotnet run --no-build --project src/JobScout.Api/JobScout.Api.csproj --launch-profile JobScout
