# Section Generator 楼层堆叠边界验证清单

本文档用于验证多楼层剖面生成中楼层边界板的输出语义，重点覆盖 `FloorStackLayoutPlan` 与 `FloorStackBoundaryPolicy.ShareInteriorBoundaries` 对边界线、填充和高度累计的影响。

本清单包含 AutoCAD 宿主绘图行为，所有通过结论均需要真实 `acad.exe` 验证。

## 1. 背景

旧逻辑中，每个 `FloorConfig` 都独立生成 `BottomBoundarySlab` 和 `TopBoundarySlab`。在多楼层剖面里，相邻楼层之间会重复表达同一块楼层交界板：下层 `TopBoundarySlab` 生成一次，上层 `BottomBoundarySlab` 再生成一次，同时高度累计也会重复计入上层 bottom structural thickness。

新逻辑引入 Core 层的 `FloorStackLayoutPlan`，由 `FloorStackBoundaryPolicy` 统一决定每层 bottom/top boundary 是否：

- 画线。
- 生成 hatch。
- 参与楼层堆叠高度累计。

默认新策略为 `ShareInteriorBoundaries`：

- 单层：bottom + top 都生成，都参与高度。
- 多层第一层：bottom + top 都生成，都参与高度。
- 多层中间层：bottom 不生成、不填充、不计高；top 生成、填充、计高。
- 多层顶层：bottom 不生成、不填充、不计高；top 生成、填充、计高。

也就是楼层交界板默认由下层 `TopBoundarySlab` 表达一次，上层 `BottomBoundarySlab` 不再重复表达。

兼容策略 `DrawAll` 保留旧行为；若未来增加配置入口，可用于临时回退。

## 2. 验证前准备

1. 使用 AutoCAD 2018。
2. 在当前分支完成最新编译，并通过 `NETLOAD` 加载最新插件 DLL。
3. 打开包含单层、两层和三层配置的测试 DWG。
4. 运行 `FloorConfig`，确认每层楼层高度、bottom/top boundary slab 模板、坡度和结构厚度配置正确。
5. 运行 `SectionPreflight`，记录完整问题清单；若存在阻断项，先修复或记录为本轮验证前置条件。
6. 准备一条可穿过墙、柱或板的合法剖切线，确保 `GenSection` 不会因为没有切到构件而提前失败。

建议分别准备：

- 关闭填充验证用 DWG。
- 开启填充验证用 DWG。
- 带不同 bottom/top 模板的多楼层 DWG。
- 带 2% bottom 或 top 坡度的多楼层 DWG。

## 3. 验证场景：单层

### 操作步骤

1. 配置单层 `F1`。
2. 关闭 `HatchOptions.Enabled`，运行 `GenSection`。
3. 打开 `HatchOptions.Enabled`，再次运行 `GenSection`。

### 预期结果

- `F1` bottom boundary 生成线。
- `F1` top boundary 生成线。
- 关闭填充时不生成 boundary hatch。
- 开启填充时 bottom + top boundary hatch 都生成。
- 楼层高度累计包含 bottom structural thickness + floor height + top structural thickness。
- 不出现缺失 top 或 bottom 的退化情况。

## 4. 验证场景：两层

### 操作步骤

1. 配置两层 `F1`、`F2`。
2. `F1` 和 `F2` 设置明显可见的 bottom/top boundary 厚度。
3. 分别在关闭填充和开启填充状态下运行 `GenSection`。

### 预期结果

- `F1`：bottom + top 都生成。
- `F2`：只生成 top boundary。
- `F2` 不应重复生成 bottom boundary。
- `F1` top 与 `F2` bottom 的楼层交界处不应出现双板、双填充或重复线。
- 开启填充时，楼层交界处只应有下层 `F1.TopBoundarySlab` 对应的 hatch。
- 堆叠高度不再重复计入 `F2.BottomBoundarySlab` 的 structural thickness。

## 5. 验证场景：三层

### 操作步骤

1. 配置三层 `F1`、`F2`、`F3`。
2. 使用相同剖切线运行 `GenSection`。
3. 对比关闭填充和开启填充输出。

### 预期结果

- `F1`：bottom + top 都生成。
- `F2`：top only。
- `F3`：top only。
- `F2` 和 `F3` 的 bottom boundary 不应输出线。
- `F2` 和 `F3` 的 bottom boundary 不应生成 hatch。
- 中间楼层底部不应重复画板。
- 多楼层堆叠不应因为 suppress bottom 而出现楼层塌缩或错位。

## 6. 验证场景：不同模板

### 操作步骤

1. `F1.BottomBoundarySlab` 使用底板模板。
2. `F1.TopBoundarySlab` 使用层间板模板。
3. `F2.TopBoundarySlab` 使用层间板或屋面模板。
4. 如有三层，`F3.TopBoundarySlab` 使用屋面模板。
5. 开启填充运行 `GenSection`。

### 预期结果

