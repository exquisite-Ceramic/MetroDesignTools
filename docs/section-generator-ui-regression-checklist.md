# Section Generator UI 回归验收清单

本文档用于第十五至第十七阶段之后的 UI 回归验收，重点覆盖 `SectionToolboxControl`、`FloorConfigPanel`、模板管理 panel/manager 的兼容路径，以及模板保存后的刷新链路。

## 1. 必跑构建命令

在仓库根目录执行：

```powershell
dotnet build E:\CAD二次开发\MetroDesignToolKits\MetroToolKits.sln -c Release --no-restore -nr:false -m:1
```

验收标准：

- 构建成功
- 若仅存在既有 `MSB3277` / `NU1900` warning，可记录但不视为本轮回归失败

## 2. SectionToolboxControl 打开验收

- 工具箱 palette 能正常打开
- `总览`、`楼层配置`、`图纸准备`、`剖面生成`、`剖面维护`、`模板与输出` Tab 能正常切换
- `模板与输出` Tab 默认显示模板首页
- 模板首页可见：
  - `进入墙体模板管理`
  - `进入楼板模板管理`
- 不应额外出现“打开独立模板窗口”按钮，除非后续阶段明确要求

## 3. 模板内嵌路径验收

### 3.1 墙体模板

- 点击 `进入墙体模板管理` 后，工具箱内部展示 `WallTemplatePanel`
- 模板列表加载正常
- 新建、删除、编辑模板行为正常
- 点击保存后：
  - 返回模板首页
  - 不关闭工具箱
- 点击取消后：
  - 返回模板首页
  - 不关闭工具箱

### 3.2 楼板模板

- 点击 `进入楼板模板管理` 后，工具箱内部展示 `SlabTemplatePanel`
- 模板列表加载正常
- 新建、删除、编辑模板行为正常
- 点击保存后：
  - 返回模板首页
  - 不关闭工具箱
- 点击取消后：
  - 返回模板首页
  - 不关闭工具箱

## 4. 模板保存刷新链路验收

### 4.1 墙体模板保存后

- `WallTemplatePanel.SaveCompleted` 触发后：
  - 墙体模板列表会 refresh
  - 页面返回模板首页
  - 工具箱状态摘要会 refresh
- `SectionToolboxControl` 只做宿主刷新协调，不承接模板保存、添加、删除、编辑逻辑

### 4.2 楼板模板保存后

- `SlabTemplatePanel.SaveCompleted` 触发后：
  - 楼板模板列表会 refresh
  - 页面返回模板首页
  - 工具箱状态摘要会 refresh
  - `FloorConfigPanel` 的楼板模板选项会 refresh

### 4.3 工具箱状态刷新确认

- 模板保存后，`TemplatesSummaryText` 会反映最新模板数量
- 相关工作台状态仍通过 `RefreshStatus()` 更新
- 不应新增 controller、navigation service 或第二套模板管理逻辑

## 5. FloorConfigPanel 验收

- 现有楼层配置保存流程不变
- `PickAlignment` / `PickScope` 行为不受影响
- `ReloadTemplateOptions()` 只刷新楼板模板下拉选项和当前显示状态
- `ReloadTemplateOptions()` 不调用：
  - `Save_Click`
  - `SaveFormEdits`
  - `PickAlignment`
  - `PickScope`
- 原已选模板仍存在时，应尽量保持原选中项
- 原已选模板已被删除时：
  - 不抛异常
  - 按现有默认逻辑回退到未绑定/空模板状态
- `ReloadTemplateOptions()` 不应改变当前楼层数据的业务含义

## 6. 原弹窗兼容路径验收

- `WallAssemblyTemplateManager` 仍可作为独立窗口正常打开
- `SlabAssemblyTemplateManager` 仍可作为独立窗口正常打开
- `SlabAssemblyTemplateManager.SelectedTemplateId` 兼容行为不变
- `FloorConfigPanel` 通过旧弹窗路径选择楼板模板时不报错
- 原独立窗口路径继续可用，不因工具箱内嵌 panel 而失效

## 7. 取消行为验收

### 7.1 墙体模板

- `WallTemplatePanel.CancelRequested` 只返回模板首页
- 取消不触发保存
- 取消不触发 `ReloadTemplates()`
- 取消不触发 `RefreshStatus()`

### 7.2 楼板模板

- `SlabTemplatePanel.CancelRequested` 只返回模板首页
- 取消不触发保存
- 取消不触发 `ReloadTemplates()`
- 取消不触发 `RefreshStatus()`
- 取消不触发 `FloorConfigPanel` 模板选项刷新

## 8. 事件绑定风险检查

- `SaveCompleted / CancelRequested` 只订阅一次
- `_templatePanelsInitialized` 保护仍存在
- `ReloadTemplates()` 不重新 `Initialize()`
- `ReloadTemplates()` 不重复绑定事件
- `ReloadTemplates()` 不关闭宿主

## 9. 不允许破坏的边界

- 不新增 controller
- 不新增 navigation service
- 不搬移模板保存逻辑到 `SectionToolboxControl`
- 不删除 `WallAssemblyTemplateManager / SlabAssemblyTemplateManager`
- 不破坏 `FloorConfigPanel` 现有保存、拾取、模板选择流程
- 不在 `SectionToolboxControl` 中重新暴露“打开独立模板窗口”按钮，除非后续阶段明确要求
- 不修改模板管理窗口的既有兼容调用路径

## 10. 建议验收顺序

- 先跑构建命令，确认编译通过
- 打开工具箱，验证 `模板与输出` Tab 默认首页
- 走一轮墙体模板内嵌保存/取消
- 走一轮楼板模板内嵌保存/取消
- 打开 `楼层配置` Tab，确认楼板模板选项刷新正常
- 再走一轮原独立模板管理窗口路径，确认兼容行为不变
