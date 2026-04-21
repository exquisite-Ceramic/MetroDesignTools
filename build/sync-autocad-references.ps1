#!/usr/bin/env pwsh
<#
.SYNOPSIS
    同步本机 AutoCAD 托管引用到 build/References/

.DESCRIPTION
    从指定或自动发现的 AutoCAD 安装目录复制 acdbmgd.dll、acmgd.dll、
    accoremgd.dll 到仓库的 build/References/，用于本地编译。

.PARAMETER AutoCADDir
    AutoCAD 安装目录。可传入类似：
    C:\Program Files\Autodesk\AutoCAD 2025
#>

param(
    [string]$AutoCADDir
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot
$targetDir = Join-Path $repoRoot "build\References"
$requiredFiles = @(
    "acdbmgd.dll",
    "acmgd.dll",
    "accoremgd.dll"
)

function Resolve-AutoCADDirectory {
    param([string]$PreferredDirectory)

    if ($PreferredDirectory) {
        return (Resolve-Path -LiteralPath $PreferredDirectory).Path
    }

    $candidates = @(
        "C:\Program Files\Autodesk\AutoCAD 2025",
        "C:\Program Files\Autodesk\AutoCAD 2024",
        "C:\Program Files\Autodesk\AutoCAD 2023",
        "C:\Program Files\Autodesk\AutoCAD 2022",
        "C:\Program Files\Autodesk\AutoCAD 2021",
        "C:\Program Files\Autodesk\AutoCAD 2020",
        "C:\Program Files\Autodesk\AutoCAD 2019",
        "C:\Program Files\Autodesk\AutoCAD 2018"
    )

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    throw "未找到 AutoCAD 安装目录，请使用 -AutoCADDir 指定，例如：C:\Program Files\Autodesk\AutoCAD 2025"
}

function Copy-ReferenceIfNeeded {
    param(
        [string]$SourcePath,
        [string]$DestinationPath
    )

    if (-not (Test-Path -LiteralPath $DestinationPath)) {
        Copy-Item -LiteralPath $SourcePath -Destination $DestinationPath -Force
        return "copied"
    }

    $sourceInfo = Get-Item -LiteralPath $SourcePath
    $destinationInfo = Get-Item -LiteralPath $DestinationPath

    if ($sourceInfo.Length -ne $destinationInfo.Length -or
        $sourceInfo.LastWriteTimeUtc -gt $destinationInfo.LastWriteTimeUtc) {
        Copy-Item -LiteralPath $SourcePath -Destination $DestinationPath -Force
        return "updated"
    }

    return "unchanged"
}

$resolvedAutoCADDir = Resolve-AutoCADDirectory -PreferredDirectory $AutoCADDir
New-Item -ItemType Directory -Path $targetDir -Force | Out-Null

Write-Host "使用 AutoCAD 引用目录: $resolvedAutoCADDir" -ForegroundColor Cyan

$results = foreach ($file in $requiredFiles) {
    $sourcePath = Join-Path $resolvedAutoCADDir $file
    $destinationPath = Join-Path $targetDir $file

    if (-not (Test-Path -LiteralPath $sourcePath)) {
        throw "缺少必需文件: $sourcePath"
    }

    [PSCustomObject]@{
        File = $file
        Status = Copy-ReferenceIfNeeded -SourcePath $sourcePath -DestinationPath $destinationPath
    }
}

$results | Format-Table -AutoSize | Out-Host
Write-Host "AutoCAD 引用已就绪: $targetDir" -ForegroundColor Green
