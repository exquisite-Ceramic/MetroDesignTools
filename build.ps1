#!/usr/bin/env pwsh
<#
.SYNOPSIS
    MetroToolKits 构建脚本

.DESCRIPTION
    封装 restore / build / test / publish 操作。

.PARAMETER Action
    build   - 编译插件项目（默认）
    test    - 运行单元测试
    all     - 编译 + 测试
    publish - 发布插件到 ./publish/SectionGenerator
    clean   - 清理编译输出

.EXAMPLE
    .\build.ps1 build
    .\build.ps1 test
    .\build.ps1 all
    .\build.ps1 publish
#>

param(
    [Parameter(Position = 0)]
    [ValidateSet("build", "test", "all", "publish", "clean")]
    [string]$Action = "build",

    [string]$AutoCADDir
)

$ErrorActionPreference = "Stop"
if ($PSVersionTable.PSVersion.Major -ge 7) {
    $PSNativeCommandUseErrorActionPreference = $false
}

$SolutionPath = Join-Path $PSScriptRoot "MetroToolKits.sln"
$PublishOutput = Join-Path $PSScriptRoot "publish\SectionGenerator"
$HostBootstrapPublishOutput = Join-Path $PSScriptRoot "publish\HostAutomationBootstrap"
$BootstrapProject = Join-Path $PSScriptRoot "src\Bootstrap\MetroToolKits.Bootstrap\MetroToolKits.Bootstrap.csproj"
$BootstrapOutput = Join-Path $PSScriptRoot "src\Bootstrap\MetroToolKits.Bootstrap\bin\Release\net8.0-windows"
$PluginProject = Join-Path $PSScriptRoot "src\Plugins\SectionGenerator\MetroToolKits.SectionGenerator.Plugin\MetroToolKits.SectionGenerator.Plugin.csproj"
$PluginAssetsFile = Join-Path $PSScriptRoot "src\Plugins\SectionGenerator\MetroToolKits.SectionGenerator.Plugin\obj\project.assets.json"
$ReferenceSyncScript = Join-Path $PSScriptRoot "build\sync-autocad-references.ps1"

if (-not $env:SystemRoot) { $env:SystemRoot = "C:\Windows" }
if (-not $env:windir) { $env:windir = $env:SystemRoot }
if (-not $env:ComSpec) { $env:ComSpec = Join-Path $env:SystemRoot "System32\cmd.exe" }
if (-not $env:ProgramFiles) { $env:ProgramFiles = [Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFiles) }
if (-not ${env:ProgramFiles(x86)}) { ${env:ProgramFiles(x86)} = [Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFilesX86) }
$systemDrive = Split-Path -Qualifier $env:SystemRoot
if (-not $env:ProgramData) { $env:ProgramData = Join-Path $systemDrive "ProgramData" }

$userProfile = [Environment]::GetFolderPath([Environment+SpecialFolder]::UserProfile)
if (-not $env:USERPROFILE -and $userProfile) { $env:USERPROFILE = $userProfile }
if (-not $env:APPDATA) {
    $env:APPDATA = [Environment]::GetFolderPath([Environment+SpecialFolder]::ApplicationData)
    if (-not $env:APPDATA -and $env:USERPROFILE) { $env:APPDATA = Join-Path $env:USERPROFILE "AppData\Roaming" }
}
if (-not $env:LOCALAPPDATA) {
    $env:LOCALAPPDATA = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
    if (-not $env:LOCALAPPDATA -and $env:USERPROFILE) { $env:LOCALAPPDATA = Join-Path $env:USERPROFILE "AppData\Local" }
}
if ($env:USERPROFILE) {
    if (-not $env:HOMEDRIVE) { $env:HOMEDRIVE = Split-Path -Qualifier $env:USERPROFILE }
    if (-not $env:HOMEPATH) { $env:HOMEPATH = $env:USERPROFILE.Substring($env:HOMEDRIVE.Length) }
}

if (-not $env:DOTNET_CLI_HOME) {
    $env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot ".dotnet-cli"
}

$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
New-Item -ItemType Directory -Path $env:DOTNET_CLI_HOME -Force | Out-Null

function Write-Step([string]$msg) {
    Write-Host "`n==> $msg" -ForegroundColor Cyan
}

function Invoke-DotnetCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,

        [Parameter(Mandatory = $true)]
        [string]$FailureMessage
    )

    $process = Start-Process -FilePath "dotnet" -ArgumentList $Arguments -NoNewWindow -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        throw "$FailureMessage (exit code: $($process.ExitCode))"
    }
}

function Invoke-AutoCADReferenceSync {
    Write-Step "准备 AutoCAD 引用"
    if ($AutoCADDir) {
        & $ReferenceSyncScript -AutoCADDir $AutoCADDir
    }
    else {
        & $ReferenceSyncScript
    }
    if (-not $?) { throw "AutoCAD 引用同步失败" }
}

function Invoke-RestoreForBuild {
    Write-Step "还原插件依赖"
    Invoke-DotnetCommand -Arguments @("restore", $PluginProject, "--ignore-failed-sources") -FailureMessage "插件还原失败"
}

function Invoke-EnsurePluginAssets {
    if (Test-Path -LiteralPath $PluginAssetsFile) {
        Write-Step "复用现有插件 restore 结果"
        return
    }

    Invoke-RestoreForBuild
}

