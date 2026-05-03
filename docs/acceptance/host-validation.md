# Host Validation Draft

## ViewDepth SightLines Status

- Phase A completed the Core input boundary for cut elements and sightline candidates.
- Phase B now generates minimal Core `ElementSectionData.SightLines` from `SectionSightLineCandidate`.
- The first projection rule is intentionally conservative: candidate chainage range becomes section-local X, element vertical range becomes section-local Y, and Core emits a rectangle outline only.
- Occlusion, hidden-line handling, front/back ordering, and type-specific sightline styling are not implemented.
- New snapshots now store independent sightline hash fields, and `CheckSectionUpdates` compares them when V2 recognition and matching hash version are available.
- Phase C1 introduced independent sightline hash model fields and hashing tests only.
- Phase C2 writes `SightLineGeometryHash`, `SightLineElementCount`, `SightLineSourceElementHandles`, and `SightLineHashVersion` for newly generated snapshots only.
- Phase C3 compares `SightLineGeometryHash` in `CheckSectionUpdates`; legacy snapshots, unavailable V2 recognizers, and hash-version mismatches remain `Unknown` / warning paths instead of direct `Outdated`.
- `UpdateSectionUseCase` still has no dedicated sightline update semantics; it remains a follow-up stage.
- `CadDrawingService` was not changed in Phase B. Real AutoCAD drawing behavior remains Pending: 需要真实 `acad.exe` 验证.

本文档是 P4.1 的宿主验收与发布输出检查草案，目标是先确认 MetroDesignToolKits / SectionGenerator 在真实 AutoCAD 宿主中的构建入口、发布目录、`NETLOAD` 入口 DLL、基础命令清单，以及后续 `accoreconsole.exe` 自动化验收的落点。

## Build Commands

当前仓库根目录已有统一脚本：

- `.\build.ps1 build`
  同步 AutoCAD 引用并编译 SectionGenerator 插件项目（Debug）。
- `.\build.ps1 test`
  同步 AutoCAD 引用、构建解决方案并运行单元测试。
- `.\build.ps1 publish`
  同步 AutoCAD 引用，编译 Bootstrap / Plugin（Release），并输出发布包到 `publish/SectionGenerator`。

对应的直接 `dotnet` 入口：

- `dotnet build .\src\Plugins\SectionGenerator\MetroToolKits.SectionGenerator.Plugin\MetroToolKits.SectionGenerator.Plugin.csproj -c Debug`
- `dotnet build .\src\Bootstrap\MetroToolKits.Bootstrap\MetroToolKits.Bootstrap.csproj -c Release`
- `dotnet publish .\src\Plugins\SectionGenerator\MetroToolKits.SectionGenerator.Plugin\MetroToolKits.SectionGenerator.Plugin.csproj -c Release -o .\publish\SectionGenerator`

说明：

- `build.ps1 publish` 在 `dotnet publish` 插件后，会额外把 Bootstrap 工件复制到 `publish/SectionGenerator`。
- 如 `build.ps1` 当前生成宿主自动化专用 Bootstrap 目录，则会输出到 `publish/HostAutomationBootstrap`。

## Output DLLs

当前需要关注两个最终输出 DLL：

- Bootstrap 入口 DLL
  `publish/SectionGenerator/MetroToolKits.Bootstrap.dll`
- SectionGenerator 插件 DLL
  `publish/SectionGenerator/MetroToolKits.SectionGenerator.Plugin.dll`

构建时的默认输出路径：

- Debug Bootstrap
  `src/Bootstrap/MetroToolKits.Bootstrap/bin/Debug/net8.0-windows`
- Release Bootstrap
  `src/Bootstrap/MetroToolKits.Bootstrap/bin/Release/net8.0-windows`
- Debug Plugin
  `src/Plugins/SectionGenerator/MetroToolKits.SectionGenerator.Plugin/bin/Debug/net8.0-windows`
- Release publish package
  `publish/SectionGenerator`

说明：

- `MetroToolKits.SectionGenerator.Plugin.dll` 不是 AutoCAD 直接 `NETLOAD` 的首选入口。
- Bootstrap 会在自身所在目录扫描 `MetroToolKits.*.Plugin.dll`，并把插件装入 DI / 命令注册链路。
- 因此发布包必须至少保证 `MetroToolKits.Bootstrap.dll` 与 `MetroToolKits.SectionGenerator.Plugin.dll` 同目录分发。

## NETLOAD Target

AutoCAD 中应 `NETLOAD` 的 DLL：

- `publish/SectionGenerator/MetroToolKits.Bootstrap.dll`

原因：

- `MetroToolKits.Bootstrap` 带有 `ExtensionApplication` 宿主入口。
- `MetroToolKits.Bootstrap` 带有 `CommandClass(BootstrapCommandBridge)`，负责向 AutoCAD 暴露外部命令。
- `Startup.Initialize()` 会在 Bootstrap 所在目录扫描并加载 `MetroToolKits.*.Plugin.dll`。

不建议直接 `NETLOAD`：

- `publish/SectionGenerator/MetroToolKits.SectionGenerator.Plugin.dll`

## NETLOAD Manual Steps

1. 先执行 `.\build.ps1 publish`，确认发布目录为 `publish/SectionGenerator`。
2. 打开 `acad.exe`。
3. 在命令行输入 `NETLOAD`。
4. 选择 `publish/SectionGenerator/MetroToolKits.Bootstrap.dll`。
5. 观察命令行与日志目录，确认未出现 Bootstrap 初始化失败、命令注册失败、插件发现失败等错误。

建议同时检查以下日志文件：

- `publish/SectionGenerator/logs/MetroToolKits.log`
- `publish/SectionGenerator/logs/MetroToolKits_User.log`

## Commands To Validate

P4 手工宿主验收建议至少覆盖以下命令：

- `ShowToolbox`
- `FloorConfig`
- `LayerMapping`
- `SectionPreflight`
- `CheckBeforeGenerate`
- `GenSection`
- `CheckSectionUpdates`
- `UpdateSection`
- `LocateSourceElement`
- `FindRelatedSections`
- `ConvertRegion`
- `RevertConversion`
- `RevertAllConversions`

仅用于宿主自检 / 自动化验收的内部命令：

- `MKSectionSelfTestInternal`
- `MKSectionHostAcceptanceInternal`

说明：

- 内部命令用于宿主自检 / 自动化验收；是否出现在普通发布包中，以当前 Bootstrap 构建配置和命令注册结果为准。
- 普通发布包侧重人工 `NETLOAD` 和手工命令验收；后续 `accoreconsole.exe` 计划可优先使用宿主自动化专用 Bootstrap。

## acad.exe Manual Validation Checklist

建议在真实 `acad.exe` 中按以下顺序验收：

1. `NETLOAD` Bootstrap 成功，命令行无初始化异常。
2. `ShowToolbox` 可打开工具箱。
3. `FloorConfig` 可打开配置界面，关闭不崩溃。
4. `LayerMapping` 可打开映射界面，关闭不崩溃。
5. `SectionPreflight` 或 `CheckBeforeGenerate` 可打开预检面板，并显示当前图纸检查结果。
6. `FloorConfig` 执行保存草稿后，关闭并重新打开当前 DWG，确认配置可回读。
7. `GenSection` 在缺少前置条件时，仍按现有行为失败并给出提示。
8. `CheckSectionUpdates` / `UpdateSection` 在无现有剖面、已有剖面两种情况下至少各验证一次不会直接崩溃。
9. `ConvertRegion` / `RevertConversion` / `RevertAllConversions` 至少验证命令入口可执行。
10. 命令执行后检查日志文件，确认没有持续性初始化错误、依赖缺失错误、反射命令注册错误。

建议重点记录的宿主现象：

