# SectionGenerator FloorConfig UI Role Verification

## 背景

Core 多楼层剖面默认使用 `FloorStackBoundaryPolicy.ShareInteriorBoundaries`。
该策略下，非首层 `BottomBoundarySlab` 不输出、不填充、不计入楼层堆叠高度。
楼层交界板默认由上一层 `TopBoundarySlab` 表达一次。

旧 UI 仍按每个 `FloorConfig` 固定显示底板/顶板配置，容易让用户误以为每层底板都会参与生成。
当前 UI 通过楼层角色、动态文案和非首层 bottom 禁用状态提示真实生成语义。

## 验证场景

### 1. 单层

1. 打开 FloorConfig 面板，只保留一个楼层 `F1`。
2. 检查当前楼层提示区。
3. 检查楼层列表。
4. 检查底边界和顶边界相关输入。

预期：

- 楼层列表显示 `F1 · 单层`。
- 当前楼层角色显示为 `单层`。
- bottom/top 都可编辑。
- 文案为底板/顶板：
  - 底边界板模板
  - 顶边界板模板
  - 底板厚度
  - 顶板厚度
  - 底板坡度
  - 顶板坡度

### 2. 两层

1. 新增两个楼层 `F1`、`F2`。
2. 选择 `F1`。
3. 选择 `F2`。

预期：

- 楼层列表显示：
  - `F1 · 底层`
  - `F2 · 顶层`
- `F1` 角色为底层，bottom/top 都可编辑。
- `F2` 角色为顶层，bottom 相关输入禁用但值不清空。
- `F2` top 文案为屋面/顶板。
- `F2` bottom 提示说明：本层下边界由上一层上部层间板表达，本层底边界配置保留但当前生成不使用。

### 3. 三层

1. 新增三个楼层 `F1`、`F2`、`F3`。
2. 依次选择三个楼层。

预期：

- 楼层列表显示：
  - `F1 · 底层`
  - `F2 · 中间层`
  - `F3 · 顶层`
- `F1` bottom/top 都可编辑。
- `F2`、`F3` bottom 相关输入禁用但值不清空。
- `F2` top 文案为上部层间板。
- `F3` top 文案为屋面/顶板。
- `F2`、`F3` 的 bottom 提示均说明该下边界由上一层表达。

### 4. 上移/下移

1. 准备三层 `F1`、`F2`、`F3`。
2. 选择中间层并点击上移。
3. 选择首层并点击下移。
4. 每次移动后检查楼层列表、当前楼层提示区、bottom/top 文案和 bottom 禁用状态。

预期：

- 移动后所有楼层角色立即刷新。
- 移到首层的楼层 bottom 重新可编辑。
- 移到非首层的楼层 bottom 禁用。
- 楼层列表的 `单层/底层/中间层/顶层` 文案与当前排序一致。
- 状态文本如缺范围、缺对齐点、基准层等仍保留在角色行下方。

### 5. 全局坡度

1. 在单层或底层启用/关闭全局底板坡度。
2. 检查 bottom slope 勾选框和值输入框。
3. 在非首层启用/关闭全局底板坡度。
4. 检查 bottom slope 勾选框和值输入框。

预期：

- 单层或底层时，全局底板坡度禁用逻辑保持旧行为。
- 非首层时，即使全局底板坡度未启用，bottom slope 控件仍因楼层角色禁用。
- 非首层时，Tooltip 优先解释共享层间板语义。
- Top slope 控件不受 bottom 角色禁用影响。

### 6. 保存/重新打开

1. 在某个楼层作为首层时设置 bottom 模板和 bottom 坡度。
2. 将该楼层移动到非首层，确认 bottom 输入被禁用。
3. 保存配置。
4. 重新打开 FloorConfig。
5. 选择该楼层。

预期：

- 非首层 bottom 配置值不丢失。
- UI 仍提示当前策略下 bottom 配置不参与生成。
- 若再次移动到首层，bottom 输入恢复可编辑，并显示此前保存的值。

## 回归验证

1. FloorConfig 保存流程正常。
2. SectionPreflight 不受 UI 角色提示影响。
3. GenSection 输出与 Core `ShareInteriorBoundaries` 行为一致：
   - 非首层 bottom 不输出线。
   - 非首层 bottom 不生成填充。
   - 非首层 bottom structural thickness 不计入高度。
4. Hatch 容错行为不受影响。
5. SightLineOptions 和视深看线不受影响。
6. 复制墙模板绑定流程不受影响。
7. `DrawAll` 策略暂不暴露 UI；如未来暴露，需要同步更新角色文案和禁用规则。

## 需要真实 AutoCAD/WPF 窗口验证

- 需要真实 AutoCAD 2018 `acad.exe` 中 `NETLOAD` 当前分支 DLL 后打开 FloorConfig 面板验证。
- 需要检查 WPF 列表中 `F1 · 底层` 等角色文本是否排版正常。
- 需要检查切换楼层、添加、删除、上移、下移后 UI 状态是否即时刷新。
- 需要检查禁用控件的 Tooltip 在 AutoCAD 承载窗口中是否可见。
- 需要检查保存并重新打开配置后，非首层 bottom 配置值是否仍保留。