- `F1` bottom 按底板模板输出。
- `F1` top 按层间板模板输出。
- `F2` bottom 不输出，即使它配置了 bottom 模板。
- `F2` top 按自身 top boundary 模板输出。
- 顶层 top 按屋面或顶板模板输出。
- hatch 边界与对应 boundary 模板厚度一致。
- suppress 的 bottom 模板不应通过线、hatch 或高度累计间接出现。

## 7. 验证场景：坡度 2%

### 操作步骤

1. 在单层或多层中设置 `BottomBoundarySlab.SlopeEnabled = true`，`SlopeValue = 0.02`。
2. 运行 `GenSection`，检查 bottom boundary 的线和 hatch。
3. 设置 `TopBoundarySlab.SlopeEnabled = true`，`SlopeValue = 0.02`。
4. 运行 `GenSection`，检查 top boundary 的线和 hatch。
5. 在多层场景中，对非首层配置 bottom slope，再运行 `GenSection`。

### 预期结果

- bottom slope 只影响 bottom boundary。
- top slope 只影响 top boundary。
- 被生成的 top boundary 线和 top hatch 边界使用同一套竖向 profile，不应出现线条有坡度但 hatch 仍为水平矩形。
- 被生成的 bottom boundary 线和 bottom hatch 边界使用同一套竖向 profile。
- 非首层 bottom boundary 被 `ShareInteriorBoundaries` suppress 时，即使配置了 bottom slope，也不输出 bottom 线或 bottom hatch。
- 开启填充时不应因为坡度板 hatch 边界导致 `eInvalidInput`。

## 8. 验证场景：高度累计

### 操作步骤

1. 设置明显不同的 structural thickness，例如：
   - bottom structural thickness = 100。
   - floor height = 3000。
   - top structural thickness = 200。
2. 配置两层或三层。
3. 运行 `GenSection`，检查楼层之间的堆叠距离。
4. 对照 `DrawAll` 旧行为预期，仅用于人工对比，不作为默认输出要求。

### 预期结果

- 单层高度贡献为 `bottom structural thickness + floor height + top structural thickness`。
- 多层第一层高度贡献包含 bottom + floor height + top。
- 多层非首层高度贡献不包含 bottom structural thickness，只包含 floor height + top structural thickness。
- 三层示例中，如果每层 bottom=100、height=3000、top=200：
  - `F1` base elevation = 0。
  - `F2` base elevation = 3300。
  - `F3` base elevation = 6500。
  - total height = 9700。
- 不应再出现每层都按 3300 累加导致的重复 bottom 厚度。

## 9. 回归验证

1. `GenSection` 正常生成：
   - 剖面块可创建。
   - 楼层线、构件剖切线、看线和标注仍可输出。

2. `SectionPreflight` 不受影响：
   - 楼层配置、图层、构件类型和剖切线前置检查仍正常。

3. Hatch 容错不受影响：
   - 单个非法 hatch 不应导致整个剖面生成失败。
   - 开启填充时仍应使用现有 hatch warning / summary。

4. `SightLineOptions` 不受影响：
   - 视深看线是否生成仍由 sightline 逻辑控制。
   - 楼层边界 suppress 不应改变墙柱看线行为。

5. 复制墙模板绑定不受影响：
   - 复制墙缺少模板元数据时仍按现有检查、弹窗绑定或识别兜底处理。
   - Floor stack policy 不应绕过模板元数据检查。

6. `DrawAll` 兼容策略：
   - 如未来启用 `DrawAll`，应可恢复旧行为：每层 bottom/top 都输出、都填充、都计高。
   - 默认验证仍以 `ShareInteriorBoundaries` 为准。

## 10. 建议手工验证顺序

1. 单层，关闭填充，运行 `GenSection`。
2. 单层，打开填充，运行 `GenSection`。
3. 两层，关闭填充，检查 `F2` bottom 不重复画线。
4. 两层，打开填充，检查楼层交界处不重复 hatch。
5. 三层，打开填充，检查 `F2` / `F3` bottom 均不输出。
6. 不同模板场景，检查 bottom/top 模板是否按楼层角色输出。
7. 坡度 2% 场景，检查线和 hatch 边界一致。
8. 高度累计场景，检查多楼层堆叠距离。
9. 运行 `SectionPreflight`，确认回归路径正常。
10. 使用复制墙测试图运行 `GenSection`，确认模板绑定提示不受影响。

## 11. 尚未覆盖风险

- AutoCAD 实际 hatch 边界、块插入和图层输出必须在真实 AutoCAD 2018 中验证，`dotnet test` 只能覆盖 Core 几何语义。
- 坡度 2% 的视觉一致性需要检查真实 DWG 中 hatch loop 与线条端点是否完全贴合。
- 不同模板组合可能涉及 finish layer、structural layer 和 wall junction mode，仍需用真实模板目录验证。
- `DrawAll` 目前作为兼容策略保留，若未来加入配置入口，需要补充 UI / App 配置保存与读取验证。
- 多楼层大量构件场景仍需验证性能和用户提示清晰度。