- 首次 `NETLOAD` 是否成功
- 首次命令调用是否触发 Bootstrap 初始化
- 各窗口是否能正常打开
- 配置是否能写入当前 DWG
- 重新打开图纸后是否能读取先前配置
- 发布目录是否缺少任何必须 DLL / JSON 资源

## Host Validation Plan Index

- `P4.2e - GenSection Host Validation Plan`
- `P4.2e - GenSection Manual Host Validation Record`
- `P4.2f - CheckSectionUpdates Host Validation Plan`
- `P4.2f - CheckSectionUpdates Manual Host Validation Record`
- `P4.2g - UpdateSection Host Validation Plan`
- `accoreconsole.exe Follow-up Plan`

## P0 Follow-up Notes

- `FloorConfig` 全局顶板/底板坡度的保存、重新打开回读、以及生成执行态取值一致性，仍需在真实 `acad.exe` 中完成手工宿主验证。
  当前状态：`Pending manual host validation`
- `viewDepth` 当前已在 `GenerateSection`、快照、`CheckSectionUpdates`、`UpdateSection` 链路中传递；`SectionViewDepthFilter` 纯几何 helper 已实现并有单元测试覆盖，但 `LayerBasedElementRecognizer` 业务语义仍未闭环，因为当前识别仍依赖 `IntersectsSection`。
  当前状态：`Pending manual host validation`
  说明：已引入 `SectionRecognitionSet` / `ViewDepthCandidate` 模型分离基础，`LayerBasedElementRecognizer` V2 可产出 viewDepth candidates；旧生成链路仍不消费 candidates，不与剖切线相交但位于 `viewDepth` strip 内的元素目前不会生成 `SightLines`；投影看线未实现，hash/update detection 尚未纳入 sightline geometry，仍需要真实 `acad.exe` 验证。

## viewDepth SightLines Stage A Status

- This section supersedes the preceding P0 wording that said the generation pipeline did not consume candidates.
- Stage A has introduced a Core input boundary for separated cut elements and sight-line candidates.
- App-layer `ViewDepthCandidate` data is mapped into Core-layer `SectionSightLineCandidate` data before section composition.
- Stage A does not generate `ElementSectionData.SightLines`.
- Stage A does not change drawing output, layer options, source-ref XData, `SectionSnapshot`, `FloorGeometryHasher`, `CheckSectionUpdates`, or `UpdateSection`.
- SightLines projection remains pending.
- snapshot/hash/update detection still does not include sight-line geometry.
- AutoCAD host validation remains `Pending manual host validation`; this still requires real `acad.exe` validation.

## P4.2b Manual NETLOAD Result

当前手动宿主验收记录如下：

- `build.ps1 publish` 已通过。
- `publish/SectionGenerator/MetroToolKits.Bootstrap.dll` 存在。
- `publish/SectionGenerator/MetroToolKits.SectionGenerator.Plugin.dll` 存在。
- AutoCAD `NETLOAD` Bootstrap 成功。
- `ShowToolbox` 可打开。
- `FloorConfig` 可打开。
- `LayerMapping` 可打开。
- `SectionPreflight` 可打开。
- `CheckBeforeGenerate` 可打开。
- 上述窗口关闭后未出现宿主崩溃。
- 日志未见以下宿主加载类错误：
  - Bootstrap 初始化失败
  - 插件发现失败
  - 依赖缺失
  - 命令注册失败

当前结论：

- `Passed`

## P4.2c DWG FloorConfig Persistence Result

当前手动持久化验收记录如下：

- `FloorConfig` 可打开。
- 可为 `F1` 设置对齐点。
- 可保存 `FloorConfig`。
- 日志已出现 `Floor config saved`，且记录 `floorCount=1`、`floors=F1`。
- 当前日志未见 `FloorConfig` 保存异常。

关闭并重新打开 DWG 后的配置回读结论：

- 如人工确认关闭并重新打开 DWG 后 `F1` 配置仍可回读，则该项结论记为 `Passed`。
- 如尚未确认关闭重开回读，则该项结论记为 `Partial`，并标记 `manual reopen/readback pending`。

## P4.2d Preflight Host Result

当前手动预检宿主验收记录如下：

- `SectionPreflight` 或 `CheckBeforeGenerate` 可打开。
- 可基于当前 DWG / 当前 `FloorConfig` 显示预检结果。
- 可显示 `CanGenerate` / `Blocking` / `Warning` / `Info` 等检查信息。
- 对当前图纸缺失前置条件的情况，可给出 `warning` / `blocking`，而不是崩溃。
- 窗口关闭后未出现宿主崩溃。
- 当前日志未见 `Preflight` 执行异常。

当前结论：

- `Passed`

## P4.2e - GenSection Host Validation Plan

本节只新增 `GenSection` 的宿主验收计划，不代表当前已完成手动验证。

执行前统一要求：

- `NETLOAD` 必须加载 `publish/SectionGenerator/MetroToolKits.Bootstrap.dll`，不要直接加载 `publish/SectionGenerator/MetroToolKits.SectionGenerator.Plugin.dll`。
- 验收宿主为真实 `acad.exe`。
- 每次执行后都应检查 `publish/SectionGenerator/logs/MetroToolKits.log` 与 `publish/SectionGenerator/logs/MetroToolKits_User.log`。
- 对于尚未手动执行的项，状态统一标记为 `Pending manual host validation`。

### Controlled Failure

#### Missing Section Line

前置条件：

- 已在 AutoCAD 中 `NETLOAD` `publish/SectionGenerator/MetroToolKits.Bootstrap.dll`。
- 当前 DWG 已存在可读取的 `FloorConfig`。
- 当前 DWG 未提供可被 `GenSection` 识别的剖切线。
- 可保留其他最小宿主条件，以便把失败原因聚焦到“缺少剖切线”。

操作步骤：

1. 打开最小测试 DWG。
2. 执行 `GenSection`。
3. 按命令要求完成最少必要输入，但不补建剖切线。
4. 记录命令行提示、对话框提示、warning、diagnostics 与日志输出。

预期结果：

- `GenSection` 给出明确提示、diagnostics、warning 或业务失败信息，指出缺少剖切线或无法定位剖切线前置条件。
- AutoCAD 宿主不崩溃。
- 插件不导致 fatal error。
- 不出现未处理异常弹窗。

通过标准：

- 命令失败为可诊断的业务失败，而不是宿主级异常。
- 用户能从命令输出、界面提示或日志中判断失败原因与下一步修复方向。
- 失败后可继续在同一宿主会话中输入其他命令。

日志检查点：

- 存在可关联到 `GenSection` 的命令开始、前置条件检查或失败记录。
- 日志中能看到“缺少剖切线”或等价业务诊断信息。
- 日志中不应出现 fatal error、未处理异常、Bootstrap 初始化失败或插件发现失败。

当前状态：

- `Pending manual host validation`

#### Missing Scope

前置条件：

- 已在 AutoCAD 中 `NETLOAD` `publish/SectionGenerator/MetroToolKits.Bootstrap.dll`。
- 当前 DWG 已存在可读取的 `FloorConfig`。
- 当前 DWG 已存在可识别的剖切线。
- 当前 DWG 未提供 `GenSection` 所需的范围信息，或范围选择步骤被故意留空。

操作步骤：

1. 打开最小测试 DWG。
2. 执行 `GenSection`。
3. 在命令执行过程中保持“缺少范围”的状态。
4. 记录命令行提示、对话框提示、warning、diagnostics 与日志输出。

预期结果：

- `GenSection` 给出明确提示、diagnostics、warning 或业务失败信息，指出缺少范围或范围不足。
- AutoCAD 宿主不崩溃。
- 插件不导致 fatal error。
- 不出现未处理异常弹窗。

通过标准：

- 命令以业务失败或可理解的 warning 结束，而不是以宿主异常结束。
- 用户可从提示或日志中识别缺少范围这一失败原因。
- 命令结束后宿主仍可继续使用。

