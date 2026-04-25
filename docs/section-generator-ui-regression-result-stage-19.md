# Section Generator UI 回归验收记录（Stage 19）

## 1. 验收日期

- 2026-04-25

## 2. 当前 git commit

- `3e7115a3b01b0af8a687faeb11da5d8cd3f55954`

## 3. build 命令和结果

执行命令：

```powershell
dotnet build E:\CAD二次开发\MetroDesignToolKits\MetroToolKits.sln -c Release --no-restore -nr:false -m:1
```

结果：

- 构建通过
- `0 个错误`
- `14 个警告`
- 主要为既有 `MSB3277` 和测试项目 `NU1900`
- 未对 warning 做处理，本阶段不包含 warning 修复

## 4. SectionToolboxControl 打开验收结果

- 工具箱 palette 能正常打开：待人工确认
- Tab 能正常切换：待人工确认
- `模板与输出` Tab 能显示模板首页：待人工确认

基于代码结构的静态确认：

- `SectionToolboxControl` 仍包含 `TemplatesHomeView / WallTemplatePanelView / SlabTemplatePanelView`
- 模板首页入口仍为：
  - `进入墙体模板管理`
  - `进入楼板模板管理`

## 5. 模板内嵌路径验收结果

### 墙体模板

- 点击 `进入墙体模板管理` 能展示 `WallTemplatePanel`：待人工确认
- 保存后返回模板首页：代码路径已确认，人工界面效果待确认
- 取消后返回模板首页：代码路径已确认，人工界面效果待确认

### 楼板模板

- 点击 `进入楼板模板管理` 能展示 `SlabTemplatePanel`：待人工确认
- 保存后返回模板首页：代码路径已确认，人工界面效果待确认
- 取消后返回模板首页：代码路径已确认，人工界面效果待确认

## 6. 模板保存刷新链路验收结果

### 墙体模板保存后

已确认代码链路：

- `WallTemplatePanel.SaveCompleted`
- `SectionToolboxControl.RefreshTemplateState(reloadWallTemplatePanel: true)`
- `ShowTemplateHome()`
- `RefreshToolboxState()`

验收判断：

- 墙体模板列表刷新：代码已确认
- 工具箱状态摘要刷新：代码已确认
- 实际界面表现：待人工确认

### 楼板模板保存后

已确认代码链路：

- `SlabTemplatePanel.SaveCompleted`
- `SectionToolboxControl.RefreshTemplateState(reloadSlabTemplatePanel: true, reloadFloorConfigTemplateOptions: true)`
- `FloorConfigPanel.ReloadTemplateOptions()`
- `ShowTemplateHome()`
- `RefreshToolboxState()`

验收判断：

- 楼板模板列表刷新：代码已确认
- `FloorConfigPanel` 的楼板模板选项刷新：代码已确认
- 工具箱状态摘要刷新：代码已确认
- 实际界面表现：待人工确认

## 7. FloorConfigPanel 模板同步验收结果

已确认代码行为：

- `ReloadTemplateOptions()` 只刷新楼板模板下拉选项和当前显示状态
- 不调用：
  - `Save_Click`
  - `SaveFormEdits`
  - `PickAlignment`
  - `PickScope`
- 会优先保留当前 `TopBoundarySlab.TemplateId / BottomBoundarySlab.TemplateId`
- 模板仍存在时，继续选中原模板
- 模板已删除时，按现有默认逻辑回退到未绑定/空模板状态，不抛异常

人工界面验收：

- 现有楼层配置保存流程不变：待人工确认
- `PickAlignment / PickScope` 不受影响：待人工确认
- 原已选模板存在时应尽量保持选中：待人工确认
- 原已选模板被删除时不抛异常：待人工确认

## 8. 原弹窗兼容路径验收结果

静态确认：

- `WallAssemblyTemplateManager` 仍保留为独立窗口宿主壳
- `SlabAssemblyTemplateManager` 仍保留为独立窗口宿主壳
- `SlabAssemblyTemplateManager.SelectedTemplateId` 兼容行为未被本阶段改动

人工界面验收：

- `WallAssemblyTemplateManager` 仍可作为独立窗口打开：待人工确认
- `SlabAssemblyTemplateManager` 仍可作为独立窗口打开：待人工确认
- `FloorConfigPanel` 通过旧弹窗路径选择楼板模板不报错：待人工确认

## 9. 取消行为验收结果

已确认代码行为：

- `WallTemplatePanel.CancelRequested` 只返回模板首页
- `SlabTemplatePanel.CancelRequested` 只返回模板首页
- 取消不触发保存
- 取消不触发 `ReloadTemplates()`
- 取消不触发 `RefreshStatus()`

人工界面验收：

- 墙体模板取消后仅返回首页：待人工确认
- 楼板模板取消后仅返回首页：待人工确认
- 取消不会触发额外保存或刷新副作用：待人工确认

## 10. 事件重复绑定风险观察

已确认：

- `_templatePanelsInitialized` 保护仍存在
- `SaveCompleted / CancelRequested` 只在首次初始化时订阅一次
- `ReloadTemplates()` 不重新 `Initialize()`
- `ReloadTemplates()` 不重复绑定事件
- `ReloadTemplates()` 不关闭宿主

风险观察结论：

- 当前代码未见模板 panel 重复初始化或重复事件绑定风险
- 仍建议人工多次切换模板页面并重复保存/取消，确认不会出现重复回调

## 11. 未通过项列表

以下项目本次未能在当前会话中完成人工界面操作验证，统一标记为“待人工确认”：

- 工具箱 palette 实际打开与 Tab 切换
- 模板首页默认显示
- 墙体模板内嵌打开、保存、取消
- 楼板模板内嵌打开、保存、取消
- 模板保存后的实际界面刷新效果
- `FloorConfigPanel` 模板选项在真实界面中的保持/回退效果
- 原独立弹窗兼容路径实际打开与选择行为

说明：

- 上述项目并非代码失败，而是当前阶段未在 AutoCAD / 实际 UI 环境中逐项点击确认

## 12. 后续修复建议

- 在 AutoCAD 实际环境中按 [section-generator-ui-regression-checklist.md](/E:/CAD二次开发/MetroDesignToolKits/docs/section-generator-ui-regression-checklist.md) 完整走一轮人工回归
- 优先验证：
  - 墙体模板保存后返回模板首页并刷新摘要
  - 楼板模板保存后 `FloorConfigPanel` 模板下拉框同步刷新
  - 取消行为无副作用
  - 原独立弹窗路径仍可用
- 若人工验证发现模板刷新存在遗漏，优先在 `SectionToolboxControl` 的宿主刷新入口中补齐，而不要把模板业务逻辑搬进工具箱