function Invoke-RestoreForTests {
    Write-Step "还原解决方案依赖"
    Invoke-DotnetCommand -Arguments @("restore", $SolutionPath) -FailureMessage "解决方案还原失败"
}

function Invoke-PluginBuild([string]$Configuration) {
    Write-Step "编译插件项目 ($Configuration)"
    Invoke-DotnetCommand -Arguments @("build", $PluginProject, "-c", $Configuration, "--no-restore") -FailureMessage "插件编译失败"
    Write-Host "编译成功" -ForegroundColor Green
}

function Invoke-BootstrapBuild {
    param(
        [string]$Configuration,
        [switch]$HostAutomation,
        [string]$OutputPath
    )

    $label = if ($HostAutomation) { "Bootstrap 项目 ($Configuration, host automation)" } else { "Bootstrap 项目 ($Configuration)" }
    Write-Step "编译 $label"

    $arguments = @("build", $BootstrapProject, "-c", $Configuration, "--no-restore")
    if ($HostAutomation) {
        $arguments += "-p:HostAutomationEnabled=true"
    }

    if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
        if (Test-Path -LiteralPath $OutputPath) {
            Remove-Item -LiteralPath $OutputPath -Recurse -Force
        }

        $arguments += "-o"
        $arguments += $OutputPath
    }

    Invoke-DotnetCommand -Arguments $arguments -FailureMessage "Bootstrap 编译失败"
    Write-Host "Bootstrap 编译成功" -ForegroundColor Green
}

function Invoke-SolutionBuild {
    Write-Step "编译解决方案"
    Invoke-DotnetCommand -Arguments @("build", $SolutionPath, "-c", "Debug", "--no-restore") -FailureMessage "解决方案编译失败"
    Write-Host "编译成功" -ForegroundColor Green
}

function Invoke-Test {
    Write-Step "运行单元测试（不依赖 AutoCAD）"
    Invoke-DotnetCommand -Arguments @(
        "test",
        $SolutionPath,
        "--filter", "FullyQualifiedName~MetroToolKits.Tests",
        "--no-build",
        "-v", "minimal"
    ) -FailureMessage "测试失败"
    Write-Host "所有测试通过" -ForegroundColor Green
}

function Invoke-Publish {
    Write-Step "发布插件到 $PublishOutput"
    Invoke-DotnetCommand -Arguments @(
        "publish",
        $PluginProject,
        "-c", "Release",
        "-o", $PublishOutput,
        "--no-restore"
    ) -FailureMessage "发布失败"

    Write-Step "补齐 Bootstrap 入口文件"
    $bootstrapArtifacts = @(
        "MetroToolKits.Bootstrap.dll",
        "MetroToolKits.Bootstrap.pdb",
        "MetroToolKits.Bootstrap.deps.json",
        "appsettings.json",
        "appsettings.Development.json"
    )

    foreach ($artifact in $bootstrapArtifacts) {
        $source = Join-Path $BootstrapOutput $artifact
        if (-not (Test-Path -LiteralPath $source)) {
            throw "缺少 Bootstrap 发布工件: $source"
        }

        Copy-Item -LiteralPath $source -Destination $PublishOutput -Force
    }

    Write-Step "导出宿主自动化专用 Bootstrap"
    Invoke-BootstrapBuild -Configuration "Release" -HostAutomation -OutputPath $HostBootstrapPublishOutput
    foreach ($artifact in $bootstrapArtifacts) {
        $source = Join-Path $HostBootstrapPublishOutput $artifact
        if (-not (Test-Path -LiteralPath $source)) {
            $fallbackSource = Join-Path $BootstrapOutput $artifact
            if (-not (Test-Path -LiteralPath $fallbackSource)) {
                throw "缺少宿主自动化 Bootstrap 工件: $source"
            }

            Copy-Item -LiteralPath $fallbackSource -Destination $HostBootstrapPublishOutput -Force
            continue
        }

        if ([System.IO.Path]::GetFullPath($source) -eq [System.IO.Path]::GetFullPath((Join-Path $HostBootstrapPublishOutput $artifact))) {
            continue
        }

        Copy-Item -LiteralPath $source -Destination $HostBootstrapPublishOutput -Force
    }

    Write-Host "发布完成: $PublishOutput" -ForegroundColor Green
}

function Invoke-Clean {
    Write-Step "清理编译输出"
    Invoke-DotnetCommand -Arguments @("clean", $SolutionPath) -FailureMessage "清理失败"
    Write-Host "清理完成" -ForegroundColor Green
}

switch ($Action) {
    "build"   { Invoke-AutoCADReferenceSync; Invoke-EnsurePluginAssets; Invoke-PluginBuild -Configuration "Debug" }
    "test"    { Invoke-AutoCADReferenceSync; Invoke-RestoreForTests; Invoke-SolutionBuild; Invoke-Test }
    "all"     { Invoke-AutoCADReferenceSync; Invoke-RestoreForTests; Invoke-SolutionBuild; Invoke-Test }
    "publish" { Invoke-AutoCADReferenceSync; Invoke-EnsurePluginAssets; Invoke-BootstrapBuild -Configuration "Release"; Invoke-PluginBuild -Configuration "Release"; Invoke-Publish }
    "clean"   { Invoke-Clean }
}
