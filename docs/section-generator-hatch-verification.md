# Section Generator Hatch 输出验证清单

本文档用于验证 `GenSection` 在开启/关闭填充输出时的行为，重点覆盖 Hatch 创建失败的容错、summary / warning 输出，以及旧绘图失败语义不被误用。

本清单包含 AutoCAD 宿主行为，所有通过结论均需要真实 `acad.exe` 验证。

## 1. 验证前准备

1. 使用 AutoCAD 2018。
2. 在当前分支完成最新编译，并通过 `NETLOAD` 加载最新插件 DLL。
3. 打开用于剖面生成的测试 DWG。
4. 运行 `FloorConfig`，确认至少存在可参与剖面生成的楼层配置、基准点和范围配置。
5. 运行 `SectionPreflight`，记录完整问题清单；若存在阻断项，先修复或记录为本轮验证前置条件。

建议同时准备两类 DWG：

- 正常几何：墙、柱、板边界完整，剖切线明确穿过构件。
- 异常几何：包含端点剖切、极小厚度、重合线、重复转换图层构件或异常填充图案配置。

## 2. 关闭填充验证

### 操作步骤

1. 在输出配置中关闭 `HatchOptions.Enabled`。
2. 运行 `GenSection`。
3. 选择合法剖切线。
4. 指定剖面图插入点。

### 预期结果

- 剖面线和剖面块正常生成。
- 不尝试创建 Hatch。
- 不产生 Hatch summary / Hatch warning。
- 不出现 `SectionGenerator.DrawingOutput.HatchSkipped`。
- 不出现 `SectionGenerator.DrawingOutput.HatchBoundaryInvalid`。
- 不出现 `SectionGenerator.DrawingOutput.HatchPatternFallback`。
- 不出现 `SectionGenerator.DrawingOutput.HatchEvaluateFailed`。

## 3. 打开填充验证：正常场景

### 操作步骤

1. 在输出配置中打开 `HatchOptions.Enabled`。
2. 确认墙、柱、板 Hatch 配置使用 AutoCAD 2018 可用图案，例如 `ANSI31` 或 `SOLID`。
3. 使用合法墙、柱、板剖切线运行 `GenSection`。
4. 指定剖面图插入点。

### 预期结果

- 剖面生成成功。
- 墙、柱、板的 Hatch 能创建成功。
- 不出现 `eInvalidInput`。
- 不出现 `SectionGenerator.DrawingOutput.DrawFailed`。
- Hatch summary 中 `RequestedCount > 0`。
- Hatch summary 中 `CreatedCount > 0`。
- Hatch summary 中 `SkippedCount = 0`。
- 若所有配置图案都可用，`FallbackPatternCount = 0`。

### 需要记录

- 测试 DWG 名称。
- AutoCAD 版本。
- 插件 DLL 路径和编译时间。
- `RequestedCount` / `CreatedCount` / `SkippedCount` / `FallbackPatternCount`。
- 是否存在 warning。

## 4. 打开填充验证：非法边界容错

### 构造场景

选择或构造容易产生退化填充边界的图元：

1. 剖切线正好切到构件端点。
2. 墙厚极小，或墙边线重合。
3. 同一图层上存在重复转换或重叠构件。
4. 将 Hatch pattern 设置为不存在的名称，例如 `MK_NOT_EXIST_PATTERN`。

### 操作步骤

1. 打开 `HatchOptions.Enabled`。
2. 分别对上述异常场景运行 `GenSection`。
3. 观察命令行、用户日志和生成结果。
4. 对不存在的 Hatch pattern 场景，确认 fallback 图案是否生效。

### 预期结果

- 剖面块仍然生成。
- 剖面线、看线、标注等非 Hatch 输出不应因为单个 Hatch 失败而丢失。
- 非法 Hatch 被跳过。
- Hatch pattern 不存在时，优先 fallback 到 `ANSI31`，再 fallback 到 `SOLID`。
- 用户能看到 warning，而不是整个 `GenSection` 失败。
- 如果只有 Hatch 失败，不应出现 `SectionGenerator.DrawingOutput.DrawFailed`。
- 如果存在非法边界，预期出现：
  - `SectionGenerator.DrawingOutput.HatchBoundaryInvalid` 或
  - `SectionGenerator.DrawingOutput.HatchSkipped`
- 如果 pattern fallback 成功，预期出现：
  - `SectionGenerator.DrawingOutput.HatchPatternFallback`
- 如果 Hatch 创建或 Evaluate 失败但块仍成功，预期出现：
  - `SectionGenerator.DrawingOutput.HatchEvaluateFailed`

示例用户可见信息：

```text
剖面已生成，但跳过 1 个非法填充区域。建议检查构件边界或关闭填充后重试。
```

## 5. 回归验证

1. `FloorConfig` 不受影响：
   - 楼层配置可打开、编辑、保存。
   - 对齐点和范围拾取行为不变。

2. `SectionPreflight` 不受影响：
   - 可正常扫描图层、实体类型、楼层配置和剖切前置问题。
   - 输出问题清单不因 Hatch writer 改造而异常。

3. `GenSection` 关闭填充不受影响：
   - 关闭 `HatchOptions.Enabled` 时完全跳过 Hatch 逻辑。
   - 剖面线和块生成路径保持可用。

4. 多楼层生成不受影响：
   - 多楼层堆叠、楼层标注和块插入正常。
   - 单个楼层 Hatch 失败不应导致其他楼层输出失败。
   - Hatch warning 应能反映总的 skipped / fallback 情况。

5. 图层创建不重复异常：
   - 重复运行 `GenSection` 不应因 Hatch 图层已存在而失败。
   - 墙填充、柱填充、楼板填充图层按配置或默认名称正常复用。

6. 旧的 `DrawingOutput.DrawFailed` 语义保留：
   - 只有块创建、事务提交、插入块引用、数据库访问等不可恢复错误才应返回 `SectionGenerator.DrawingOutput.DrawFailed`。
   - 单个 Hatch 边界非法、图案 fallback 或 Hatch Evaluate 失败不应直接升级为整个绘图失败。

## 6. 建议手工验证顺序

1. 正常 DWG，关闭填充，运行 `GenSection`。
2. 正常 DWG，打开填充，使用默认 Hatch 图案运行 `GenSection`。
3. 正常 DWG，打开填充，将 pattern 改为不存在名称，验证 fallback warning。
4. 异常 DWG，打开填充，剖切构件端点，验证非法 Hatch 跳过。
5. 异常 DWG，打开填充，使用极小厚度或重合线构件，验证非法 Hatch 跳过。
6. 多楼层 DWG，打开填充，验证单层异常不影响整体块生成。
7. 再次运行 `FloorConfig` 和 `SectionPreflight`，确认回归路径正常。

## 7. 尚未覆盖风险

- `CadHatchWriter` 的 `AppendLoop` / `EvaluateHatch(true)` 行为必须在真实 AutoCAD 2018 中验证；静态测试无法覆盖 AutoCAD DBObject 生命周期。
- Hatch 图案可用性依赖 AutoCAD 当前环境和 PAT 配置，不同机器可能出现 fallback 差异。
- 目前非法 Hatch warning 以结果汇总为主，若后续需要定位到具体源构件 handle，需要继续扩展 `HatchOutputWarning` 或 Hatch writer 结果。
- 如果所有 Hatch 都失败但线和块成功，当前预期是局部成功；需要确认用户日志和命令行展示足够清晰。
- 多楼层、大量 Hatch 的性能与事务内对象清理仍需要实际 DWG 压力验证。