日志检查点：

- 存在与 `GenSection` 相关的范围检查或失败记录。
- 日志中能看到“缺少范围”或等价业务诊断信息。
- 日志中不应出现 fatal error、未处理异常或反射命令注册错误。

当前状态：

- `Pending manual host validation`

#### Missing Reference Floor

前置条件：

- 已在 AutoCAD 中 `NETLOAD` `publish/SectionGenerator/MetroToolKits.Bootstrap.dll`。
- 当前 DWG 已存在可识别的剖切线。
- 当前 DWG 已具备最小范围信息。
- 当前 DWG 缺少 `GenSection` 所需的基准层，或 `FloorConfig` 中不存在可供命令解析的基准层配置。

操作步骤：

1. 打开最小测试 DWG。
2. 执行 `GenSection`。
3. 保持“缺少基准层”的图纸状态完成命令触发。
4. 记录命令行提示、对话框提示、warning、diagnostics 与日志输出。

预期结果：

- `GenSection` 给出明确提示、diagnostics、warning 或业务失败信息，指出缺少基准层或基准层解析失败。
- AutoCAD 宿主不崩溃。
- 插件不导致 fatal error。
- 不出现未处理异常弹窗。

通过标准：

- 失败原因为可诊断的业务前置条件缺失。
- 用户可从提示、warning 或日志中区分该失败与“缺少剖切线”“缺少范围”等其他场景。
- 失败后宿主仍稳定可用。

日志检查点：

- 存在基准层读取、基准层解析或前置条件失败记录。
- 日志中能看到“缺少基准层”或等价业务诊断信息。
- 日志中不应出现 fatal error、未处理异常或依赖缺失错误。

当前状态：

- `Pending manual host validation`

#### Missing Elements

前置条件：

- 已在 AutoCAD 中 `NETLOAD` `publish/SectionGenerator/MetroToolKits.Bootstrap.dll`。
- 当前 DWG 已存在可读取的 `FloorConfig`。
- 当前 DWG 已存在可识别的剖切线。
- 当前 DWG 已存在最小范围与基准层条件。
- 当前范围内不存在可被 `GenSection` 识别、提取或转换的构件。

操作步骤：

1. 打开最小测试 DWG。
2. 执行 `GenSection`。
3. 让命令在“无构件”条件下完成前置检查或生成尝试。
4. 记录命令行提示、对话框提示、warning、diagnostics 与日志输出。

预期结果：

- `GenSection` 给出明确提示、diagnostics、warning 或业务失败信息，指出缺少构件、未识别构件或无可生成对象。
- AutoCAD 宿主不崩溃。
- 插件不导致 fatal error。
- 不出现未处理异常弹窗。

通过标准：

- 命令结果能被识别为受控失败或业务 warning，而不是未处理异常。
- 用户可从输出或日志中理解失败原因与后续补救方向。
- 宿主在命令结束后仍可继续响应其他命令。

日志检查点：

- 存在构件扫描、构件识别、构件转换或“无可用构件”记录。
- 日志中能看到“缺少构件”或等价业务诊断信息。
- 日志中不应出现 fatal error、未处理异常或宿主崩溃前兆。

当前状态：

- `Pending manual host validation`

### Minimal Success / Partial Success

最小测试 DWG 的要求：

- 使用真实 `acad.exe` 可正常打开的最小 DWG。
- DWG 中已保存至少一份可读取的 `FloorConfig`。
- DWG 中已存在至少一条可被 `GenSection` 识别的剖切线。
- DWG 中至少存在一个位于有效范围内的简单构件，且该构件应满足“可识别”或“可转换”二者之一。
- 如无法稳定准备可转换构件，也可先以“可识别但可能无法完整生成”为目标，验证部分成功或业务 warning 路径。

FloorConfig 的最低要求：

- 当前 DWG 中存在一份可回读的 `FloorConfig`。
- `FloorConfig` 至少包含 `GenSection` 执行所需的最小楼层信息。
- 如命令依赖基准层、对齐点或楼层映射，则应准备一组最小但自洽的配置。
- 不要求在本计划中扩展命令名称或引入新的配置入口。

剖切线的最低要求：

- 至少存在一条可由当前 `GenSection` 实现识别的剖切线。
- 剖切线位置应落在最小测试场景内，并能与待处理范围形成明确关系。
- 剖切线不要求覆盖复杂场景，但应足以触发一次完整生成或部分生成流程。

简单构件的最低要求：

- 至少存在一个几何简单、便于识别的构件。
- 该构件位于剖切线和目标范围关联区域内。
- 构件可以是当前实现已知可识别对象，或可被转换管线接受的最小对象。
- 如当前对象只能触发部分成功，也应保留，以验证 warning 与日志可诊断性。

操作步骤：

1. 执行 `.\build.ps1 publish`，确认发布目录为 `publish/SectionGenerator`。
2. 打开 `acad.exe` 并加载最小测试 DWG。
3. 通过 `NETLOAD` 加载 `publish/SectionGenerator/MetroToolKits.Bootstrap.dll`，不要直接加载插件 DLL。
4. 先确认 `FloorConfig`、剖切线与最小构件条件已准备完成。
5. 执行 `GenSection`。
6. 记录命令行输出、界面提示、生成结果、warning、diagnostics 与日志。
7. 如命令未生成完整结果，但产生业务 warning 或部分输出，也记录该结果，不将其误记为 `Passed`。

预期结果：

- `GenSection` 能执行完成；或者
- 在没有构件、构件不足、构件无法转换时产生业务 warning；但
- AutoCAD 宿主不能崩溃。
- 不能出现 fatal error。
- 不能出现未处理异常。
- 生成结果、warning、日志至少有一个能说明执行结果。

通过标准：

- 完整成功路径下，命令执行结束后可观察到生成结果，且日志未出现宿主级异常。
- 部分成功路径下，即使没有完整生成结果，也至少存在业务 warning、diagnostics 或可读日志来解释执行结果。
- 无论完整成功还是部分成功，宿主在命令结束后都保持稳定，不出现 fatal error 或未处理异常。

日志检查点：

- 存在与 `GenSection` 相关的命令开始、前置检查、构件识别、生成完成或 warning 记录。
- 如发生部分成功，应能在日志中看到构件不足、构件无法转换或等价业务 warning。
- 日志中不应出现 fatal error、未处理异常、Bootstrap 初始化失败或插件发现失败。

当前状态：

- `Pending manual host validation`

### Next actions

- 准备一个最小测试 DWG，至少包含可回读的 `FloorConfig`、一条可识别剖切线，以及一个可识别或可转换的简单构件。
- 在真实 `acad.exe` 中按本节步骤执行 `GenSection`，补充手动验收记录，并明确区分 controlled failure、partial success 与 full success。
- 后续可评估是否通过宿主自动化专用 Bootstrap 与 `.scr` 脚本，把 `GenSection` 最小 smoke 场景接入 `accoreconsole.exe`。

## P4.2e - GenSection Manual Host Validation Record

本节用于后续真实 AutoCAD 手工宿主验收时填写记录；当前不代表已执行验收。

### Execution Context

- Date: `TBD`
- Validator: `TBD`
- Git commit: `TBD`
- Build command: `TBD`
- Publish command: `TBD`
- AutoCAD host: `TBD`
- Loaded DLL: `publish/SectionGenerator/MetroToolKits.Bootstrap.dll`
- Test DWG: `TBD`
- Log files checked: `TBD`
- Notes: `TBD`
- Current status: `Pending manual host validation`

说明：

- 应通过 `NETLOAD` 加载 `publish/SectionGenerator/MetroToolKits.Bootstrap.dll`。
- 不应直接加载 `publish/SectionGenerator/MetroToolKits.SectionGenerator.Plugin.dll`。

