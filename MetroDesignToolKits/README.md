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
   - `MetroToolKits.SectionGenerator.Plugin.dll`
   - `MetroToolKits.SectionGenerator.Core.dll`
   - `MetroToolKits.SectionGenerator.App.dll`
   - `MetroToolKits.SectionGenerator.Infrastructure.dll`
   - `SectionGeneratorConfig.json`
3. 在 AutoCAD 中执行 `NETLOAD`，加载 `MetroToolKits.Bootstrap.dll`
4. 所有插件命令自动可用

### 使用方法

在 AutoCAD 命令行中输入：
```
GenSection    # 生成剖面图
ShowToolbox   # 显示工具箱
```

---

## 项目结构

```
MetroToolKits/
├── MetroToolKits.sln                     # 解决方案文件
│
├── build/                                # 编译辅助文件（不发布）
│   └── References/
│       ├── accoremgd.dll                 # AutoCAD 核心托管库
│       ├── acdbmgd.dll                   # AutoCAD 数据库托管库
│       └── acmgd.dll                     # AutoCAD 托管库
│
├── src/                                  # 所有源码
│   │
│   ├── Shared/                           # 跨工具共享代码
│   │   ├── MetroToolKits.Foundation.Core/
│   │   │   ├── Geometry/                 # 点、线、多边形运算
│   │   │   ├── Mathematics/              # 插值、变换、单位转换
│   │   │   └── *.csproj
│   │   │
│   │   └── MetroToolKits.Foundation.Cad/
│   │       ├── Abstractions/             # 接口定义 (IEditorService等)
│   │       ├── Services/                 # CAD API 封装实现
│   │       ├── Extensions/               # 扩展方法
│   │       └── *.csproj
│   │
│   ├── Bootstrap/                        # 插件加载器
│   │   └── MetroToolKits.Bootstrap/
│   │       ├── Startup.cs                # DI 容器初始化
│   │       ├── PluginLoader.cs           # 插件扫描与加载
│   │       ├── CommandRegistry.cs        # 命令动态注册
│   │       └── *.csproj
│   │
│   ├── Plugins/                          # 功能插件
│   │   │
│   │   ├── SectionGenerator/             # 剖面生成器
│   │   │   ├── MetroToolKits.SectionGenerator.Core/
│   │   │   │   ├── Geometry/
│   │   │   │   ├── Sections/
│   │   │   │   └── *.csproj
│   │   │   ├── MetroToolKits.SectionGenerator.App/
│   │   │   │   ├── Abstractions/         # 用例接口、配置接口
│   │   │   │   ├── UseCases/             # 用例实现
│   │   │   │   └── *.csproj
│   │   │   ├── MetroToolKits.SectionGenerator.Infrastructure/
│   │   │   │   ├── Config/               # JSON 配置读写
│   │   │   │   └── *.csproj
│   │   │   ├── MetroToolKits.SectionGenerator.Plugin/
│   │   │   │   ├── Commands/             # GenSection 命令
│   │   │   │   ├── UI/                   # 工具箱面板
│   │   │   │   └── *.csproj
│   │   │   └── Resources/
│   │   │       └── SectionGeneratorConfig.json
│   │   │
│   │   └── Extras/                       # 小脚本收容所
│   │       └── MetroToolKits.Extras.Commands/
│   │           ├── LayerCommands.cs
│   │           ├── TextCommands.cs
│   │           └── *.csproj
│   │
│   └── Tests/                            # 测试项目
│       ├── MetroToolKits.Foundation.Core.Tests/
│       ├── MetroToolKits.SectionGenerator.Core.Tests/
│       └── MetroToolKits.Integration.Tests/
│
├── docs/                                 # 文档
├── README.md
└── .editorconfig
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
│ SectionGenerator │  │ DoorWindow      │  │ Extras.Commands │
│   .Plugin.dll    │  │   .Plugin.dll   │  │     .dll        │
└─────────────────┘  └─────────────────┘  └─────────────────┘
          │                    │                    │
          └────────────────────┼────────────────────┘
                               │ 依赖
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│                     共享基础设施层 (Shared)                       │
│  ┌──────────────────┐ ┌──────────────────┐ ┌──────────────────┐ │
│  │ Foundation.Core  │ │  Foundation.Cad  │ │  Foundation.UI   │ │
│  │  (纯算法/几何)   │ │ (CAD API 封装)   │ │  (通用WPF控件)   │ │
│  └──────────────────┘ └──────────────────┘ └──────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
```

### 分层架构（以 SectionGenerator 为例）

