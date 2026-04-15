# SectionGenerator - CAD剖面图生成器

## 项目简介

SectionGenerator 是一个基于 AutoCAD .NET API 开发的插件，用于自动生成建筑剖面图。该工具可以根据用户选择的剖切线和辅助实体，快速生成包含底板、顶板、装修面层等结构的剖面图，并自动添加尺寸标注和标高符号。

## 功能特性

- **剖面图自动生成**: 根据剖切线自动生成建筑剖面图
- **辅助实体识别**: 支持与剖切线相交的墙线、梁线等辅助实体识别
- **可配置参数**: 支持自定义底板厚度、顶板厚度、装修面层厚度、层高等参数
- **坡度支持**: 可选择是否添加楼板坡度，并自定义坡度值
- **自动标注**: 自动生成层高标注、坡度标注和标高符号
- **配置文件持久化**: 参数设置自动保存到配置文件，下次使用时自动加载
- **看线功能**: 查看直线、多段线、圆弧的详细几何信息

## 系统要求

- AutoCAD 2018 或更高版本
- .NET 8.0 Windows
- Windows 10/11 操作系统

## 安装 / 加载方法（开发态）

1. 编译项目生成 `SectionGenerator.dll`
2. 准备运行所需文件（同一目录即可）：
   - `SectionGenerator.dll`
   - `SectionGeneratorConfig.json`
3. 在 AutoCAD 中执行 `NETLOAD`，加载 `SectionGenerator.dll`

> 说明：项目通过本地引用 `libs/` 下的 `acmgd/acdbmgd/accoremgd` 进行编译，运行时由 AutoCAD 宿主提供对应程序集。

## 使用方法

### 启动命令

在 AutoCAD 命令行中输入：
```
GenSection
```

也可以使用工具箱：
```
ShowToolbox
```

### 操作步骤

1. **选择剖切线**: 选择一条直线作为剖切线
2. **选择辅助实体** (可选): 选择与剖切线相交的墙线、梁线等实体，用于生成分割线
3. **修改设置** (可选): 根据提示选择是否修改剖面参数
4. **指定插入点**: 在图纸上指定剖面图的插入位置

### 配置参数

| 参数名 | 默认值 | 单位 | 说明 |
|--------|--------|------|------|
| BottomSlabThickness | 800.0 | mm | 底板厚度 |
| TopSlabThickness | 600.0 | mm | 顶板厚度 |
| FinishThickness | 120.0 | mm | 装修面层厚度 |
| FloorHeight | 5200.0 | mm | 层高 |
| HasSlope | true | - | 是否有坡度 |
| SlopeValue | 0.002 | - | 坡度值（千分比）|

## 项目结构

```
SectionGenerator/
├── SectionGenerator.csproj           # 项目文件
├── SectionGeneratorConfig.json       # 配置文件
├── libs/                             # CAD API 库
│   ├── accoremgd.dll                 # AutoCAD 核心托管库
│   ├── acdbmgd.dll                   # AutoCAD 数据库托管库
│   └── acmgd.dll                     # AutoCAD 托管库
├── src/
│   ├── SectionGenerator.Plugin/      # 命令入口/Palette(UI)（直接依赖 Autodesk.*）
│   │   ├── Commands/                 # CommandMethod 入口
│   │   └── UI/                       # Palette/WinForms
│   ├── SectionGenerator.App/         # 应用层（用例编排、接口抽象）
│   │   └── Abstractions/             # CAD 边界接口（事务/选择/读写/坐标转换）
│   └── SectionGenerator.Core/        # 领域层（纯算法/模型，不依赖 Autodesk.*）
└── README.md                         # 项目文档
```

## 技术架构

### 分层说明（当前落地）

- **Plugin（`src/SectionGenerator.Plugin`）**：AutoCAD 命令入口、UI（Palette/WinForms）、与用户交互。允许引用 `Autodesk.AutoCAD.*`。
- **App（`src/SectionGenerator.App`）**：应用层抽象与用例编排（目前主要是边界接口与抽象基类）。不直接依赖具体 CAD 实现。
- **Core（`src/SectionGenerator.Core`）**：与 AutoCAD 无关的核心模型/算法（几何、剖面领域对象）。不引用 `Autodesk.*`。

### 现有实现位置
- **剖面命令与设置**：`src/SectionGenerator.Plugin/Commands/SectionCommands.cs`
  - `Commands.GenerateSection()`：命令入口与交互流程
  - `SectionSettings`：配置参数模型（目前仍位于 Plugin，后续可迁移到 App/Infrastructure）
- **工具箱 UI**：`src/SectionGenerator.Plugin/UI/ToolboxPanel.cs`

### 依赖库

- **AutoCAD .NET API**: 用于与 AutoCAD 交互
  - `accoremgd.dll`: 核心功能
  - `acdbmgd.dll`: 数据库操作
  - `acmgd.dll`: 图形操作
- **Newtonsoft.Json**: 用于配置文件的序列化和反序列化

## 开发说明

### 编译项目

```bash
dotnet build
```

### 发布项目

```bash
dotnet publish -c Release
```

### 清理产物（建议）

构建输出目录为可再生文件，不建议纳入源码管理：
- `bin/`
- `obj/`

### 注意事项（当前项目约束）

1. 确保引用的 CAD API 库版本与目标 AutoCAD 版本匹配
2. 项目目标框架为 `net8.0-windows`，需要安装 .NET 8.0 SDK
3. 输出平台为 `x64`，与 AutoCAD 64位版本匹配

## 版本历史

### v1.0.0
- 初始版本发布
- 实现基本的剖面图生成功能
- 支持可配置参数
- 支持坡度设置

## 许可证

本项目为内部工具，仅供学习和参考使用。

## 联系方式

如有问题或建议，请联系开发团队。

---

**开发日期**: 2024年
**开发环境**: .NET 8.0 + AutoCAD API
