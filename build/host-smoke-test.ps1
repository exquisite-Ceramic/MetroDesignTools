#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Run a minimal SectionGenerator host smoke test with accoreconsole.

.DESCRIPTION
    The script copies plugin output into an ASCII-only temp folder and then:
    1. NETLOAD MetroToolKits.Bootstrap.dll
    2. Run SectionSelfTest
    3. Check the log for SECTION_SELF_TEST:OK

    This validates the AutoCAD host entry, Bootstrap initialization,
    command bridging, DI registration, and key service resolution.

.PARAMETER AcadConsolePath
    Path to accoreconsole.exe.

.PARAMETER DwgPath
    DWG path used to start the host.

.PARAMETER Configuration
    Build configuration. Default is Debug.

.PARAMETER WorkingDirectory
    Temporary host test directory. Default is E:\MetroToolKitsHostTest.
#>

param(
    [string]$AcadConsolePath = "C:\Program Files\Autodesk\AutoCAD 2025\accoreconsole.exe",
    [string]$DwgPath = "C:\Program Files\Autodesk\AutoCAD 2025\Express\brkline.dwg",
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [string]$WorkingDirectory = "E:\MetroToolKitsHostTest"
)

if ($PSVersionTable.PSEdition -ne "Core") {
    $pwsh = Get-Command pwsh -ErrorAction SilentlyContinue
    if (-not $pwsh) {
        throw "PowerShell 7 (pwsh) is required to run host-smoke-test.ps1."
    }

    $forwardArgs = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", $PSCommandPath,
        "-AcadConsolePath", $AcadConsolePath,
        "-DwgPath", $DwgPath,
        "-Configuration", $Configuration,
        "-WorkingDirectory", $WorkingDirectory
    )

    & $pwsh.Source @forwardArgs
    exit $LASTEXITCODE
}

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$pluginOutput = Join-Path $repoRoot "src\Plugins\SectionGenerator\MetroToolKits.SectionGenerator.Plugin\bin\$Configuration\net8.0-windows"
$scriptPath = Join-Path $WorkingDirectory "section-selftest.scr"
$logPath = Join-Path $WorkingDirectory "accoreconsole-selftest.log"

function Write-Step([string]$message) {
    Write-Host "`n==> $message" -ForegroundColor Cyan
}

if (-not (Test-Path $AcadConsolePath)) {
    throw "accoreconsole.exe was not found: $AcadConsolePath"
}

if (-not (Test-Path $DwgPath)) {
    throw "DWG file was not found: $DwgPath"
}

if (-not (Test-Path $pluginOutput)) {
    throw "Plugin output folder was not found: $pluginOutput`nRun dotnet build first."
}

Write-Step "Preparing host test directory"
if (Test-Path $WorkingDirectory) {
    Remove-Item -LiteralPath $WorkingDirectory -Recurse -Force
}

New-Item -ItemType Directory -Path $WorkingDirectory | Out-Null
Copy-Item -Path (Join-Path $pluginOutput "*") -Destination $WorkingDirectory -Recurse -Force

@'
(setvar "SECURELOAD" 0)
(command "_.NETLOAD" "E:/MetroToolKitsHostTest/MetroToolKits.Bootstrap.dll")
SectionSelfTest
(princ)
'@ | Set-Content -LiteralPath $scriptPath -Encoding ASCII

Write-Step "Running accoreconsole host smoke test"
& $AcadConsolePath /i $DwgPath /s $scriptPath *> $logPath
if ($LASTEXITCODE -ne 0) {
    throw "accoreconsole failed with exit code: $LASTEXITCODE"
}

Write-Step "Checking self-test result"
function Test-BytePattern([byte[]]$Haystack, [byte[]]$Needle) {
    for ($i = 0; $i -le $Haystack.Length - $Needle.Length; $i++) {
        $matched = $true
        for ($j = 0; $j -lt $Needle.Length; $j++) {
            if ($Haystack[$i + $j] -ne $Needle[$j]) {
                $matched = $false
                break
            }
        }

        if ($matched) {
            return $true
        }
    }

    return $false
}

$logBytes = [System.IO.File]::ReadAllBytes($logPath)
$okUtf16 = [System.Text.Encoding]::Unicode.GetBytes("SECTION_SELF_TEST:OK")
$okAscii = [System.Text.Encoding]::ASCII.GetBytes("SECTION_SELF_TEST:OK")

if ((Test-BytePattern $logBytes $okUtf16) -or (Test-BytePattern $logBytes $okAscii)) {
    Write-Host "Host smoke test passed: SectionSelfTest returned OK" -ForegroundColor Green
    return
}

throw "Host smoke test failed: SECTION_SELF_TEST:OK was not found in the log. Log: $logPath"
