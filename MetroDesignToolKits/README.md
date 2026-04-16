# MetroToolKits - CAD工具集

## 项目简介

MetroToolKits 是一个基于 AutoCAD .NET API 开发的 CAD 工具集，旨在提供一系列高效、易用的建筑设计辅助工具。采用插件化架构设计，支持动态加载和扩展。

| 工具名称 | 说明 | 状态 |
|---------|------|------|
| **SectionGenerator** | 建筑剖面图自动生成器 | ✅ 已发布 |

---

## 快速开始

### 系统要求

- AutoCAD 2018 或更高版本
- .NET 8.0 Windows
- Windows 10/11 操作系统

### 安装 / 加载方法

1. 编译整个解决方案
2. 准备以下文件到同一目录：
   - `MetroToolKits.Bootstrap.dll`（主入口）
   - `MetroToolKits.Foundation.Core.dll`
   - `MetroToolKits.Foundation.Cad.dll`
   - `MetroToolKits.Foundation.Building.dll`
   - `MetroToolKits.SectionGenerator.Plugin.dll`
   - `MetroToolKits.SectionGenerator.Core.dll`
   - `MetroToolKits.SectionGenerator.App.dll`
   - `MetroToolKits.SectionGenerator.Infrastructure.dll`
   - `SectionGeneratorConfig.json`
   - `ElementTypes.json`
3. 在 AutoCAD 中执行 `NETLOAD`，加载 `MetroToolKits.Bootstrap.dll`
4. 所有插件命令自动可用

### 可用命令

在 AutoCAD 命令行中输入：

| 命令 | 说明 |
|:---|:---|
| `GenSection` | 生成多楼层剖面图 |
| `FloorConfig` | 楼层配置管理（增删改楼层、拾取对齐点） |
| `CheckSectionUpdates` | 检测剖面是否需要更新（非模态对话框） |
| `UpdateSection` | 更新单个剖面块 |
| `LayerMapping` | 全图图层映射管理器 |
| `ConvertRegion` | 区域引导构件转换 |
| `RevertConversion` | 恢复构件转换 |

---

## 项目结构

```
MetroToolKits/
├── MetroToolKits.sln                     # 解决方案文件
│
├── build/                                # 编译辅助文件（不发布）
│   └── References/
│       ├── accoremgd.dll
│       ├── acdbmgd.dll
│       └── acmgd.dll
│
├── docs/                                 # 开发文档
│
└── src/                                  # 所有源码
    │
    ├── Shared/                           # 跨工具共享代码
    │   ├── MetroToolKits.Foundation.Core/
    │   │   ├── Geometry/                 # 点、线、多边形、变换矩阵
    │   │   ├── Logging/                  # IUserLogger 接口（跨层共享）
    │   │   └── *.csproj
    │   │
    │   ├── MetroToolKits.Foundation.Cad/
    │   │   ├── Services/                 # CAD API 封装（接口+实现）
    │   │   └── *.csproj
    │   │
    │   └── MetroToolKits.Foundation.Building/
    │       ├── Elements/                 # BuildingElement / Wall / Slab / Column
    │       ├── Types/                    # ElementTypeDefinition / ElementTypeLoader
    │       └── *.csproj
    │
    ├── Bootstrap/                        # 插件加载器
    │   └── MetroToolKits.Bootstrap/
    │       ├── Startup.cs                # DI 容器 + 日志系统初始化
    │       ├── PluginLoader.cs           # 插件扫描与加载
    │       ├── CommandRegistry.cs        # 命令元数据注册（工具箱查询用）
    │       ├── Logging/                  # UserLogger / FileLogger / AcadEditorLogger
    │       └── *.csproj
    │
    ├── Plugins/                          # 功能插件
    │   └── SectionGenerator/
    │       ├── MetroToolKits.SectionGenerator.Core/
    │       │   └── Sections/             # SectionComposer / MultiFloorSectionComposer
    │       │                             # FloorGeometryHasher / SectionSnapshot
    │       ├── MetroToolKits.SectionGenerator.App/
    │       │   ├── Abstractions/         # 用例接口、仓储接口（IDrawingService 等）
    │       │   └── UseCases/             # GenerateSectionUseCase 等
    │       ├── MetroToolKits.SectionGenerator.Infrastructure/
    │       │   ├── Config/               # 旧版 JSON 配置（保留兼容）
    │       │   ├── Recognition/          # LayerBasedElementRecognizer
    │       │   ├── Repositories/         # JsonFloorConfigRepository / XDataSnapshotRepository
    │       │   └── Services/             # CadDrawingService / CadBlockEraseService
    │       │                             # ElementConversionBackupService
    │       ├── MetroToolKits.SectionGenerator.Plugin/
    │       │   ├── Commands/             # 所有 AutoCAD 命令入口
    │       │   ├── UI/                   # WPF 对话框（FloorConfigWindow 等）
    │       │   └── *.csproj
    │       └── Resources/
    │           └── SectionGeneratorConfig.json
    │
    └── Tests/                            # 测试项目
        ├── MetroToolKits.Tests.Foundation/          # Foundation 层单元测试
        ├── MetroToolKits.Tests.SectionGenerator.Core/  # Core 层单元测试
        └── （预留）MetroToolKits.Integration.Tests/ # 需 AutoCAD 环境的集成测试
```

