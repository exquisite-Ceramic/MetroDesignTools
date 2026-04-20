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
# 1. 将 AutoCAD DLL 复制到 build/References/（见构建指南 §1.2）
# 2. 构建 + 测试
.\build.ps1 all
```

### 加载插件

在 AutoCAD 中执行：
```
NETLOAD
```
选择 `MetroToolKits.SectionGenerator.Plugin.dll`，插件会自动带起 `Bootstrap` 并注册命令。

### 推荐使用流程

1. 先执行 `FloorConfig` 配置楼层与对齐点
2. 再执行 `GenSection` 生成剖面
3. 平面变更后执行 `CheckSectionUpdates`
4. 需要双向定位时执行 `LocateSourceElement` / `FindRelatedSections`
5. 高频操作建议执行 `ShowToolbox`

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

---

## 依赖

- AutoCAD .NET API（`acdbmgd.dll` / `acmgd.dll` / `accoremgd.dll`）— 不随仓库分发，见 [build/References/README.md](./build/References/README.md)
- .NET 8 共享运行时中的 `Microsoft.Extensions.*` 程序集（构建后自动复制到插件输出目录）

---

## 许可证

内部工具，仅供学习和参考使用。
