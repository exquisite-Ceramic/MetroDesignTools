# 项目状态

**版本**: v1.1.0-beta  
**状态**: 当前权威状态文档  
**更新日期**: 2026-04-21

---

## 当前状态

**🚧 内部可用（beta）** — 阶段六已完成，阶段七已补齐文档、回归测试与宿主烟测，阶段八已形成可分发 beta 发布包；本轮已补完架构纠偏，剩余 GUI/交互场景待人工验收

---

## 功能完成情况

| 阶段 | 内容 | 状态 |
|:---|:---|:---|
| 阶段一 | 基础设施（Foundation 层、Bootstrap、DI） | ✅ |
| 阶段二 | 构件识别与转换（图层映射、区域转换、备份恢复） | ✅ |
| 阶段三 | 单层剖面生成（SectionComposer、坡度、标注） | ✅ |
| 阶段四 | 多楼层管理与对齐（MultiFloorSectionComposer、FloorConfig UI） | ✅ |
| 阶段五 | 变更检测与更新（FloorGeometryHasher、XData 快照、CheckSectionUpdates） | ✅ |
| 阶段六 | 双向定位与辅助功能 | ✅ |
| 阶段七 | 集成测试与文档完善 | 🚧 宿主烟测已通过，GUI/交互验收待完成 |
| 阶段八 | 打包与发布 | ✅ 已完成（v1.1.0-beta 本地发布包与 Git tag） |

---

## 可用命令

| 命令 | 说明 |
|:---|:---|
| `GenSection` | 生成多楼层剖面图 |
| `FloorConfig` | 楼层配置管理（增删改楼层、拾取对齐点） |
| `CheckSectionUpdates` | 检测剖面是否需要更新（非模态对话框） |
| `UpdateSection` | 更新单个剖面块 |
| `LocateSourceElement` | 从剖面块内实体定位回源平面构件 |
| `FindRelatedSections` | 从平面构件查询关联剖面并定位剖面块 |
| `ShowToolbox` | 打开停靠式工具箱面板 |
| `LayerMapping` | 全图图层映射管理器 |
| `ConvertRegion` | 区域引导构件转换 |
| `RevertConversion` | 恢复构件转换 |

---

## 测试状态

| 测试项目 | 通过 / 总计 |
|:---|:---|
| `MetroToolKits.Tests.Foundation` | 49 / 49 ✅ |
| `MetroToolKits.Tests.SectionGenerator.Core` | 51 / 51 ✅ |
| **合计** | **100 / 100 ✅** |

> 注：以上为阶段五基线验证结果；阶段六至阶段七已新增 4 个回归测试。当前机器的 `dotnet test` 仍受 NuGet/测试依赖环境影响，尚未刷新最新统计。

---

## 架构纠偏完成情况

| Issue | 内容 | 状态 |
|:---|:---|:---|
| #3 | 修复 IUserLogger DI 注册重复 | ✅ |
| #4 | 删除 CadAdapter 遗留目录 | ✅ |
| #5 | 删除旧 SectionGenerator.* 命名空间和 ServiceLocator | ✅ |
| #6 | 切断 App 层对 Bootstrap 层的项目级依赖 | ✅ |
| #7 | 统一版本号（Directory.Build.props） | ✅ |
| #8 | 建立 build/test 流程（build.ps1 + CI） | ✅ |
| #9 | 明确 AutoCAD DLL 分发策略 | ✅ |
| #10 | 构件转换/恢复/图层映射收回 App 用例层 | ✅ |
| #11 | FloorConfig 加载/保存收回 App 用例层 | ✅ |
| #12 | 配置与日志迁移到按包隔离的用户目录 | ✅ |

---

## 编译状态

历史基线：

```text
dotnet build MetroToolKits.sln → 0 错误，~8 警告（AutoCAD DLL 版本兼容性）
```

当前工作区（2026-04-21）：

> 已在本机完成 `MetroToolKits.SectionGenerator.Plugin.csproj` 编译与发布，结果为 `0 错误`、仅保留 AutoCAD 版本冲突告警；同时通过 AutoCAD 2025 `accoreconsole.exe` 完成 `SectionSelfTest` 宿主烟测。
> 运行期配置和日志现已改为按发布包隔离的 `%LOCALAPPDATA%\MetroToolKits\Packages\<package-scope>\...` 用户目录，不再写安装目录，也不会让不同发布包共享同一份状态。

> `dotnet test` 仍受本机代理与测试依赖环境影响，尚未在本机重新完成刷新。

---

## 下一步

1. 在 `acad.exe` 中执行 [阶段七-验收记录.md](./阶段七-验收记录.md) 里剩余的 GUI/交互验收项
2. 修复当前环境的 NuGet 代理/网络问题后，重新执行 build/test 并刷新测试统计
3. 基于 `v1.1.0-beta` 发布包继续推进正式版验收与版本升级