---

## 技术架构

### 架构总览

```
┌─────────────────────────────────────────────────────────────────┐
│                     AutoCAD 宿主进程                             │
└─────────────────────────────────────────────────────────────────┘
                               │ NETLOAD
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│                  MetroToolKits.Bootstrap.dll                     │
│                (插件加载器 / DI 容器 / 命令注册)                   │
└─────────────────────────────────────────────────────────────────┘
                               │ 扫描加载
          ┌────────────────────┼────────────────────┐
          ▼                    ▼                    ▼
┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐
│ SectionGenerator │  │ 其他功能插件     │  │ Extras.Commands │
│   .Plugin.dll    │  │   .Plugin.dll    │  │  （预留扩展）    │
└─────────────────┘  └─────────────────┘  └─────────────────┘
          │                    │                    │
          └────────────────────┼────────────────────┘
                               │ 依赖
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│                     共享基础设施层 (Shared)                       │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────────┐  │
│  │Foundation.   │  │Foundation.   │  │Foundation.Building   │  │
│  │Core          │  │Cad           │  │(建筑构件领域模型)     │  │
│  │(纯算法/几何) │  │(CAD API封装) │  │                      │  │
│  └──────────────┘  └──────────────┘  └──────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

### 分层架构（以 SectionGenerator 为例）

```
┌─────────────────────────────────────────────────────────────┐
│                    表示层 (Plugin)                            │
│         命令入口、工具箱 UI、用户交互                          │
│         依赖：应用层接口、Foundation.Cad                       │
└──────────────────────────────┬──────────────────────────────┘
                               │ 调用
┌──────────────────────────────▼──────────────────────────────┐
│                     应用层 (App)                              │
│         用例编排、事务管理、业务协调                           │
│         依赖：领域层、基础设施层接口（IDrawingService 等）      │
└──────────────┬───────────────────────────────┬──────────────┘
               │ 调用                          │ 依赖接口
┌──────────────▼──────────────┐ ┌──────────────▼──────────────┐
│       领域层 (Core)          │ │    基础设施层 (Infrastructure) │
│    核心业务概念、规则、算法   │ │  配置管理、绘图、快照存储       │
│    不依赖 CAD API 和框架     │ │  依赖：应用层接口、Foundation   │
└──────────────────────────────┘ └──────────────────────────────┘
               │                               │
               └───────────────┬───────────────┘
                               ▼
┌─────────────────────────────────────────────────────────────┐
│                    通用工具层 (Foundation)                    │
│    Foundation.Core（几何/日志接口）                           │
│    Foundation.Cad（CAD API 封装）                             │
│    Foundation.Building（建筑构件领域模型）                     │
└─────────────────────────────────────────────────────────────┘
```

### 层级职责说明

| 层级 | 项目 | 职责 | 依赖规则 |
|:---|:---|:---|:---|
| **通用工具层** | `Foundation.Core` | 纯数学/几何算法、`IUserLogger` 接口 | 无外部依赖 |
| | `Foundation.Cad` | AutoCAD API 封装（Editor/Layer/Transaction） | `Foundation.Core` |
| | `Foundation.Building` | 建筑构件领域模型（`BuildingElement`、`Wall`、`Slab` 等） | `Foundation.Core` |
| **领域层** | `SectionGenerator.Core` | 剖面生成算法、多楼层堆叠、几何指纹计算 | `Foundation.Core`、`Foundation.Building` |
| **应用层** | `SectionGenerator.App` | 用例编排（生成/检测/更新剖面） | `Core`、基础设施接口（`IDrawingService`、`IUserLogger` 等） |
| **基础设施层** | `SectionGenerator.Infrastructure` | 配置读写、绘图实现、快照存储、构件识别 | `App.Abstractions`、`Foundation.Cad`、`Foundation.Building` |
| **表示层** | `SectionGenerator.Plugin` | 命令入口、WPF 对话框、命令行交互 | `App.Abstractions`、`Foundation.Cad` |
| **引导层** | `Bootstrap` | 插件入口、DI 容器初始化、日志系统、命令注册 | `Foundation.Core`、`Foundation.Cad` |

> **注意**：`CommandRegistry` 用于工具箱面板查询可用命令及元数据，实际命令注册由 AutoCAD 运行时通过 `[CommandMethod]` 特性在程序集加载时自动完成。
>
> **注意**：Core 层允许定义纯数据结构 DTO（如 `SectionSnapshot`），供 Infrastructure 层读写使用，以降低跨层传递复杂度。

### 插件接口

```csharp
public interface IPlugin
{
    string Name { get; }
    string Version { get; }
    void ConfigureServices(IServiceCollection services);
    void RegisterCommands(ICommandRegistry registry);
}
```

### 插件拔插机制

- **统一入口**：用户只需 `NETLOAD MetroToolKits.Bootstrap.dll` 一次
- **自动发现**：`Bootstrap` 扫描同目录下所有 `MetroToolKits.*.Plugin.dll`
- **动态注册**：插件向 DI 容器注册自身服务，命令自动可用
- **物理拔插**：复制新插件 DLL 即可扩展功能，删除 DLL 即可移除功能

---

## 扩展开发

### 新增工具规范

#### 大型功能（需独立分层）

在 `src/Plugins/` 下创建文件夹，按如下模板建立项目：

```
Plugins/
└── YourNewTool/
    ├── MetroToolKits.YourNewTool.Core/
    ├── MetroToolKits.YourNewTool.App/
    ├── MetroToolKits.YourNewTool.Infrastructure/
    ├── MetroToolKits.YourNewTool.Plugin/
    └── Resources/
        └── YourNewToolConfig.json