### Controlled Failure Record

| Scenario | Test DWG | Expected Result | Actual Result | Log Check | Status | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| Missing Section Line | `TBD` | 不崩溃，给出业务 warning 或 diagnostics | `TBD` | `TBD` | `Pending manual host validation` | `TBD` |
| Missing Scope | `TBD` | 不崩溃，给出业务 warning 或 diagnostics | `TBD` | `TBD` | `Pending manual host validation` | `TBD` |
| Missing Reference Floor | `TBD` | 不崩溃，给出业务 warning 或 diagnostics | `TBD` | `TBD` | `Pending manual host validation` | `TBD` |
| Missing Elements | `TBD` | 不崩溃，给出业务 warning 或 diagnostics | `TBD` | `TBD` | `Pending manual host validation` | `TBD` |

### Minimal Success / Partial Success Record

| Scenario | Test DWG | Expected Result | Actual Result | Generated Output | Log Check | Status | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Minimal DWG with FloorConfig + section line + simple recognizable element | `TBD` | 命令可执行完成，或至少以可诊断结果结束且宿主不崩溃 | `TBD` | `TBD` | `TBD` | `Pending manual host validation` | `TBD` |
| Minimal DWG with FloorConfig + section line but no convertible element | `TBD` | 给出业务 warning 或 diagnostics，宿主不崩溃 | `TBD` | `TBD` | `TBD` | `Pending manual host validation` | `TBD` |
| Minimal DWG with FloorConfig + section line + element conversion warning | `TBD` | 产生可诊断 warning 或部分输出，宿主不崩溃 | `TBD` | `TBD` | `TBD` | `Pending manual host validation` | `TBD` |

### Manual Validation Checklist

- [ ] Run `.\build.ps1 publish`
- [ ] Start `acad.exe`
- [ ] Load `publish/SectionGenerator/MetroToolKits.Bootstrap.dll` with `NETLOAD`
- [ ] Do not directly load plugin DLL
- [ ] Open controlled failure DWG
- [ ] Execute `GenSection`
- [ ] Record command line output
- [ ] Record UI diagnostics or warning
- [ ] Check `publish/SectionGenerator/logs/MetroToolKits.log`
- [ ] Check `publish/SectionGenerator/logs/MetroToolKits_User.log`
- [ ] Confirm AutoCAD host did not crash
- [ ] Confirm no fatal error
- [ ] Confirm no unhandled exception dialog
- [ ] Record final status

### Result Classification

- `Passed`: 只有真实手动验收完成，且满足通过标准时才能使用。
- `Controlled Failure`: 命令按业务失败或 warning 结束，宿主不崩溃。
- `Partial Success`: 命令执行到部分流程，产生 warning 或部分输出，宿主不崩溃。
- `Failed`: 出现宿主崩溃、fatal error、未处理异常，或业务结果不可诊断。
- `Pending manual host validation`: 尚未实际执行手工宿主验收。

## P4.2f - CheckSectionUpdates Host Validation Plan

本节只新增 `CheckSectionUpdates` 的宿主验收计划，不代表当前已完成手动验证。

执行前统一要求：

- `NETLOAD` 必须加载 `publish/SectionGenerator/MetroToolKits.Bootstrap.dll`，不要直接加载 `publish/SectionGenerator/MetroToolKits.SectionGenerator.Plugin.dll`。
- 验收宿主为真实 `acad.exe`。
- 每次执行后都应检查 `publish/SectionGenerator/logs/MetroToolKits.log` 与 `publish/SectionGenerator/logs/MetroToolKits_User.log`。
- 对于尚未手动执行的项，状态统一标记为 `Pending manual host validation`。

### Controlled Failure

#### No Generated Sections In Current DWG

前置条件：

- 已在 AutoCAD 中 `NETLOAD` `publish/SectionGenerator/MetroToolKits.Bootstrap.dll`。
- 当前 DWG 可正常打开。
- 当前 DWG 中没有由 `GenSection` 生成过的剖面结果。
- 如有 `FloorConfig`，可保留；本场景重点验证“无已生成剖面”时的行为。

操作步骤：

1. 打开无已生成剖面的测试 DWG。
2. 执行 `CheckSectionUpdates`。
3. 记录命令行提示、对话框提示、warning、diagnostics 与日志输出。

预期结果：

- `CheckSectionUpdates` 给出明确提示、diagnostics、warning 或业务失败信息，指出当前 DWG 中没有可检查更新的已生成剖面。
- AutoCAD 宿主不崩溃。
- 插件不导致 fatal error。
- 不出现未处理异常弹窗。

通过标准：

- 命令结果表现为可理解的业务失败或 warning，而不是宿主异常。
- 用户可从输出或日志中识别“无已生成剖面”这一失败原因。
- 命令结束后宿主仍可继续执行其他命令。

日志检查点：

- 存在与 `CheckSectionUpdates` 相关的命令开始、剖面检索或失败记录。
- 日志中能看到“没有已生成剖面”或等价业务诊断信息。
- 日志中不应出现 fatal error、未处理异常、Bootstrap 初始化失败或插件发现失败。

当前状态：

- `Pending manual host validation`

#### No FloorConfig In Current DWG

前置条件：

- 已在 AutoCAD 中 `NETLOAD` `publish/SectionGenerator/MetroToolKits.Bootstrap.dll`。
- 当前 DWG 中不存在可回读的 `FloorConfig`。
- 当前 DWG 可包含或不包含剖面结果，但本场景重点验证“缺少 FloorConfig”时的行为。

操作步骤：

1. 打开无 `FloorConfig` 的测试 DWG。
2. 执行 `CheckSectionUpdates`。
3. 记录命令行提示、对话框提示、warning、diagnostics 与日志输出。

预期结果：

- `CheckSectionUpdates` 给出明确提示、diagnostics、warning 或业务失败信息，指出缺少 `FloorConfig` 或无法解析当前图纸配置。
- AutoCAD 宿主不崩溃。
- 插件不导致 fatal error。
- 不出现未处理异常弹窗。

通过标准：

- 命令以可诊断的业务失败结束，而不是因空引用、状态异常或宿主错误中断。
- 用户可从提示或日志中理解需要先补齐或恢复 `FloorConfig`。
- 命令结束后宿主仍稳定可用。

日志检查点：

- 存在 `FloorConfig` 读取、校验或失败记录。
- 日志中能看到“缺少 FloorConfig”或等价业务诊断信息。
- 日志中不应出现 fatal error、未处理异常或依赖缺失错误。

当前状态：

- `Pending manual host validation`

#### Missing Section Metadata Snapshot Or XData

前置条件：

- 已在 AutoCAD 中 `NETLOAD` `publish/SectionGenerator/MetroToolKits.Bootstrap.dll`。
- 当前 DWG 中存在剖面结果。
- 当前 DWG 中不存在可用的剖面元数据、snapshot 或 XData，或这些关联数据不可被当前实现读取。

操作步骤：

1. 打开 metadata、snapshot 或 XData 缺失的测试 DWG。
2. 执行 `CheckSectionUpdates`。
3. 记录命令行提示、对话框提示、warning、diagnostics 与日志输出。

预期结果：

- `CheckSectionUpdates` 给出明确提示、diagnostics、warning 或业务失败信息，指出缺少可用于更新检查的 metadata、snapshot 或 XData。
- AutoCAD 宿主不崩溃。
- 插件不导致 fatal error。
- 不出现未处理异常弹窗。

通过标准：

- 命令结果为可解释的业务失败或 warning，而不是因为关联数据缺失导致未处理异常。
- 用户可从输出或日志中判断当前剖面缺少更新检测所依赖的数据。
- 命令结束后宿主仍可继续使用。

日志检查点：

