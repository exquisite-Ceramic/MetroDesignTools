# Contracts README

`docs/contracts` 用来说明 SectionGenerator 的契约层边界，以及示例 payload / schema / C# DTO 三者之间的对应关系。

## P3 Contracts Stabilization Scope

P3 的目标是稳定契约边界，而不是扩展业务能力。

P3 只允许修改以下内容：

- contract examples
- JSON schemas
- contract documentation
- DTO serialization round-trip tests

P3 明确不做以下内容：

- LLM integration
- DSL interpreter
- Core algorithm changes
- DWG envelope changes
- FloorConfig save flow changes
- UI behavior changes
- automatic repair
- business behavior changes

## 这层解决什么问题

Contracts 层的目标不是承载算法，也不是承载 AutoCAD/WPF 细节，而是提供稳定、可序列化、可测试的输入输出边界。

当前这层主要服务以下场景：

- `Plugin/UI -> App` 的请求 DTO
- `App -> Plugin/UI` 的展示 DTO
- 后续脚本、批处理、外部宿主、AI/DSL 的结构化接口
- 文档样例、回归测试、契约评审时的共同语言

这层应尽量避免直接依赖：

- `System.Windows`
- `Autodesk.AutoCAD`
- 领域算法内部对象
- WPF 控件状态

## 当前代码位置

Contracts 代码位于：

- [MetroToolKits.SectionGenerator.Contracts.csproj](E:/CAD二次开发/MetroDesignToolKits/src/Plugins/SectionGenerator/MetroToolKits.SectionGenerator.Contracts/MetroToolKits.SectionGenerator.Contracts.csproj)

主要子目录：

- `Common`
  通用几何/基础 DTO，例如点坐标。
- `Floors`
  楼层配置读写相关契约。
- `Output`
  输出图层、填充、标注等契约。
- `LayerMapping`
  图层映射工作台相关契约。
- `Preflight`
  生成前检查报告相关契约。
- `Templates`
  模板目录和选项 DTO。
- `Workbench`
  工具箱/工作台快照 DTO。
- `OperationTrace`
  操作追踪和事件记录 DTO。

## 当前已落地的核心契约

### 1. 楼层配置保存请求

顶层保存请求：

- [SaveFloorConfigDocumentRequestDto.cs](E:/CAD二次开发/MetroDesignToolKits/src/Plugins/SectionGenerator/MetroToolKits.SectionGenerator.Contracts/Floors/SaveFloorConfigDocumentRequestDto.cs)

它把保存拆成两部分：

- `FloorConfig`
- `OutputConfig`

这对应当前 P0 保存链路：

`UI -> SaveFloorConfigDocumentRequestDto -> IFloorConfigUseCase.Save(request) -> Validator -> Repository`

相关示例：

- [save-floor-config-document.request.example.json](E:/CAD二次开发/MetroDesignToolKits/docs/contracts/examples/save-floor-config-document.request.example.json)

### 2. 输出配置

输出配置 DTO：

- [SectionOutputConfigDto.cs](E:/CAD二次开发/MetroDesignToolKits/src/Plugins/SectionGenerator/MetroToolKits.SectionGenerator.Contracts/Output/SectionOutputConfigDto.cs)

相关示例：

- [section-output-config.example.json](E:/CAD二次开发/MetroDesignToolKits/docs/contracts/examples/section-output-config.example.json)

### 3. 生成前检查报告

Preflight 报告 DTO：

- [SectionPreflightReportDto.cs](E:/CAD二次开发/MetroDesignToolKits/src/Plugins/SectionGenerator/MetroToolKits.SectionGenerator.Contracts/Preflight/SectionPreflightReportDto.cs)

当前报告已覆盖：

- 当前图纸状态
- 配置来源
- 楼层数量
- 基准层相关 blocking/warning/info
- 非基准层跳过信息
- 模板缺失
- 输出配置明显错误
- 最终 `CanGenerate / Blocking / Warning / Info`

相关示例：

- [section-preflight-report.response.example.json](E:/CAD二次开发/MetroDesignToolKits/docs/contracts/examples/section-preflight-report.response.example.json)

### 4. 图层映射工作台

