# MetroToolKits 架构说明

**版本**: v1.1.0-beta  
**状态**: 当前权威架构文档  
**更新日期**: 2026-04-21

---

## 分层结构

```text
AutoCAD 宿主
  -> NETLOAD MetroToolKits.Bootstrap.dll
  -> Bootstrap
     插件发现 / DI / 日志 / 命令桥接
  -> SectionGenerator.Plugin
     命令入口 / WPF / PaletteSet / 点选与高亮
  -> SectionGenerator.App
     用例编排 / 输入校验 / 业务流程
  -> SectionGenerator.Core
     剖面算法 / 多楼层对齐 / 指纹计算
  -> Shared
     Foundation.Core / Foundation.Cad / Foundation.Building
```

---

## 层级职责

| 层级 | 项目 | 职责 | 允许依赖 |
|:---|:---|:---|:---|
| **共享层** | `Foundation.Core` | 几何、公用值对象、`IUserLogger`、统一失败/诊断契约 | 无 |
| | `Foundation.Cad` | 可复用的 AutoCAD 宿主访问能力（文档/事务/图层等） | `Foundation.Core` |
| | `Foundation.Building` | 纯建筑构件模型与 `ElementTypeDefinition` | `Foundation.Core` |
| **核心层** | `SectionGenerator.Core` | 剖面生成算法、多楼层叠加、楼层对齐、几何哈希 | `Foundation.Core`、`Foundation.Building` |
| **应用层** | `SectionGenerator.App` | 用例编排、请求/响应模型、基础设施抽象 | `SectionGenerator.Core`、`Foundation.Core`、`Foundation.Building` |
| **基础设施层** | `SectionGenerator.Infrastructure` | JSON 持久化、XData 仓储、CAD 绘图/导航/识别实现 | `SectionGenerator.App`、`SectionGenerator.Core`、`Foundation.*` |
| **表示层** | `SectionGenerator.Plugin` | 命令、WPF、PaletteSet、用户交互 | `Bootstrap`、`SectionGenerator.App`、`SectionGenerator.Infrastructure`、`Foundation.*` |
| **引导层** | `Bootstrap` | `NETLOAD` 入口、插件加载、命令桥接、DI 组合根 | `Foundation.Core`、`Foundation.Cad` |

---

## 当前边界规则

### 1. Plugin 只负责宿主交互，不直接落业务规则

- `Plugin` 允许处理选择集、命令行输入、高亮、窗口显示。
- `Plugin` 不再直接执行“构件转换/恢复/全图映射”的业务改写。
- `FloorConfig` 的加载/保存也统一经过 `IFloorConfigUseCase`。
- 这些流程统一由 `App` 编排，`Infrastructure` 落地。

### 2. App 是唯一业务编排入口

- `App` 定义并消费基础设施抽象，例如：
  - `IDrawingService`
  - `IElementRecognizer`
  - `IElementTypeCatalog`
  - `IElementConversionService`
  - `IFloorConfigRepository`
- `App` 不引用 `Autodesk.AutoCAD.*`、`System.Windows.*`、`Bootstrap` 静态状态。
- `App` 统一把底层异常包装结果映射为 `OperationResult`，并汇总 `Diagnostics`。

### 3. Foundation.Building 只保留纯模型

- `Foundation.Building` 现在只放领域模型和类型定义。
- `ElementTypes.json` 的读写已移到 `Infrastructure/Repositories/JsonElementTypeCatalog.cs`。
- 共享层不再承担文件系统或 JSON 持久化职责。

### 4. Bootstrap 只暴露宿主入口，不暴露运行时全局状态

- AutoCAD 命令仍通过 `BootstrapCommandBridge` 上的 `[CommandMethod]` 暴露。
- `Startup` 不再公开 `ServiceProvider` / `UserLogger` / `LoggerFactory`。
- 命令执行只通过 `CommandRegistry` 分发，避免插件和 UI 回读组合根。
- 默认文件日志与用户日志也写入用户目录，不再依赖安装目录可写。

### 5. Command 契约集中管理