- 存在 metadata、snapshot、XData 读取或校验记录。
- 日志中能看到“metadata 缺失”“snapshot 缺失”“XData 缺失”或等价业务诊断信息。
- 日志中不应出现 fatal error、未处理异常或反射命令注册错误。

当前状态：

- `Pending manual host validation`

#### Source Elements Deleted Or Unrecognizable

前置条件：

- 已在 AutoCAD 中 `NETLOAD` `publish/SectionGenerator/MetroToolKits.Bootstrap.dll`。
- 当前 DWG 中存在已生成剖面以及最小关联信息。
- 参与生成的原始构件已被删除，或当前实现已无法识别这些源构件。

操作步骤：

1. 打开源构件已删除或不可识别的测试 DWG。
2. 执行 `CheckSectionUpdates`。
3. 记录命令行提示、对话框提示、warning、diagnostics 与日志输出。

预期结果：

- `CheckSectionUpdates` 给出明确提示、diagnostics、warning 或业务失败信息，指出源构件缺失、已删除或无法识别。
- AutoCAD 宿主不崩溃。
- 插件不导致 fatal error。
- 不出现未处理异常弹窗。

通过标准：

- 命令能够把问题归因为源构件状态异常，而不是宿主级异常。
- 用户可从输出或日志判断剖面已无法和原始构件建立稳定更新检测关系。
- 失败后宿主仍保持稳定。

日志检查点：

- 存在源构件查找、识别、匹配或失败记录。
- 日志中能看到“源构件已删除”“源构件无法识别”或等价业务诊断信息。
- 日志中不应出现 fatal error、未处理异常或宿主崩溃前兆。

当前状态：

- `Pending manual host validation`

#### Section Exists But Missing Source Association

前置条件：

- 已在 AutoCAD 中 `NETLOAD` `publish/SectionGenerator/MetroToolKits.Bootstrap.dll`。
- 当前 DWG 中存在剖面结果。
- 剖面结果本身存在，但与源构件的关联信息缺失、不完整或不可解析。

操作步骤：

1. 打开剖面存在但源构件关联信息缺失的测试 DWG。
2. 执行 `CheckSectionUpdates`。
3. 记录命令行提示、对话框提示、warning、diagnostics 与日志输出。

预期结果：

- `CheckSectionUpdates` 给出明确提示、diagnostics、warning 或业务失败信息，指出剖面与源构件的关联信息缺失或无法解析。
- AutoCAD 宿主不崩溃。
- 插件不导致 fatal error。
- 不出现未处理异常弹窗。

通过标准：

- 命令结果能被识别为受控失败或业务 warning。
- 用户可区分“有剖面但缺少关联信息”和“完全没有剖面”这两类场景。
- 命令结束后宿主仍可继续执行其他操作。

日志检查点：

- 存在剖面关联读取、匹配或失败记录。
- 日志中能看到“关联信息缺失”或等价业务诊断信息。
- 日志中不应出现 fatal error、未处理异常或依赖缺失错误。

当前状态：

- `Pending manual host validation`

#### Unsaved Drawing Or Invalid Path State

前置条件：

- 已在 AutoCAD 中 `NETLOAD` `publish/SectionGenerator/MetroToolKits.Bootstrap.dll`。
- 当前图纸未保存，或当前图纸路径状态异常，导致命令无法稳定定位配置、关联文件或图纸上下文。
- 可同时保留最小剖面与配置条件，以便把失败原因聚焦到路径状态异常。

操作步骤：

1. 打开未保存或路径状态异常的测试图纸。
2. 执行 `CheckSectionUpdates`。
3. 记录命令行提示、对话框提示、warning、diagnostics 与日志输出。

预期结果：

- `CheckSectionUpdates` 给出明确提示、diagnostics、warning 或业务失败信息，指出图纸未保存、路径异常或无法稳定定位配置上下文。
- AutoCAD 宿主不崩溃。
- 插件不导致 fatal error。
- 不出现未处理异常弹窗。

通过标准：

- 命令能以业务失败或 warning 结束，而不是因路径解析问题触发未处理异常。
- 用户可从输出或日志理解需要先保存图纸或修复路径状态。
- 命令结束后宿主仍保持稳定。

日志检查点：

- 存在图纸路径、配置定位、上下文解析或失败记录。
- 日志中能看到“图纸未保存”“路径异常”或等价业务诊断信息。
- 日志中不应出现 fatal error、未处理异常或 Bootstrap 初始化失败。

当前状态：

- `Pending manual host validation`

### Minimal Success / Partial Success

最小测试 DWG 的要求：

- 使用真实 `acad.exe` 可正常打开的最小 DWG。
- DWG 中已保存至少一份可读取的 `FloorConfig`。
- DWG 中已存在至少一个由 `GenSection` 生成过的剖面。
- 剖面结果中存在可用于更新检测的 metadata、snapshot 或 XData。
- 至少有一个源构件仍可识别。
- 可选准备一份变体 DWG，在其中修改一个简单源构件，用于验证“存在变化”路径。

FloorConfig 的最低要求：

- 当前 DWG 中存在一份可回读的 `FloorConfig`。
- `FloorConfig` 至少覆盖当前剖面所属楼层或更新检查所需的最小上下文。
- 如 `CheckSectionUpdates` 依赖图纸级配置定位，则应保证配置能在当前 DWG 中被稳定解析。

已生成剖面的最低要求：

- 当前 DWG 中至少存在一个已生成剖面结果。
- 该剖面应由当前流程识别为可参与更新检查的对象。
- 不要求准备复杂剖面集合，但至少要能支撑一次“无变化”或“有变化”的最小检查。

metadata、snapshot 或 XData 的最低要求：

- 剖面结果中至少有一类关联数据可被当前实现读取。
- 该关联数据应足以把剖面结果与源构件或历史生成状态建立最小联系。
- 如当前实现依赖特定字段或结构，则测试 DWG 应保证这些最小字段存在且未损坏。

操作步骤：

1. 执行 `.\build.ps1 publish`，确认发布目录为 `publish/SectionGenerator`。
2. 打开 `acad.exe` 并加载最小测试 DWG。
3. 通过 `NETLOAD` 加载 `publish/SectionGenerator/MetroToolKits.Bootstrap.dll`，不要直接加载插件 DLL。
4. 先确认 `FloorConfig`、已生成剖面、metadata 或 XData、以及至少一个可识别源构件已准备完成。
5. 执行 `CheckSectionUpdates`。
6. 记录命令行输出、界面提示、检查结果、warning、diagnostics 与日志。
7. 可选：修改一个简单源构件后再次执行 `CheckSectionUpdates`，观察是否提示存在变化、过期、需要更新或等价业务结果。
8. 如命令未形成完整检查结果，但产生业务 warning，也记录该结果，不将其误记为 `Passed`。

预期结果：

- `CheckSectionUpdates` 能执行完成；或者
- 在缺少足够数据时产生业务 warning；但
- AutoCAD 宿主不能崩溃。
- 不能出现 fatal error。
- 不能出现未处理异常。
- 如果未发生变化，应能提示无更新或无差异。
- 如果发生变化，应能提示存在变化、过期、需要更新或类似业务结果。

通过标准：

- 无变化路径下，命令执行结束后能给出“无更新”“无差异”或等价结果，且宿主保持稳定。
- 有变化路径下，命令执行结束后能给出“存在变化”“过期”“需要更新”或等价结果，且宿主保持稳定。
- 部分成功路径下，即使缺少足够数据，也至少存在业务 warning、diagnostics 或可读日志来解释执行结果。
- 无论是无变化、有变化还是部分成功，均不出现 fatal error 或未处理异常。

日志检查点：

