#!/usr/bin/env pwsh
<#
.SYNOPSIS
    使用 accoreconsole 对 SectionGenerator 做最小宿主烟测。

.DESCRIPTION
    该脚本会将插件输出复制到纯 ASCII 临时目录，随后通过 accoreconsole：
    1. NETLOAD MetroToolKits.SectionGenerator.Plugin.dll
    2. 执行 SectionSelfTest
    3. 检查日志中是否包含 SECTION_SELF_TEST:OK

    用于验证 AutoCAD 宿主入口、Bootstrap 初始化、DI 注册和关键服务解析是否正常。

.PARAMETER AcadConsolePath
    accoreconsole.exe 路径。

.PARAMETER DwgPath
    用于启动宿主的 DWG 文件路径。

.PARAMETER Configuration
    编译配置，默认 Debug。

.PARAMETER WorkingDirectory
    临时宿主测试目录，默认位于仓库外的 E:\MetroToolKitsHostTest。
#>

param(
    [string]$AcadConsolePath = "C:\Program Files\Autodesk\AutoCAD 2025\accoreconsole.exe",
    [string]$DwgPath = "C:\Program Files\Autodesk\AutoCAD 2025\Express\brkline.dwg",
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [string]$WorkingDirectory = "E:\MetroToolKitsHostTest"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$pluginOutput = Join-Path $repoRoot "src\Plugins\SectionGenerator\MetroToolKits.SectionGenerator.Plugin\bin\$Configuration\net8.0-windows"
$scriptPath = Join-Path $WorkingDirectory "section-selftest.scr"
$logPath = Join-Path $WorkingDirectory "accoreconsole-selftest.log"

function Write-Step([string]$message) {
    Write-Host "`n==> $message" -ForegroundColor Cyan
}

if (-not (Test-Path $AcadConsolePath)) {
    throw "未找到 accoreconsole.exe: $AcadConsolePath"
}

if (-not (Test-Path $DwgPath)) {
    throw "未找到 DWG 文件: $DwgPath"
}

if (-not (Test-Path $pluginOutput)) {
    throw "未找到插件输出目录: $pluginOutput`n请先执行 dotnet build。"
}

Write-Step "准备宿主测试目录"
if (Test-Path $WorkingDirectory) {
    Remove-Item -LiteralPath $WorkingDirectory -Recurse -Force
}

New-Item -ItemType Directory -Path $WorkingDirectory | Out-Null
Copy-Item -Path (Join-Path $pluginOutput "*") -Destination $WorkingDirectory -Recurse -Force

@'
(setvar "SECURELOAD" 0)
(command "_.NETLOAD" "E:/MetroToolKitsHostTest/MetroToolKits.SectionGenerator.Plugin.dll")
SectionSelfTest
(princ)
'@ | Set-Content -LiteralPath $scriptPath -Encoding ASCII

Write-Step "执行 accoreconsole 宿主烟测"
& $AcadConsolePath /i $DwgPath /s $scriptPath *> $logPath
if ($LASTEXITCODE -ne 0) {
    throw "accoreconsole 执行失败，退出码: $LASTEXITCODE"
}

Write-Step "检查自检结果"
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
    Write-Host "宿主烟测通过：SectionSelfTest 返回 OK" -ForegroundColor Green
    return
}

throw "宿主烟测失败：日志中未找到 SECTION_SELF_TEST:OK。日志位置: $logPath"
