# SectionGenerator 用户手册

**版本**: v1.1.0-beta  
**适用对象**: AutoCAD 最终使用人员

---

## 1. 安装准备

### 1.1 环境要求

- Windows 10/11
- AutoCAD 2018+
- .NET 8.0 SDK（仅本地构建需要）

### 1.2 加载方式

1. 按 [构建与测试指南.md](./构建与测试指南.md) 准备 AutoCAD 依赖 DLL
2. 构建输出插件
3. 在 AutoCAD 中执行 `NETLOAD`
4. 选择 `MetroToolKits.Bootstrap.dll`

---

## 2. 首次使用建议流程

1. 执行 `FloorConfig`，配置楼层、层高、楼板厚度和对齐点
2. 如原图图层不规范，执行 `LayerMapping`
3. 如需批量规范化图元，执行 `ConvertRegion`
4. 执行 `GenSection` 生成剖面
5. 执行 `CheckSectionUpdates` 检查是否需要更新
6. 如需双向定位，使用 `LocateSourceElement` 或 `FindRelatedSections`
7. 日常使用可执行 `ShowToolbox` 打开工具箱面板

---

## 3. 命令说明

| 命令 | 用途 | 典型使用场景 |
|:---|:---|:---|
| `GenSection` | 生成多楼层剖面图 | 从平面剖切线生成剖面块 |
| `FloorConfig` | 楼层配置管理 | 设置楼层高度、板厚、对齐点 |
| `CheckSectionUpdates` | 检测剖面更新状态 | 平面调整后批量检查 |
| `UpdateSection` | 更新单个剖面块 | 只更新一个指定剖面 |
| `LocateSourceElement` | 从剖面定位源构件 | 反查剖面线对应平面墙/柱 |
| `FindRelatedSections` | 从平面查询关联剖面 | 查某个平面构件被哪些剖面引用 |
| `ShowToolbox` | 打开工具箱 | 常用命令集中入口 |
| `LayerMapping` | 图层映射管理 | 规范化原图图层 |
| `ConvertRegion` | 区域转换 | 对选区内图元做构件类型转换 |
| `RevertConversion` | 恢复转换 | 撤销区域转换结果 |

---

## 4. 典型操作示例

### 4.1 示例一：生成多楼层剖面

1. 执行 `FloorConfig`
2. 配置楼层：
   - `B1`
   - `1F`
   - `2F`
3. 为每层记录三点对齐
4. 执行 `GenSection`
5. 按提示依次：
   - 选择剖切线
   - 输入视图深度
   - 指定剖面插入点
6. 检查生成块名称、楼层标高与板线是否正确

### 4.2 示例二：平面变更后批量更新

1. 调整平面中的墙线或柱子位置
2. 执行 `CheckSectionUpdates`
3. 在列表中查看哪些剖面为“需更新”
4. 点击“更新所选”或“全部更新”
5. 再次刷新，确认状态恢复为最新

### 4.3 示例三：从剖面回查平面构件

1. 执行 `LocateSourceElement`
2. 在剖面块内部选择一条墙线或柱线
3. 系统自动缩放到平面中的源构件并高亮

### 4.4 示例四：从平面查询关联剖面

1. 执行 `FindRelatedSections`
2. 选择平面中的墙、柱等源构件
3. 在“关联剖面”窗口中查看引用该构件的剖面列表
4. 双击某项或点击“定位选中剖面”跳转

---

## 5. 工具箱使用

执行 `ShowToolbox` 后，会打开停靠式工具箱面板，包含三组入口：

- 剖面：生成、检测更新、更新剖面
- 双向定位：剖面定位源构件、查询关联剖面
- 转换与配置：楼层配置、图层映射、区域转换、恢复转换

适合频繁切换命令的日常操作。

---

## 6. 常见问题

### Q1. `GenSection` 提示没有识别到构件

- 检查剖切线是否确实穿过目标构件
- 检查图层是否符合 `MK_结构墙` / `MK_结构柱` / `MK_楼板` 前缀
- 检查楼层对齐点是否设置错误

### Q2. `CheckSectionUpdates` 看不到需要更新的剖面

- 确认该剖面是通过本插件生成的
- 确认快照未被删除
- 确认平面修改发生在剖切线覆盖的构件上

### Q3. `LocateSourceElement` 无法定位

- 所选对象必须是剖面块中的构件线，不是普通标注文字
- 若对象不是插件生成，可能没有源句柄 XData

### Q4. `FindRelatedSections` 结果为空

- 该构件可能未被任何剖面引用
- 对应剖面块的快照可能不存在

---

## 7. 相关文档

- [构建与测试指南.md](./构建与测试指南.md)
- [阶段七-验收记录.md](./阶段七-验收记录.md)
- [architecture.md](./architecture.md)