- 对外命令名统一定义在 `Bootstrap/SectionGeneratorCommandNames.cs`。
- 各命令类通过 `CommandBindingAttribute` 自声明命令名和入口方法。
- `SectionGeneratorPlugin.RegisterCommands(...)` 使用 `CommandRegistrationScanner` 扫描这些绑定，不再手工维护第二份命令清单。
- `BootstrapCommandBridge` 与扫描注册链共用同一组命令常量。
- `CommandRegistry` 同时记录命令类型和入口方法名，避免桥接层再维护额外的反射特例。

### 6. Foundation.Cad 与 Infrastructure 的职责划分

- `Foundation.Cad` 提供跨插件可复用的宿主访问能力。
- `Infrastructure` 可以直接使用 AutoCAD API，但仅限插件特有的适配实现，例如：
  - XData 快照
  - 图纸导航
  - 剖面绘制
  - 构件转换备份
- 业务用例和 UI 不应越过抽象直接调用这些实现细节。

### 7. 安装目录只放模板，运行期数据写入用户目录

- 发布包中的 `SectionGeneratorConfig.json` 与 `ElementTypes.json` 仅作为模板文件随包分发。
- 插件启动时会将模板复制到按包隔离的用户目录 `%LOCALAPPDATA%\MetroToolKits\Packages\<package-scope>\SectionGenerator\`，后续读写都在该目录完成。
- `Bootstrap` 日志写入 `%LOCALAPPDATA%\MetroToolKits\Packages\<package-scope>\Bootstrap\logs\`。
- 运行期不再把配置或日志写回安装目录，也不会让不同发布包共享同一份用户状态。

### 8. 错误处理采用 Shared 契约 + 分层落地

- `Foundation.Core` 提供统一的 `OperationStatus`、`OperationFailure`、`OperationDiagnostic`、`ToolkitException`。
- `Plugin` 只负责输入前置校验和用户反馈，不直接推断业务失败原因。
- `App` 负责聚合诊断、判定 `Success / PartialSuccess / Failed`，并把异常转换成稳定失败结果。
- `Infrastructure` 必须包装 AutoCAD / IO 原生异常，不能把底层异常直接泄漏到命令层。
- `GenSection` 当前采用 fail-fast 规则：若所有楼层都没有识别到可剖切构件，则直接返回失败，不再继续绘图。

---

## 目标框架约束

| 项目 | 目标框架 | 原因 |
|:---|:---|:---|
| `Foundation.Core` | `net8.0` | 纯计算与接口 |
| `Foundation.Building` | `net8.0` | 纯领域模型 |
| `SectionGenerator.Core` | `net8.0` | 可脱离宿主单测 |
| `SectionGenerator.App` | `net8.0` | 可脱离宿主单测 |
| `Foundation.Cad` | `net8.0-windows` | 持有 AutoCAD API 依赖 |
| `SectionGenerator.Infrastructure` | `net8.0-windows` | AutoCAD 适配实现 |
| `SectionGenerator.Plugin` | `net8.0-windows` | WPF / AutoCAD 命令 |
| `Bootstrap` | `net8.0-windows` | AutoCAD `NETLOAD` 入口 |

---

## 项目依赖关系图

```text
Bootstrap
  -> Foundation.Core
  -> Foundation.Cad

SectionGenerator.Plugin
  -> Bootstrap
  -> SectionGenerator.App
  -> SectionGenerator.Infrastructure
  -> Foundation.Core / Foundation.Cad / Foundation.Building

SectionGenerator.App
  -> SectionGenerator.Core
  -> Foundation.Core / Foundation.Building

SectionGenerator.Infrastructure
  -> SectionGenerator.App
  -> SectionGenerator.Core
  -> Foundation.Core / Foundation.Cad / Foundation.Building

SectionGenerator.Core
  -> Foundation.Core / Foundation.Building

Foundation.Building
  -> Foundation.Core

Foundation.Cad
  -> Foundation.Core
```

---

## 可测试性边界

| 层级 | 测试方式 | 是否需要 AutoCAD |
|:---|:---|:---|
| `Foundation.Core` | 单元测试 | ❌ |
| `Foundation.Building` | 单元测试 | ❌ |
| `SectionGenerator.Core` | 单元测试 | ❌ |
| `SectionGenerator.App` | 单元测试 | ❌ |
| `SectionGenerator.Infrastructure` | 宿主或集成测试 | ✅ |
| `SectionGenerator.Plugin` | 手动宿主测试 | ✅ |
