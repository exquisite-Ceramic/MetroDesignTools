# MetroToolKits 架构说明

**版本**: v1.1.0-beta  
**状态**: 当前权威架构文档  
**更新日期**: 2026-04-20

---

## 分层结构

```
┌─────────────────────────────────────────────────────────────────┐
│                     AutoCAD 宿主进程                             │
└─────────────────────────────────────────────────────────────────┘
                               │ NETLOAD
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│                  MetroToolKits.Bootstrap.dll                     │
│   插件加载器 / DI 容器 / 日志系统 / AutoCAD 命令桥接             │
└─────────────────────────────────────────────────────────────────┘
                               │ 扫描 MetroToolKits.*.Plugin.dll
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│              MetroToolKits.SectionGenerator.Plugin.dll           │
│         命令实现 / WPF 对话框 / 命令行交互                        │
└─────────────────────────────────────────────────────────────────┘
          │ 调用                              │ 调用
          ▼                                   ▼
┌──────────────────────┐         ┌──────────────────────────────┐
│  SectionGenerator.   │         │  SectionGenerator.           │
│  App                 │         │  Infrastructure              │
│  用例编排            │◄────────│  配置/绘图/快照/识别          │
│  (接口依赖)          │  实现接口│  (实现 App 层接口)           │
└──────────┬───────────┘         └──────────────────────────────┘
           │ 调用
           ▼
┌──────────────────────┐
│  SectionGenerator.   │
│  Core                │
│  剖面算法/指纹计算    │
└──────────────────────┘
           │
           ▼
┌─────────────────────────────────────────────────────────────────┐
│                     共享基础设施层 (Shared)                       │
│  Foundation.Core        Foundation.Cad       Foundation.Building │
│  (几何/日志接口)        (CAD API 封装)        (建筑构件模型)      │
└─────────────────────────────────────────────────────────────────┘
```

---

## 层级职责

| 层级 | 项目 | 职责 | 依赖规则 |
|:---|:---|:---|:---|
| **通用工具层** | `Foundation.Core` | 纯几何算法、`IUserLogger` 接口 | 无外部依赖 |
| | `Foundation.Cad` | AutoCAD API 封装 | `Foundation.Core` |
| | `Foundation.Building` | 建筑构件领域模型（`Wall`、`Slab` 等） | `Foundation.Core` |
| **领域层** | `SectionGenerator.Core` | 剖面生成算法、多楼层堆叠、几何指纹 | `Foundation.Core`、`Foundation.Building` |
| **应用层** | `SectionGenerator.App` | 用例编排（生成/检测/更新剖面） | `Core`、基础设施接口 |
| **基础设施层** | `SectionGenerator.Infrastructure` | 配置读写、绘图实现、快照存储、构件识别 | `App.Abstractions`、`Foundation.*` |
| **表示层** | `SectionGenerator.Plugin` | 命令入口、WPF 对话框 | `App.Abstractions`、`Foundation.Cad` |
| **引导层** | `Bootstrap` | 插件入口、DI 容器、日志系统 | `Foundation.Core`、`Foundation.Cad` |

---

## 关键设计决策

### 依赖倒置
- App 层定义接口（`IDrawingService`、`IElementRecognizer` 等），Infrastructure 层实现
- `IUserLogger` 接口定义在 `Foundation.Core.Logging`，Bootstrap 实现，App 层通过接口使用

### 插件发现机制
- Bootstrap 扫描同目录下 `MetroToolKits.*.Plugin.dll`
- 插件 `AssemblyName` 必须符合此模式（当前：`MetroToolKits.SectionGenerator.Plugin`）
- 通过反射查找 `IPlugin` 实现类，调用 `ConfigureServices` 和 `RegisterCommands`

### 命令注册
- AutoCAD 命令通过 `BootstrapCommandBridge` 上的 `[CommandMethod]` 特性注册到 `MetroToolKits.Bootstrap.dll`
- `BootstrapCommandBridge` 只做宿主入口和异常兜底，真正的命令实现仍从插件注册到 `CommandRegistry`
- `CommandRegistry` 同时承担命令元数据查询和 DI 分发，不再要求插件程序集自己暴露 AutoCAD 命令入口

### 版本管理
- 统一版本源：`Directory.Build.props`（当前 `1.1.0-beta`）
- 插件版本从程序集读取，不硬编码

---

## 项目依赖关系图

```
Bootstrap
  └── Foundation.Core（含 IUserLogger）
  └── Foundation.Cad

SectionGenerator.Plugin
  └── Bootstrap
  └── SectionGenerator.App
  └── SectionGenerator.Infrastructure
  └── Foundation.Core / Foundation.Cad / Foundation.Building

SectionGenerator.App
  └── SectionGenerator.Core
  └── Foundation.Core / Foundation.Building

SectionGenerator.Infrastructure
  └── SectionGenerator.App（实现接口）
  └── SectionGenerator.Core
  └── Foundation.Core / Foundation.Cad / Foundation.Building

SectionGenerator.Core
  └── Foundation.Core / Foundation.Building

Foundation.Building
  └── Foundation.Core

Foundation.Cad
  └── Foundation.Core
```

---

## 可测试性边界

| 层级 | 测试方式 | 是否需要 AutoCAD |
|:---|:---|:---|
| `Foundation.Core` | 单元测试 | ❌ |
| `Foundation.Building` | 单元测试 | ❌ |
| `SectionGenerator.Core` | 单元测试（含 Mock） | ❌ |
| `SectionGenerator.App` | 单元测试（Mock 接口） | ❌ |
| `SectionGenerator.Infrastructure` | 集成测试 | ✅ |
| `SectionGenerator.Plugin` | 手动测试 | ✅ |