- 存在与 `CheckSectionUpdates` 相关的命令开始、剖面检索、关联数据读取、源构件比对、检查完成或 warning 记录。
- 无变化路径下，应能在日志中看到“无更新”“无差异”或等价业务结果。
- 有变化路径下，应能在日志中看到“存在变化”“需要更新”“过期”或等价业务结果。
- 日志中不应出现 fatal error、未处理异常、Bootstrap 初始化失败或插件发现失败。

当前状态：

- `Pending manual host validation`

### Next actions

- 准备无剖面 DWG。
- 准备有 `FloorConfig` 但无剖面 DWG。
- 准备有已生成剖面但无变化 DWG。
- 准备有已生成剖面且源构件发生变化 DWG。
- 准备 metadata 缺失或损坏 DWG。

## P4.2f - CheckSectionUpdates Manual Host Validation Record

本节用于后续真实 AutoCAD 手工宿主验收时填写记录；当前不代表已执行验收。

### Execution Context

- Date: `TBD`
- Validator: `TBD`
- Git commit: `TBD`
- Build command: `TBD`
- Publish command: `TBD`
- AutoCAD host: `TBD`
- Loaded DLL: `publish/SectionGenerator/MetroToolKits.Bootstrap.dll`
- Test DWG: `TBD`
- Log files checked: `TBD`
- Notes: `TBD`
- Current status: `Pending manual host validation`

说明：

- 应通过 `NETLOAD` 加载 `publish/SectionGenerator/MetroToolKits.Bootstrap.dll`。
- 不应直接加载 `publish/SectionGenerator/MetroToolKits.SectionGenerator.Plugin.dll`。

### Controlled Failure Record

| Scenario | Test DWG | Expected Result | Actual Result | Log Check | Status | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| No Generated Sections In Current DWG | `TBD` | 不崩溃，给出业务 warning 或 diagnostics | `TBD` | `TBD` | `Pending manual host validation` | `TBD` |
| No FloorConfig In Current DWG | `TBD` | 不崩溃，给出业务 warning 或 diagnostics | `TBD` | `TBD` | `Pending manual host validation` | `TBD` |
| Missing Section Metadata Snapshot Or XData | `TBD` | 不崩溃，给出业务 warning 或 diagnostics | `TBD` | `TBD` | `Pending manual host validation` | `TBD` |
| Source Elements Deleted Or Unrecognizable | `TBD` | 不崩溃，给出业务 warning 或 diagnostics | `TBD` | `TBD` | `Pending manual host validation` | `TBD` |
| Section Exists But Missing Source Association | `TBD` | 不崩溃，给出业务 warning 或 diagnostics | `TBD` | `TBD` | `Pending manual host validation` | `TBD` |
| Unsaved Drawing Or Invalid Path State | `TBD` | 不崩溃，给出业务 warning 或 diagnostics | `TBD` | `TBD` | `Pending manual host validation` | `TBD` |

### Minimal Success / Partial Success Record

| Scenario | Test DWG | Expected Result | Actual Result | Update Check Result | Log Check | Status | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Minimal DWG with FloorConfig + generated section + metadata | `TBD` | 命令可执行完成，且能给出可诊断的检查结果，宿主不崩溃 | `TBD` | `TBD` | `TBD` | `Pending manual host validation` | `TBD` |
| Minimal DWG with generated section but no source changes | `TBD` | 明确提示无更新或无差异，宿主不崩溃 | `TBD` | `TBD` | `TBD` | `Pending manual host validation` | `TBD` |
| Minimal DWG with generated section and modified source element | `TBD` | 明确提示存在变化、过期或需要更新，宿主不崩溃 | `TBD` | `TBD` | `TBD` | `Pending manual host validation` | `TBD` |
| Minimal DWG with generated section but insufficient metadata | `TBD` | 给出业务 warning 或 diagnostics，宿主不崩溃 | `TBD` | `TBD` | `TBD` | `Pending manual host validation` | `TBD` |

### Manual Validation Checklist

- [ ] Run `.\build.ps1 publish`
- [ ] Start `acad.exe`
- [ ] Load `publish/SectionGenerator/MetroToolKits.Bootstrap.dll` with `NETLOAD`
- [ ] Do not directly load plugin DLL
- [ ] Open controlled failure DWG
- [ ] Execute `CheckSectionUpdates`
- [ ] Record command line output
- [ ] Record UI diagnostics or warning
- [ ] Check `publish/SectionGenerator/logs/MetroToolKits.log`
- [ ] Check `publish/SectionGenerator/logs/MetroToolKits_User.log`
- [ ] Confirm AutoCAD host did not crash
- [ ] Confirm no fatal error
- [ ] Confirm no unhandled exception dialog
- [ ] Record whether result is no changes, changes detected, warning, or failure
- [ ] Record final status

### Result Classification

- `Passed`: 只有真实手动验收完成，且满足通过标准时才能使用。
- `Controlled Failure`: 命令按业务失败或 warning 结束，宿主不崩溃。
- `No Changes`: 命令完成检查，并明确提示没有更新或没有差异。
- `Changes Detected`: 命令完成检查，并明确提示存在变化、过期或需要更新。
- `Partial Success`: 命令执行到部分流程，产生 warning 或部分结果，宿主不崩溃。
- `Failed`: 出现宿主崩溃、fatal error、未处理异常，或业务结果不可诊断。
- `Pending manual host validation`: 尚未实际执行手工宿主验收。

## P4.2g - UpdateSection Host Validation Plan

本节只新增 `UpdateSection` 的宿主验收计划，不代表当前已完成手动验证。

执行前统一要求：

- `NETLOAD` 必须加载 `publish/SectionGenerator/MetroToolKits.Bootstrap.dll`，不要直接加载 `publish/SectionGenerator/MetroToolKits.SectionGenerator.Plugin.dll`。
- 验收宿主为真实 `acad.exe`。
- 每次执行后都应检查 `publish/SectionGenerator/logs/MetroToolKits.log` 与 `publish/SectionGenerator/logs/MetroToolKits_User.log`。
- 对于尚未手动执行的项，状态统一标记为 `Pending manual host validation`。

### Controlled Failure

#### No Generated Sections In Current DWG

前置条件：

- 已在 AutoCAD 中 `NETLOAD` `publish/SectionGenerator/MetroToolKits.Bootstrap.dll`。
- 当前 DWG 可正常打开。
- 当前 DWG 中没有由 `GenSection` 生成过的剖面结果。

操作步骤：

1. 打开无已生成剖面的测试 DWG。
2. 执行 `UpdateSection`。
3. 记录命令行提示、对话框提示、warning、diagnostics 与日志输出。

预期结果：

- `UpdateSection` 给出明确提示、diagnostics、warning 或业务失败信息，指出当前 DWG 中没有可更新的已生成剖面。
- AutoCAD 宿主不崩溃。
- 插件不导致 fatal error。
- 不出现未处理异常弹窗。
- 不应产生半更新但无提示的错误状态。

通过标准：

- 命令结果表现为可理解的业务失败或 warning，而不是宿主异常。
- 用户可从输出或日志中识别“无已生成剖面”这一失败原因。
- 图纸内容不应被静默修改。

日志检查点：

- 存在与 `UpdateSection` 相关的命令开始、剖面检索或失败记录。
- 日志中能看到“没有可更新剖面”或等价业务诊断信息。
- 日志中不应出现 fatal error、未处理异常、Bootstrap 初始化失败或插件发现失败。

当前状态：

- `Pending manual host validation`

#### No Updatable Section Selected

前置条件：

- 已在 AutoCAD 中 `NETLOAD` `publish/SectionGenerator/MetroToolKits.Bootstrap.dll`。
- 当前 DWG 中可以存在剖面结果，但用户未选择可更新的剖面，或选择了不受支持的对象。

操作步骤：

1. 打开包含已生成剖面或普通对象的测试 DWG。
2. 执行 `UpdateSection`。
3. 在选择阶段不选择任何可更新剖面，或故意选择非目标对象。
4. 记录命令行提示、对话框提示、warning、diagnostics 与日志输出。

