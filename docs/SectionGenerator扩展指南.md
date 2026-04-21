# SectionGenerator 扩展指南

**版本**: v1.1.0-beta  
**适用对象**: 二次开发人员

---

## 1. 扩展原则

新增能力时，优先遵守当前分层约束：

- `Foundation.*`：几何、CAD 基础封装、建筑构件模型
- `SectionGenerator.Core`：纯算法、剖面组合、哈希、楼层对齐逻辑
- `SectionGenerator.App`：用例编排、接口定义
- `SectionGenerator.Infrastructure`：AutoCAD 访问、XData、JSON、识别器、绘图
- `SectionGenerator.Plugin`：命令、WPF 窗口、命令行交互
- `Bootstrap`：插件加载、DI、命令桥接、按包隔离的运行期日志目录

不要把以下内容放进 `App/Core`：

- `Autodesk.AutoCAD.*`
- `System.Windows.*`
- `Application.DocumentManager` 之类的宿主调用

---

## 2. 新增构件类型的推荐步骤

以“新增楼梯构件”举例：

### 2.1 在 Foundation.Building 定义构件模型

位置建议：

- `src/Shared/MetroToolKits.Foundation.Building/Elements/Stair.cs`

要求：

- 继承 `BuildingElement`
- 实现 `GetBoundingBox()`
- 实现 `GetSectionGeometry(Line3D, Vector3D)`

### 2.2 在识别器中加入识别规则

位置：

- [LayerBasedElementRecognizer.cs](../src/Plugins/SectionGenerator/MetroToolKits.SectionGenerator.Infrastructure/Recognition/LayerBasedElementRecognizer.cs)

典型修改：

- 增加图层前缀映射
- 在 `ConvertToElement` 中加入新构件分支
- 读取 AutoCAD 实体几何并填充 `SourceHandle` / `SourceLayer`

### 2.3 如需新的剖面绘制表现，修改 Core

位置：

- `SectionComposer`
- `MultiFloorSectionComposer`
- 必要时新增 Core 帮助类

原则：

- 几何规则放在 `Core`
- AutoCAD 画图细节仍放在 `Infrastructure`

### 2.4 如需保存额外引用数据，扩展抽象与仓储

位置：

- `App/Abstractions`
- `Infrastructure/Repositories`

做法：

- 先在 `App` 中定义接口/DTO
- 再由 `Infrastructure` 实现

### 2.5 通过 Plugin 暴露命令或窗口

位置：

- `Plugin/Commands`
- `Plugin/UI`
- `SectionGeneratorPlugin.ConfigureServices`
- `SectionGeneratorPlugin.RegisterCommands`

---

## 3. 新增命令的推荐步骤

1. 在 `App/UseCases` 增加用例接口与实现
2. 如需外部能力，在 `App/Abstractions` 定义接口
3. 在 `Infrastructure` 提供实现
4. 在 `Plugin/Commands` 新建命令类，保留 AutoCAD/WPF 交互
5. 在 `SectionGeneratorPlugin` 注册服务，并在命令类上声明 `CommandBindingAttribute`
6. 更新 README、状态文档和用户手册
7. 增加单元测试或手动集成清单

---

## 4. 测试建议

### 4.1 优先补单元测试

适用范围：

- `Foundation.Core`
- `Foundation.Building`
- `SectionGenerator.Core`
- `SectionGenerator.App`（通过 Mock 抽象接口）

### 4.2 宿主相关能力走集成验证

适用范围：

- `Infrastructure`
- `Plugin`

验证方式：

- AutoCAD 宿主内手动执行
- 检查 XData、块生成、窗口交互、定位缩放行为

---

## 5. 本轮阶段七中沉淀的模式

### 5.1 楼层对齐逻辑集中在 Core

位置：

- [FloorSectionLineTransformer.cs](../src/Plugins/SectionGenerator/MetroToolKits.SectionGenerator.Core/Sections/FloorSectionLineTransformer.cs)

作用：

- 生成剖面前识别构件
- 更新检测时重建剖切线
- 多楼层组合时统一应用同一套对齐规则

### 5.2 双向定位继续走“App 抽象 + Infrastructure 实现”

相关接口：

- `ISectionReferenceRepository`
- `IEntityNavigationService`

这样可以保持：

- 命令层只处理交互
- 用例层只处理业务判断
- AutoCAD 视图缩放和实体访问仍留在 `Infrastructure`

### 5.3 配置窗口也要经 App 用例层

相关接口：

- `IFloorConfigUseCase`
- `IFloorConfigRepository`

原则：

- `FloorConfigWindow` 只处理 WPF 编辑状态
- `FloorConfigCommand` 只处理命令和拾点交互
- 配置加载/保存必须经 `App` 用例，不让 UI 直连仓储

---

## 6. 修改前自检清单

- 是否把宿主 API 写进了 `App` 或 `Core`
- 是否新增了未经过 `App.Abstractions` 的跨层调用
- 是否把 UI 状态放进 Bootstrap 全局静态对象
- 是否补了最少一条回归测试或一项手动验证记录
- 是否更新了 README / 状态文档 / 用户手册

---

## 7. 相关文档

- [architecture.md](./architecture.md)
- [构建与测试指南.md](./构建与测试指南.md)
- [阶段七-验收记录.md](./阶段七-验收记录.md)
