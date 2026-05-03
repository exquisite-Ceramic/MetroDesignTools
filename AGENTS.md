目标背景：

当前用户在 AutoCAD 中 COPY 已转换过的墙体后，新实体会保留图层 MK\_结构墙\_\*，但不会自动拥有 MK\_ElementBackup 中按 Handle 保存的转换元数据，例如 ConvertedType=Wall、TemplateId=xxx。

因此剖面生成时，复制墙体虽然仍会被图层识别为 Wall，但 GetTemplateId 返回空，导致它不会应用墙体模板，而是退回 legacy BuildingWall 逻辑，使用默认 Thickness=200，最终出现墙体表达不一致、填充越界等问题。

本次目标：

当 GenSection 检测到“位于已转换墙图层，但缺少墙体模板元数据”的实体时，不要静默按 legacy wall 生成。

应该弹窗提示用户：

检测到若干复制/异常墙体缺少模板信息，是否将它们绑定到同图层已有墙体模板？

用户确认后，补写转换元数据，再继续生成剖面。

用户拒绝或无法推断模板时，不应继续生成错误墙体，应给出清晰提示。

架构约束：

1\. Core 层不能引用 AutoCAD API，也不能处理弹窗。

2\. App 层负责 use case、结果模型、诊断，不直接依赖 AutoCAD 类型。

3\. Infrastructure 层可以访问 AutoCAD Database、Transaction、Entity、Handle，并负责扫描和补写 MK\_ElementBackup 元数据。

4\. Plugin/Command/UI 层负责弹窗确认，不要在 Recognizer 或 Core 里弹窗。

5\. 不要把复制墙缺少模板信息的实体静默退回 ConvertToWall legacy 逻辑。

6\. 不要破坏已转换实体的原始图层还原能力。不要简单调用会覆盖 OriginalLayer 的方法，除非确认不会覆盖正确数据。

7\. 修改要小步、可编译、可测试。

8\. 保持现有 Result / Diagnostic / CommandPresenter 风格。

9\. 完成后输出修改文件、核心逻辑、测试结果、仍需 AutoCAD 2018 验证的点。