预期结果：

- `UpdateSection` 给出明确提示、diagnostics、warning 或业务失败信息，指出没有选择可更新剖面或当前选择无效。
- AutoCAD 宿主不崩溃。
- 插件不导致 fatal error。
- 不出现未处理异常弹窗。
- 不应产生半更新但无提示的错误状态。

通过标准：

- 命令能把问题明确归因到“未选择有效剖面结果”，而不是进入不确定状态。
- 用户可从输出或日志理解需要重新选择正确对象。
- 图纸内容不应被静默修改。

日志检查点：

- 存在选择校验、对象类型判断或失败记录。
- 日志中能看到“未选择可更新剖面”“选择无效”或等价业务诊断信息。
- 日志中不应出现 fatal error、未处理异常或反射命令注册错误。

当前状态：

- `Pending manual host validation`

#### No FloorConfig In Current DWG

前置条件：

- 已在 AutoCAD 中 `NETLOAD` `publish/SectionGenerator/MetroToolKits.Bootstrap.dll`。
- 当前 DWG 中不存在可回读的 `FloorConfig`。
- 当前 DWG 可包含剖面结果，但本场景重点验证“缺少 FloorConfig”时的行为。

操作步骤：

1. 打开无 `FloorConfig` 的测试 DWG。
2. 执行 `UpdateSection`。
3. 记录命令行提示、对话框提示、warning、diagnostics 与日志输出。

预期结果：

- `UpdateSection` 给出明确提示、diagnostics、warning 或业务失败信息，指出缺少 `FloorConfig` 或无法解析当前图纸配置。
- AutoCAD 宿主不崩溃。
- 插件不导致 fatal error。
- 不出现未处理异常弹窗。
- 不应产生半更新但无提示的错误状态。

通过标准：

- 命令以可诊断的业务失败结束，而不是因空引用、状态异常或宿主错误中断。
- 用户可从提示或日志中理解需要先补齐或恢复 `FloorConfig`。
- 图纸内容不应被静默修改。

日志检查点：

- 存在 `FloorConfig` 读取、校验或失败记录。
- 日志中能看到“缺少 FloorConfig”或等价业务诊断信息。
- 日志中不应出现 fatal error、未处理异常或依赖缺失错误。

当前状态：

- `Pending manual host validation`

#### Missing Metadata Snapshot Or XData

前置条件：

- 已在 AutoCAD 中 `NETLOAD` `publish/SectionGenerator/MetroToolKits.Bootstrap.dll`。
- 当前 DWG 中存在剖面结果。
- 当前剖面结果缺少可用于更新的 metadata、snapshot 或 XData，或这些关联数据不可被当前实现读取。

操作步骤：

1. 打开 metadata、snapshot 或 XData 缺失的测试 DWG。
2. 执行 `UpdateSection`。
3. 记录命令行提示、对话框提示、warning、diagnostics 与日志输出。

预期结果：

- `UpdateSection` 给出明确提示、diagnostics、warning 或业务失败信息，指出缺少可用于更新的 metadata、snapshot 或 XData。
- AutoCAD 宿主不崩溃。
- 插件不导致 fatal error。
- 不出现未处理异常弹窗。
- 不应产生半更新但无提示的错误状态。

通过标准：

- 命令结果为可解释的业务失败或 warning，而不是因为关联数据缺失导致未处理异常。
- 用户可从输出或日志中判断当前剖面缺少更新所依赖的数据。
- 图纸内容不应被静默修改。

日志检查点：

- 存在 metadata、snapshot、XData 读取或校验记录。
- 日志中能看到“metadata 缺失”“snapshot 缺失”“XData 缺失”或等价业务诊断信息。
- 日志中不应出现 fatal error、未处理异常或反射命令注册错误。

当前状态：

- `Pending manual host validation`

#### Source Elements Deleted Or Unrecognizable

前置条件：

- 已在 AutoCAD 中 `NETLOAD` `publish/SectionGenerator/MetroToolKits.Bootstrap.dll`。
- 当前 DWG 中存在已生成剖面以及最小关联信息。
- 参与生成的原始构件已被删除，或当前实现已无法识别这些源构件。

操作步骤：

1. 打开源构件已删除或不可识别的测试 DWG。
2. 执行 `UpdateSection`。
3. 记录命令行提示、对话框提示、warning、diagnostics 与日志输出。

预期结果：

- `UpdateSection` 给出明确提示、diagnostics、warning 或业务失败信息，指出源构件缺失、已删除或无法识别。
- AutoCAD 宿主不崩溃。
- 插件不导致 fatal error。
- 不出现未处理异常弹窗。
- 不应产生半更新但无提示的错误状态。

通过标准：

- 命令能够把问题归因为源构件状态异常，而不是宿主级异常。
- 用户可从输出或日志判断更新无法建立稳定的源构件关系。
- 图纸内容不应被静默修改。

日志检查点：

- 存在源构件查找、识别、匹配或失败记录。
- 日志中能看到“源构件已删除”“源构件无法识别”或等价业务诊断信息。
- 日志中不应出现 fatal error、未处理异常或宿主崩溃前兆。

当前状态：

- `Pending manual host validation`

#### Missing Source Floor Configuration

前置条件：

- 已在 AutoCAD 中 `NETLOAD` `publish/SectionGenerator/MetroToolKits.Bootstrap.dll`。
- 当前 DWG 中存在剖面结果与部分更新关联数据。
- 剖面对应的源楼层配置缺失，或当前 `FloorConfig` 中无法解析出该剖面所需的源楼层上下文。

操作步骤：

1. 打开源楼层配置缺失的测试 DWG。
2. 执行 `UpdateSection`。
3. 记录命令行提示、对话框提示、warning、diagnostics 与日志输出。

预期结果：

- `UpdateSection` 给出明确提示、diagnostics、warning 或业务失败信息，指出剖面对应的源楼层配置缺失或无法解析。
- AutoCAD 宿主不崩溃。
- 插件不导致 fatal error。
- 不出现未处理异常弹窗。
- 不应产生半更新但无提示的错误状态。

通过标准：

- 命令把问题归因为楼层配置缺失，而不是以模糊失败结束。
- 用户可从输出或日志中理解需要恢复相关楼层配置。
- 图纸内容不应被静默修改。

日志检查点：

- 存在楼层配置读取、楼层匹配或失败记录。
- 日志中能看到“源楼层配置缺失”或等价业务诊断信息。
- 日志中不应出现 fatal error、未处理异常或依赖缺失错误。

当前状态：

- `Pending manual host validation`

#### Damaged Section Or Partially Deleted Entities

前置条件：

- 已在 AutoCAD 中 `NETLOAD` `publish/SectionGenerator/MetroToolKits.Bootstrap.dll`。
- 当前 DWG 中存在已生成剖面。
- 剖面本身已经损坏，或部分实体被手动删除，导致更新目标集合不完整。

操作步骤：

1. 打开已生成剖面部分实体被手动删除的测试 DWG。
2. 执行 `UpdateSection`。
3. 记录命令行提示、对话框提示、warning、diagnostics 与日志输出。

预期结果：

- `UpdateSection` 给出明确提示、diagnostics、warning 或业务失败信息，指出剖面已损坏、部分实体缺失或无法完整更新。
- AutoCAD 宿主不崩溃。
- 插件不导致 fatal error。
- 不出现未处理异常弹窗。
- 不应产生半更新但无提示的错误状态。

通过标准：

- 命令能识别剖面损坏或实体缺失，而不是静默跳过或留下不明确状态。
- 用户可从输出或日志判断需要重新生成、修复或清理损坏剖面。
- 如发生部分写入，应同时有明确提示，不可出现静默半更新。

日志检查点：