```
┌─────────────────────────────────────────────────────────────┐
│                    表示层 (Plugin)                            │
│         命令入口、工具箱 UI、用户交互                          │
│                    依赖：应用层、基础设施层(接口)               │
└──────────────────────────────┬──────────────────────────────┘
                               │ 调用
┌──────────────────────────────▼──────────────────────────────┐
│                     应用层 (App)                              │
│         用例编排、事务管理、业务协调                           │
│                    依赖：领域层、基础设施层(接口)               │
└──────────────┬───────────────────────────────┬──────────────┘
               │ 调用                          │ 依赖接口
┌──────────────▼──────────────┐ ┌──────────────▼──────────────┐
│       领域层 (Core)          │ │    基础设施层 (Infrastructure) │
│    核心业务概念、规则、算法   │ │  配置管理、外部服务访问         │
│   不依赖 CAD API 和框架      │ │  依赖：应用层接口、通用工具层    │
└──────────────────────────────┘ └──────────────────────────────┘
               │                               │
               └───────────────┬───────────────┘
                               ▼
┌─────────────────────────────────────────────────────────────┐
│                    通用工具层 (Foundation)                    │
│           纯几何算法、CAD API 封装、通用 UI 控件               │
│                        无上层依赖                             │
└─────────────────────────────────────────────────────────────┘
```

### 层级职责说明

| 层级 | 项目命名规范 | 职责 | 依赖规则 |
|:---|:---|:---|:---|
| **通用工具层** | `MetroToolKits.Foundation.*` | 提供所有工具共享的基础能力：纯数学/几何算法、CAD API 封装、通用 UI 控件 | 无外部依赖 |
| **领域层** | `MetroToolKits.{ToolName}.Core` | 定义工具专属的领域模型、业务规则、核心算法 | 仅依赖 `Foundation.Core` |
| **应用层** | `MetroToolKits.{ToolName}.App` | 编排领域能力完成特定用例，控制事务边界 | 依赖领域层、基础设施层接口 |
| **基础设施层** | `MetroToolKits.{ToolName}.Infrastructure` | 实现应用层定义的配置读写接口，处理 JSON/数据库等外部资源 | 依赖应用层接口、`Foundation.Core` |
| **表示层** | `MetroToolKits.{ToolName}.Plugin` | 接收 AutoCAD 命令，处理用户交互，渲染结果 | 依赖应用层、`Foundation.Cad` |
| **引导层** | `MetroToolKits.Bootstrap` | 统一插件入口，DI 容器初始化，命令动态注册 | 依赖所有插件的 Plugin 层、`Foundation.Cad` |

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

直接在 `src/Plugins/Extras/MetroToolKits.Extras.Commands/` 中添加 Command 类，调用 `Foundation.Cad` 封装完成功能。

### 依赖关系图

```
MetroToolKits.Bootstrap
    ├── MetroToolKits.Foundation.Cad
    └── 所有 MetroToolKits.*.Plugin

MetroToolKits.SectionGenerator.Plugin
    ├── MetroToolKits.SectionGenerator.App
    ├── MetroToolKits.Foundation.Cad
    └── MetroToolKits.Foundation.UI (可选)

MetroToolKits.SectionGenerator.App
    ├── MetroToolKits.SectionGenerator.Core
    └── MetroToolKits.SectionGenerator.Infrastructure.Abstractions

MetroToolKits.SectionGenerator.Infrastructure
    ├── MetroToolKits.SectionGenerator.App.Abstractions
    └── MetroToolKits.Foundation.Core

MetroToolKits.SectionGenerator.Core
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

### 发布项目

```bash
dotnet publish -c Release
```

### 依赖库

- **AutoCAD .NET API**: 用于与 AutoCAD 交互
  - `accoremgd.dll`: 核心功能
  - `acdbmgd.dll`: 数据库操作
  - `acmgd.dll`: 图形操作
- **Newtonsoft.Json**: 用于配置文件的序列化和反序列化

### 注意事项

1. 确保引用的 CAD API 库版本与目标 AutoCAD 版本匹配
2. 项目目标框架为 `net8.0-windows`，需要安装 .NET 8.0 SDK
3. 输出平台为 `x64`，与 AutoCAD 64位版本匹配
4. **重要**：`build/References/` 下的 DLL 仅用于编译，发布时**不要**复制到输出目录

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

### v1.0.0 (2024)
- 初始版本发布
- 实现 SectionGenerator 剖面图生成功能
- 支持可配置参数和坡度设置
- 提供工具箱界面
- 采用插件化架构，支持动态加载扩展

---

## 许可证

本项目为内部工具，仅供学习和参考使用。

## 联系方式

如有问题或建议，请联系开发团队。

---

**开发环境**: .NET 8.0 + AutoCAD API
