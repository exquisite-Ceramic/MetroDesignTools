#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Run a minimal SectionGenerator host smoke test with accoreconsole.

.DESCRIPTION
    The script copies plugin output into an ASCII-only temp folder and then:
    1. NETLOAD MetroToolKits.Bootstrap.dll
    2. Run the internal host self-test command
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
    Temporary host test directory. Default is %TEMP%\MetroToolKitsHostTest.

.PARAMETER IsolateUserId
    accoreconsole isolate user id. Default is MetroToolKitsHostTest.

.PARAMETER IsolateUserDataFolder
    accoreconsole isolate profile folder. Default is <WorkingDirectory>\profile.
#>

param(
    [string]$AcadConsolePath = "C:\Program Files\Autodesk\AutoCAD 2025\accoreconsole.exe",
    [string]$DwgPath = "C:\Program Files\Autodesk\AutoCAD 2025\Express\brkline.dwg",
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [string]$WorkingDirectory = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "MetroToolKitsHostTest"),
    [string]$IsolateUserId = "MetroToolKitsHostTest",
    [string]$IsolateUserDataFolder
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
$packageOutput = Join-Path $repoRoot "publish\SectionGenerator"
$hostBootstrapOutput = Join-Path $repoRoot "publish\HostAutomationBootstrap"
$scriptPath = Join-Path $WorkingDirectory "section-selftest.scr"
$logPath = Join-Path $WorkingDirectory "accoreconsole-selftest.log"
$netloadPath = (Join-Path $WorkingDirectory "MetroToolKits.Bootstrap.dll").Replace('\', '/')

if ([string]::IsNullOrWhiteSpace($IsolateUserDataFolder)) {
    $IsolateUserDataFolder = Join-Path $WorkingDirectory "profile"
}

function Write-Step([string]$message) {
    Write-Host "`n==> $message" -ForegroundColor Cyan
}

function Find-PluginLogSuccess([string]$token, [datetime]$startedAt) {
    $packagesRoot = Join-Path $env:LOCALAPPDATA "MetroToolKits\Packages"
    if (-not (Test-Path $packagesRoot)) {
        return $false
    }

    $candidateLogs = Get-ChildItem -Path $packagesRoot -Recurse -Filter "MetroToolKits.log" -File |
        Where-Object { $_.LastWriteTime -ge $startedAt.AddMinutes(-1) } |
        Sort-Object LastWriteTime -Descending

    foreach ($logFile in $candidateLogs) {
        $content = Get-Content -LiteralPath $logFile.FullName -Raw -ErrorAction SilentlyContinue
        if ($null -ne $content -and $content.Contains($token)) {
            return $true
        }
    }

    return $false
}

if (-not (Test-Path $AcadConsolePath)) {
    throw "accoreconsole.exe was not found: $AcadConsolePath"
}

if (-not (Test-Path $DwgPath)) {
    throw "DWG file was not found: $DwgPath"
}

if (-not (Test-Path $packageOutput)) {
    throw "Publish output folder was not found: $packageOutput`nRun build.ps1 publish first."
}

if (-not (Test-Path $hostBootstrapOutput)) {
    throw "Host automation Bootstrap output was not found: $hostBootstrapOutput`nRun build.ps1 publish first."
}

Write-Step "Preparing host test directory"
if (Test-Path $WorkingDirectory) {
    Remove-Item -LiteralPath $WorkingDirectory -Recurse -Force
}

New-Item -ItemType Directory -Path $WorkingDirectory | Out-Null
New-Item -ItemType Directory -Path $IsolateUserDataFolder -Force | Out-Null
Copy-Item -Path (Join-Path $packageOutput "*") -Destination $WorkingDirectory -Recurse -Force
Copy-Item -Path (Join-Path $hostBootstrapOutput "*") -Destination $WorkingDirectory -Recurse -Force

@"
(setvar "SECURELOAD" 0)
(command "_.NETLOAD" "$netloadPath")
(command "MKSectionSelfTestInternal")
(princ)
"@ | Set-Content -LiteralPath $scriptPath -Encoding ASCII

Write-Step "Running accoreconsole host smoke test"
$startedAt = Get-Date
& $AcadConsolePath /i $DwgPath /s $scriptPath /isolate $IsolateUserId $IsolateUserDataFolder *> $logPath
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

if (Find-PluginLogSuccess -token "SECTION_SELF_TEST:OK" -startedAt $startedAt) {
    Write-Host "Host smoke test passed: SectionSelfTest returned OK (from plugin log)" -ForegroundColor Green
    return
}

throw "Host smoke test failed: SECTION_SELF_TEST:OK was not found in the log. Log: $logPath"