```

#### 小型脚本（无需分层）

在 `src/Plugins/Extras/`（预留目录，按需创建）中添加 Command 类，调用 `Foundation.Cad` 封装完成功能。

### 依赖关系图（实际）

```
MetroToolKits.Bootstrap
    ├── MetroToolKits.Foundation.Core   ← 含 IUserLogger 接口
    └── MetroToolKits.Foundation.Cad

MetroToolKits.SectionGenerator.Plugin
    ├── MetroToolKits.SectionGenerator.App
    ├── MetroToolKits.SectionGenerator.Infrastructure
    ├── MetroToolKits.Foundation.Core
    ├── MetroToolKits.Foundation.Cad
    └── MetroToolKits.Foundation.Building

MetroToolKits.SectionGenerator.App
    ├── MetroToolKits.SectionGenerator.Core
    ├── MetroToolKits.Foundation.Core
    ├── MetroToolKits.Foundation.Building
    └── MetroToolKits.Bootstrap         ← 仅通过 IUserLogger 接口依赖

MetroToolKits.SectionGenerator.Infrastructure
    ├── MetroToolKits.SectionGenerator.App   ← 实现 App 层接口
    ├── MetroToolKits.SectionGenerator.Core
    ├── MetroToolKits.Foundation.Core
    ├── MetroToolKits.Foundation.Cad
    └── MetroToolKits.Foundation.Building

MetroToolKits.SectionGenerator.Core
    ├── MetroToolKits.Foundation.Core
    └── MetroToolKits.Foundation.Building

MetroToolKits.Foundation.Building
    └── MetroToolKits.Foundation.Core

MetroToolKits.Foundation.Cad
    └── MetroToolKits.Foundation.Core
```

---

## 开发说明

### 编译项目

```bash
dotnet build
```

### 运行测试

```bash
dotnet test
```

### 发布项目

```bash
dotnet publish -c Release
```

### 依赖库

- **AutoCAD .NET API**：用于与 AutoCAD 交互
  - `accoremgd.dll`：核心功能
  - `acdbmgd.dll`：数据库操作
  - `acmgd.dll`：图形操作
- **Newtonsoft.Json**：配置文件和快照的序列化/反序列化
- **Microsoft.Extensions.DependencyInjection**：DI 容器
- **Microsoft.Extensions.Logging**：结构化日志

### 注意事项

1. 确保引用的 CAD API 库版本与目标 AutoCAD 版本匹配
2. 项目目标框架为 `net8.0-windows`，需要安装 .NET 8.0 SDK
3. **重要**：`build/References/` 下的 DLL 仅用于编译，发布时**不要**复制到输出目录

---

## 设计原则

| 原则 | 说明 |
|:---|:---|
| **关注点分离** | 表示、应用、领域、基础设施职责清晰分离 |
| **依赖倒置** | 上层依赖接口，下层实现接口，禁止跨层直接依赖具体类 |
| **领域纯净** | Core 层不引用任何 AutoCAD API 或第三方框架 |
| **显式依赖** | 通过构造函数注入，避免 Service Locator 反模式 |
| **可测试性** | Core 层算法可脱离 AutoCAD 进行单元测试 |
| **拔插灵活** | 新增/移除功能只需操作对应 DLL，无需修改核心代码 |

---

## 版本历史

### v1.1.0 (2026-04-16)
- 完成阶段一~五开发（基础设施、构件识别、单层/多层剖面生成、变更检测）
- 架构纠偏：提取 `IUserLogger` 接口，消除 App 层对 Bootstrap 的直接依赖
- 架构纠偏：合并 `CadAdapter` 层至 `Infrastructure`，简化项目结构
- 新增 `Foundation.Building` 共享层（建筑构件领域模型）
- 新增日志系统（开发日志 + 用户日志，支持配置文件切换）
- 单元测试：100 个测试用例，全部通过

### v1.0.0 (2024)
- 初始版本发布
- 实现 SectionGenerator 剖面图生成功能
- 支持可配置参数和坡度设置
- 采用插件化架构，支持动态加载扩展

---

## 许可证

本项目为内部工具，仅供学习和参考使用。

---

**开发环境**: .NET 8.0 + AutoCAD API
