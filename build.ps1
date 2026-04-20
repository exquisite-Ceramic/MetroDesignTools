#!/usr/bin/env pwsh
<#
.SYNOPSIS
    MetroToolKits 构建脚本

.DESCRIPTION
    封装 restore / build / test / publish 操作。

.PARAMETER Action
    build   - 编译解决方案（默认）
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
    [string]$Action = "build"
)

$ErrorActionPreference = "Stop"
$SolutionPath = Join-Path $PSScriptRoot "MetroToolKits.sln"
$PublishOutput = Join-Path $PSScriptRoot "publish\SectionGenerator"
$PluginProject = Join-Path $PSScriptRoot "src\Plugins\SectionGenerator\MetroToolKits.SectionGenerator.Plugin\MetroToolKits.SectionGenerator.Plugin.csproj"

function Write-Step([string]$msg) {
    Write-Host "`n==> $msg" -ForegroundColor Cyan
}

function Invoke-Build {
    Write-Step "编译解决方案"
    dotnet build $SolutionPath -c Debug --no-restore
    if ($LASTEXITCODE -ne 0) { throw "编译失败" }
    Write-Host "编译成功" -ForegroundColor Green
}

function Invoke-Test {
    Write-Step "运行单元测试（不依赖 AutoCAD）"
    dotnet test $SolutionPath `
        --filter "FullyQualifiedName~MetroToolKits.Tests" `
        --no-build `
        -v minimal
    if ($LASTEXITCODE -ne 0) { throw "测试失败" }
    Write-Host "所有测试通过" -ForegroundColor Green
}

function Invoke-Publish {
    Write-Step "发布插件到 $PublishOutput"
    dotnet publish $PluginProject `
        -c Release `
        -o $PublishOutput `
        --no-restore
    if ($LASTEXITCODE -ne 0) { throw "发布失败" }
    Write-Host "发布完成: $PublishOutput" -ForegroundColor Green
}

function Invoke-Clean {
    Write-Step "清理编译输出"
    dotnet clean $SolutionPath
    Write-Host "清理完成" -ForegroundColor Green
}

# 先还原依赖
Write-Step "还原 NuGet 依赖"
dotnet restore $SolutionPath
if ($LASTEXITCODE -ne 0) { throw "还原失败" }

switch ($Action) {
    "build"   { Invoke-Build }
    "test"    { Invoke-Build; Invoke-Test }
    "all"     { Invoke-Build; Invoke-Test }
    "publish" { Invoke-Build; Invoke-Publish }
    "clean"   { Invoke-Clean }
}