图层映射工作台 DTO：

- [LayerMappingWorkspaceDto.cs](E:/CAD二次开发/MetroDesignToolKits/src/Plugins/SectionGenerator/MetroToolKits.SectionGenerator.Contracts/LayerMapping/LayerMappingWorkspaceDto.cs)

这类 DTO 主要用于：

- UI 展示可映射图层
- 返回可选构件类型/模板
- 传递当前已建立的映射关系

## docs/contracts 目录约定

当前目录建议按下面的职责使用：

- `examples/`
  给人看、给联调看、给回归测试看。
- `schemas/`
  放 JSON Schema，作为“机器可校验”的契约定义。
- `README.md`
  放边界说明、目录索引、命名约定、当前状态。

## 当前现状

当前 `examples/` 已经有示例文件，但 `schemas/` 目录还没有完整落地的 schema 文件。

这意味着：

- 代码 DTO 已经是当前事实来源
- 示例 JSON 可用于人工评审和测试样例
- JSON Schema 仍属于“待补齐的契约脚手架”

在 schema 补齐之前，建议以以下优先级理解契约：

1. C# DTO 定义
2. App mapper / validator 的实际行为
3. `docs/contracts/examples/*.json`

## Slope Percent Semantics

所有 `Percent` 字段都按“百分数”解释，而不是按小数比例解释：

- `2.0` means `2%`
- `2.0` does not mean `0.02`

当前需要保持这一语义稳定的字段包括：

- `GlobalSlopePercent`
- `GlobalTopSlopePercent`
- `GlobalBottomSlopePercent`
- `LegacySlopePercent`
- `BoundarySlabEditDto.SlopePercent`

## Preflight Enum Stability

Preflight `Severity` 在 P3 固定为以下值：

- `Passed`
- `Info`
- `Warning`
- `Blocking`

Preflight `ActionTarget` 在 P3 固定为以下值：

- `None`
- `FloorConfig`
- `LayerMapping`

## SuggestedCommandTag Compatibility

- `SuggestedCommandTag` 当前保留为兼容字段。
- 当前约定值为 `FloorConfig` / `LayerMapping` / 空字符串。
- P3 不删除它。
- 后续是否改为完全由 `ActionTarget` 派生，另行评估。

## Passed Check Grouping Behavior

- `Passed` 保留在 `Checks`
- `Passed` 不进入 `BlockingChecks`
- `Passed` 不进入 `WarningChecks`
- `Passed` 不进入 `InfoChecks`

## 命名建议

建议后续继续保持下面的命名习惯：

- `*.request.example.json`
  表示请求样例
- `*.response.example.json`
  表示响应样例
- `*.schema.json`
  表示机器校验 schema
- `*Dto.cs`
  表示契约层 DTO

顶层请求/响应名称尽量直接表达用例，而不是表达 UI：

- `SaveFloorConfigDocumentRequestDto`
- `SectionPreflightReportDto`
- `LayerMappingWorkspaceDto`

不要使用带明显宿主耦合的命名，例如：

- `FloorConfigWindowPayload`
- `PaletteSaveModel`
- `AutoCadFloorConfigRequest`

## 修改这层时的原则

- 优先加字段，不轻易改字段含义。
- 如果字段语义变化明显，优先新增字段而不是复用旧字段。
- UI 层不要把 WPF 控件状态直接泄漏进 Contracts。
- Core/Infrastructure 内部对象不要直接冒充 Contracts 返回给外部。
- 示例 JSON 改动时，尽量同步更新对应 DTO 和测试。

## 后续建议

下一步最值得补的是：

1. 为当前已落地的示例补齐真实存在的 `schemas/*.schema.json`。
2. 给 `Preflight`、`FloorConfig Save`、`LayerMapping` 建立一一对应的 schema 与 example。
3. 在测试里增加 example/schema/DTO 对齐检查，避免文档和代码漂移。

如果后续继续扩 Contracts，建议优先新增这些主题目录：

- `Generation`
- `Diagnostics`
- `LayerMapping`
- `Workbench`

其中 `Generation` 和 `Diagnostics` 最适合承接未来的批处理、脚本和 AI/DSL 输入输出边界。
