# MetroToolKits

AutoCAD .NET 插件工具集，当前包含 **SectionGenerator**（建筑剖面图自动生成器）。

**状态**: 🚧 v1.1.0-beta — 内部可用，尚未正式发布

---

## 文档导航

| 文档 | 说明 |
|:---|:---|
| [docs/architecture.md](./docs/architecture.md) | 架构说明（权威） |
| [docs/status.md](./docs/status.md) | 项目状态与功能完成情况（权威） |
| [docs/构建与测试指南.md](./docs/构建与测试指南.md) | 构建先决条件、命令集、测试范围 |
| [docs/阶段七-验收记录.md](./docs/阶段七-验收记录.md) | 架构复核、阶段七验收与阻塞说明 |
| [docs/SectionGenerator用户手册.md](./docs/SectionGenerator用户手册.md) | 面向使用人员的安装与操作指南 |
| [docs/SectionGenerator扩展指南.md](./docs/SectionGenerator扩展指南.md) | 面向开发人员的扩展说明 |
| [docs/section-generator-ui-regression-checklist.md](./docs/section-generator-ui-regression-checklist.md) | SectionGenerator UI 回归验收清单 |
| [docs/section-generator-ui-regression-result-stage-19.md](./docs/section-generator-ui-regression-result-stage-19.md) | 第十九阶段 UI 人工验收记录 |
| [docs/开发日志文档.md](./docs/开发日志文档.md) | 开发日志规范 |
| [docs/用户日志文档.md](./docs/用户日志文档.md) | 用户日志规范 |
| [docs/archive/](./docs/archive/) | 历史分析与阶段性文档（归档） |

---

## 快速开始

### 系统要求

- AutoCAD 2018+，Windows 10/11
- .NET 8.0 SDK

### 构建

```bash
# 1. 同步 AutoCAD DLL（首次或切换版本时）
.\build\sync-autocad-references.ps1 -AutoCADDir "C:\Program Files\Autodesk\AutoCAD 2025"

# 2. 构建插件
.\build.ps1 build
```

### 加载插件

在 AutoCAD 中执行：
```
NETLOAD
```
选择 `MetroToolKits.Bootstrap.dll`，由 `Bootstrap` 完成插件发现、DI 初始化与命令桥接。

### 推荐使用流程

1. 先执行 `FloorConfig` 配置楼层与对齐点
2. 再执行 `GenSection` 生成剖面
3. 平面变更后执行 `CheckSectionUpdates`
4. 需要双向定位时执行 `LocateSourceElement` / `FindRelatedSections`
5. 高频操作建议执行 `ShowToolbox`

### SectionToolbox 工作台

`ShowToolbox` 会打开多 Tab 工作台，当前主要入口包括：

- `总览`：当前图纸状态、推荐动作和快捷入口
- `楼层配置`：内嵌 `FloorConfigPanel`，支持保存、放弃修改、拾取对齐点和整层范围
- `图纸准备`：内嵌 `LayerMappingPanel`，可直接进行图层映射编辑，并保留独立图层映射/模板管理入口
- `剖面生成`：生成条件、缺失项摘要和 `GenSection` 入口
- `剖面维护`：已有剖面状态摘要和 `CheckSectionUpdates` 入口
- `模板与输出`：模板管理入口，以及前往楼层配置维护输出设置的入口

说明：

- 工具箱顶部的“关闭工具箱”只会隐藏当前 Palette，不会自动保存或清空当前工作台状态
- 模板管理器、楼层配置窗口、图层映射独立窗口路径仍然保留，用于兼容既有使用方式

### 可用命令

| 命令 | 说明 |
|:---|:---|
| `GenSection` | 生成多楼层剖面图 |
| `FloorConfig` | 楼层配置管理 |
| `CheckSectionUpdates` | 检测剖面更新状态 |
| `UpdateSection` | 更新单个剖面块 |
| `LocateSourceElement` | 从剖面实体定位回源平面构件 |
| `FindRelatedSections` | 从平面构件查询关联剖面 |
| `ShowToolbox` | 打开 MetroToolKits 工具箱面板 |
| `LayerMapping` | 图层映射管理器 |
| `ConvertRegion` | 区域引导构件转换 |
| `RevertConversion` | 恢复构件转换 |
| `RevertAllConversions` | 恢复当前图中的全部转换 |

---

## 依赖

- AutoCAD .NET API（`acdbmgd.dll` / `acmgd.dll` / `accoremgd.dll`）— 本机复制到 `build/References/`，不随仓库分发，且已被 Git 忽略，见 [build/References/README.md](./build/References/README.md)
- `Microsoft.Extensions.*` 程序集会在构建/发布时从当前项目输出复制到插件目录
- 发布包默认包含 `SectionGeneratorConfig.json`、`ElementTypes.json` 和 `README-release.md`
- 运行期可写配置与日志默认落在按包隔离的用户目录 `%LOCALAPPDATA%\MetroToolKits\Packages\<package-scope>\...`，发布包中的 JSON 文件只作为初始化模板保留
- SectionToolbox 的本地操作链追踪（`OperationTrace`）默认写入 `%LOCALAPPDATA%\MetroToolKits\OperationTrace\operation-trace-YYYYMMDD.jsonl`
- `OperationTrace` 只记录本地结构化操作事件（如工具箱打开、刷新、Tab 切换、命令入口、关闭工具箱），不上传数据，也不记录 DWG 完整路径、图元坐标、handle 列表等敏感信息
- 宿主烟测与宿主验收命令仅供自动化脚本内部调用，正常用户命令面不再暴露

---

## 许可证

内部工具，仅供学习和参考使用。
