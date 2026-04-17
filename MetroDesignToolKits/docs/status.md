# 项目状态

**版本**: v1.1.0-beta  
**状态**: 当前权威状态文档  
**更新日期**: 2026-04-16

---

## 当前状态

**🚧 内部可用（beta）** — 功能开发完成，架构纠偏完成，尚未正式打包发布

---

## 功能完成情况

| 阶段 | 内容 | 状态 |
|:---|:---|:---|
| 阶段一 | 基础设施（Foundation 层、Bootstrap、DI） | ✅ |
| 阶段二 | 构件识别与转换（图层映射、区域转换、备份恢复） | ✅ |
| 阶段三 | 单层剖面生成（SectionComposer、坡度、标注） | ✅ |
| 阶段四 | 多楼层管理与对齐（MultiFloorSectionComposer、FloorConfig UI） | ✅ |
| 阶段五 | 变更检测与更新（FloorGeometryHasher、XData 快照、CheckSectionUpdates） | ✅ |
| 阶段六 | 双向定位与辅助功能 | ⏳ 未开始 |
| 阶段七 | 集成测试与文档完善 | ⏳ 未开始 |
| 阶段八 | 打包与发布 | ⏳ 未开始 |

---

## 可用命令

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

## 测试状态

| 测试项目 | 通过 / 总计 |
|:---|:---|
| `MetroToolKits.Tests.Foundation` | 49 / 49 ✅ |
| `MetroToolKits.Tests.SectionGenerator.Core` | 51 / 51 ✅ |
| **合计** | **100 / 100 ✅** |

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

---

## 编译状态

```
dotnet build MetroToolKits.sln → 0 错误，~8 警告（AutoCAD DLL 版本兼容性）
```

---

## 下一步

1. 实现阶段六（双向定位：`LocateSourceElement`、`FindRelatedSections`、`ShowToolbox`）
2. 执行阶段七集成测试清单
3. 完成阶段八打包发布，打 Git tag `v1.1.0`