- 存在剖面实体检索、对象恢复、更新目标校验或失败记录。
- 日志中能看到“剖面损坏”“实体缺失”“部分实体被删除”或等价业务诊断信息。
- 日志中不应出现 fatal error、未处理异常或宿主崩溃前兆。

当前状态：

- `Pending manual host validation`

#### Missing Target Layer Block Style Or Output Configuration

前置条件：

- 已在 AutoCAD 中 `NETLOAD` `publish/SectionGenerator/MetroToolKits.Bootstrap.dll`。
- 当前 DWG 中存在可更新剖面以及最小更新关联数据。
- 更新过程中所需的目标图层、块、样式或输出配置缺失，或当前实现无法稳定解析这些输出条件。

操作步骤：

1. 打开输出配置缺失的测试 DWG。
2. 执行 `UpdateSection`。
3. 记录命令行提示、对话框提示、warning、diagnostics 与日志输出。

预期结果：

- `UpdateSection` 给出明确提示、diagnostics、warning 或业务失败信息，指出目标图层、块、样式或输出配置缺失。
- AutoCAD 宿主不崩溃。
- 插件不导致 fatal error。
- 不出现未处理异常弹窗。
- 不应产生半更新但无提示的错误状态。

通过标准：

- 命令把问题归因为输出配置缺失，而不是留下不确定更新结果。
- 用户可从输出或日志理解需要补齐目标图层、块、样式或配置。
- 图纸内容不应被静默修改，或如有部分写入必须有明确提示。

日志检查点：

- 存在图层、块、样式、输出配置读取或失败记录。
- 日志中能看到“目标图层缺失”“块缺失”“样式缺失”“输出配置缺失”或等价业务诊断信息。
- 日志中不应出现 fatal error、未处理异常或 Bootstrap 初始化失败。

当前状态：

- `Pending manual host validation`

### Minimal Success / Partial Success

最小测试 DWG 的要求：

- 使用真实 `acad.exe` 可正常打开的最小 DWG。
- DWG 中已保存至少一份可读取的 `FloorConfig`。
- DWG 中已存在至少一个由 `GenSection` 生成过的剖面。
- 剖面结果中存在可用于更新的 metadata、snapshot 或 XData。
- 至少有一个源构件仍可识别。
- 可选准备一份变体 DWG，先修改一个简单源构件，再执行 `CheckSectionUpdates` 和 `UpdateSection` 验证有变化路径。

FloorConfig 的最低要求：

- 当前 DWG 中存在一份可回读的 `FloorConfig`。
- `FloorConfig` 至少覆盖当前剖面所属楼层或更新所需的最小上下文。
- 如 `UpdateSection` 依赖图纸级配置定位，则应保证配置能在当前 DWG 中被稳定解析。

已生成剖面的最低要求：

- 当前 DWG 中至少存在一个已生成剖面结果。
- 该剖面应由当前流程识别为可参与更新的对象。
- 不要求准备复杂剖面集合，但至少要能支撑一次最小更新尝试。

metadata、snapshot 或 XData 的最低要求：

- 剖面结果中至少有一类关联数据可被当前实现读取。
- 该关联数据应足以把剖面结果与源构件或历史生成状态建立最小联系。
- 如当前实现依赖特定字段或结构，则测试 DWG 应保证这些最小字段存在且未损坏。

源构件变化的最低要求：

- 至少准备一个简单源构件，且当前实现仍可识别。
- 如需验证更新路径，该构件应发生可观察的最小变化，例如几何、位置、可识别属性或可导致剖面结果变化的最小调整。
- 如当前只能验证“无变化”路径，也应保留该源构件，以确认命令不会静默失败。

操作步骤：

1. 执行 `.\build.ps1 publish`，确认发布目录为 `publish/SectionGenerator`。
2. 打开 `acad.exe` 并加载最小测试 DWG。
3. 通过 `NETLOAD` 加载 `publish/SectionGenerator/MetroToolKits.Bootstrap.dll`，不要直接加载插件 DLL。
4. 先确认 `FloorConfig`、已生成剖面、metadata 或 XData、以及至少一个可识别源构件已准备完成。
5. 可选：先修改一个简单源构件，再执行 `CheckSectionUpdates`，记录是否提示存在变化、过期或需要更新。
6. 执行 `UpdateSection`。
7. 记录命令行输出、界面提示、更新结果、warning、diagnostics 与日志。
8. 如命令未形成完整更新结果，但产生业务 warning，也记录该结果，不将其误记为 `Passed`。

预期结果：

- `UpdateSection` 能执行完成；或者
- 在构件不足、metadata 不完整、无法完成全部更新时产生业务 warning；但
- AutoCAD 宿主不能崩溃。
- 不能出现 fatal error。
- 不能出现未处理异常。
- 如果可以更新，应更新已有剖面或重新生成对应结果。
- 如果不能更新，应给出明确业务原因。
- 不能出现静默失败。

通过标准：

- 可更新路径下，命令执行结束后可观察到已有剖面被更新，或对应结果被重新生成，且宿主保持稳定。
- 无变化路径下，如不存在实际差异，也应给出明确结果或提示，而不是静默结束。
- 部分成功路径下，即使无法完成全部更新，也至少存在业务 warning、diagnostics 或可读日志来解释执行结果。
- 无论是完整更新、无变化还是部分成功，均不出现 fatal error、未处理异常或静默失败。

日志检查点：

- 存在与 `UpdateSection` 相关的命令开始、剖面检索、关联数据读取、源构件比对、更新写入、更新完成或 warning 记录。
- 可更新路径下，应能在日志中看到更新执行、重建结果、写入完成或等价业务结果。
- 无法完整更新时，应能在日志中看到构件不足、metadata 不完整、输出条件缺失或等价业务 warning。
- 日志中不应出现 fatal error、未处理异常、Bootstrap 初始化失败或插件发现失败。

当前状态：

- `Pending manual host validation`

### Next actions

- 准备无剖面 DWG。
- 准备有 `FloorConfig` 但无剖面 DWG。
- 准备有已生成剖面但无变化 DWG。
- 准备有已生成剖面且源构件发生变化 DWG。
- 准备已生成剖面 metadata 缺失 DWG。
- 准备已生成剖面部分实体被手动删除 DWG。

## accoreconsole.exe Follow-up Plan

P4.1 先不引入自动化脚本执行，只明确后续验收计划：

1. 如 `build.ps1` 当前生成 `publish/HostAutomationBootstrap`，可将其作为 `accoreconsole.exe` 宿主自动化入口目录的后续参考。
2. 准备最小 DWG 样本：
   - 空白图
   - 含已保存 FloorConfig 的图
   - 含已生成剖面的图
3. 编写 `.scr` 脚本，驱动：
   - `NETLOAD`
   - `MKSectionSelfTestInternal`
   - `MKSectionHostAcceptanceInternal`
4. 采集控制台输出和日志文件，形成可重复执行的 Host Smoke 结果。
5. 后续再决定是否把 `accoreconsole.exe` smoke check 纳入 CI 或发布前检查脚本。

当前仓库内可作为后续参考的发布目录：

- 如 `build.ps1` 当前生成 `publish/HostAutomationBootstrap`，则可作为后续参考。
- 如 `build.ps1` 当前生成 `publish/HostSmoke`，则可作为后续参考。

## Current P4.1 Conclusion

当前构建与发布输出关系可先固定为：

- 构建入口：`build.ps1`
- 宿主加载入口：`MetroToolKits.Bootstrap.dll`
- 插件业务实现：`MetroToolKits.SectionGenerator.Plugin.dll`
- 推荐发布目录：`publish/SectionGenerator`

P4.2 可在此基础上进入真实 `acad.exe` 手动验收，P4.3 再收口到 `accoreconsole.exe` 自动化 smoke plan。
