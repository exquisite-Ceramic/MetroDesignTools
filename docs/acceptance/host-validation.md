# Host Validation Draft

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
