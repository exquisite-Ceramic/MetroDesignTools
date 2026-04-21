#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Run a fuller SectionGenerator host acceptance test with accoreconsole.

.DESCRIPTION
    The script copies plugin output into an ASCII-only temp folder and then:
    1. NETLOAD MetroToolKits.Bootstrap.dll
    2. Run SectionHostAcceptance
    3. Check the log for SECTION_HOST_ACCEPTANCE:OK

    This validates the Generate -> Snapshot -> FindRelated -> LocateSource ->
    CheckUpdates -> Update chain inside the AutoCAD host.

.PARAMETER AcadConsolePath
    Path to accoreconsole.exe.

.PARAMETER DwgPath
    DWG path used to start the host.

.PARAMETER Configuration
    Build configuration. Default is Debug.

.PARAMETER WorkingDirectory
    Temporary host test directory. Default is %TEMP%\MetroToolKitsHostAcceptance.

.PARAMETER IsolateUserId
    accoreconsole isolate user id. Default is MetroToolKitsHostAcceptance.

.PARAMETER IsolateUserDataFolder
    accoreconsole isolate profile folder. Default is <WorkingDirectory>\profile.
#>

param(
    [string]$AcadConsolePath = "C:\Program Files\Autodesk\AutoCAD 2025\accoreconsole.exe",
    [string]$DwgPath = "C:\Program Files\Autodesk\AutoCAD 2025\Express\brkline.dwg",
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [string]$WorkingDirectory = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "MetroToolKitsHostAcceptance"),
    [string]$IsolateUserId = "MetroToolKitsHostAcceptance",
    [string]$IsolateUserDataFolder
)

if ($PSVersionTable.PSEdition -ne "Core") {
    $pwsh = Get-Command pwsh -ErrorAction SilentlyContinue
    if (-not $pwsh) {
        throw "PowerShell 7 (pwsh) is required to run host-acceptance-test.ps1."
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
$scriptPath = Join-Path $WorkingDirectory "section-host-acceptance.scr"
$logPath = Join-Path $WorkingDirectory "accoreconsole-host-acceptance.log"
$netloadPath = (Join-Path $WorkingDirectory "MetroToolKits.Bootstrap.dll").Replace('\', '/')

if ([string]::IsNullOrWhiteSpace($IsolateUserDataFolder)) {
    $IsolateUserDataFolder = Join-Path $WorkingDirectory "profile"
}

function Write-Step([string]$message) {
    Write-Host "`n==> $message" -ForegroundColor Cyan
}

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

if (-not (Test-Path $AcadConsolePath)) {
    throw "accoreconsole.exe was not found: $AcadConsolePath"
}

if (-not (Test-Path $DwgPath)) {
    throw "DWG file was not found: $DwgPath"
}

if (-not (Test-Path $pluginOutput)) {
    throw "Plugin output folder was not found: $pluginOutput`nRun dotnet build first."
}

Write-Step "Preparing host acceptance directory"
if (Test-Path $WorkingDirectory) {
    Remove-Item -LiteralPath $WorkingDirectory -Recurse -Force
}

New-Item -ItemType Directory -Path $WorkingDirectory | Out-Null
New-Item -ItemType Directory -Path $IsolateUserDataFolder -Force | Out-Null
Copy-Item -Path (Join-Path $pluginOutput "*") -Destination $WorkingDirectory -Recurse -Force

@"
(setvar "SECURELOAD" 0)
(command "_.NETLOAD" "$netloadPath")
SectionHostAcceptance
(princ)
"@ | Set-Content -LiteralPath $scriptPath -Encoding ASCII

Write-Step "Running accoreconsole host acceptance"
& $AcadConsolePath /i $DwgPath /s $scriptPath /isolate $IsolateUserId $IsolateUserDataFolder *> $logPath
if ($LASTEXITCODE -ne 0) {
    throw "accoreconsole failed with exit code: $LASTEXITCODE"
}

Write-Step "Checking acceptance result"
$logBytes = [System.IO.File]::ReadAllBytes($logPath)
$okUtf16 = [System.Text.Encoding]::Unicode.GetBytes("SECTION_HOST_ACCEPTANCE:OK")
$okAscii = [System.Text.Encoding]::ASCII.GetBytes("SECTION_HOST_ACCEPTANCE:OK")

if ((Test-BytePattern $logBytes $okUtf16) -or (Test-BytePattern $logBytes $okAscii)) {
    Write-Host "Host acceptance passed: SectionHostAcceptance returned OK" -ForegroundColor Green
    return
}

throw "Host acceptance failed: SECTION_HOST_ACCEPTANCE:OK was not found in the log. Log: $logPath"
