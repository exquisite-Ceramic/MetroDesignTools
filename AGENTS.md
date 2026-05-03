目标背景：
当前 SectionGenerator 的视深功能会把 view depth 范围内的候选构件转成 SightLineCandidate，再由 SightLineComposer 输出到 MK_看线 图层。
问题是：楼板/顶板/底板本身已经通过剖面线、边界线、填充表达；如果再作为视深候选输出矩形看线框，就会在楼板附近出现多条重复 MK_看线，干扰剖面表达。

本次目标：
默认禁止 Slab / CompositeSlabElement 生成视深看线。
墙、柱、复合墙等非楼板构件的视深看线行为保持不变。

架构约束：
1. Core 层只能做纯几何和组合逻辑，不允许引用 AutoCAD API。
2. 不修改 CadDrawingService，不从绘图阶段硬删 MK_看线。
3. 不修改 Hatch 相关逻辑。
4. 不影响 CutLines、SlabLines、HatchRegions 的生成。
5. 关闭/开启填充行为都不应受影响。
6. 修改要小步、可编译、可测试。
7. 保持现有代码风格、nullable 风格和测试风格。
8. 完成后输出修改文件、核心逻辑、测试结果和仍需 AutoCAD 2018 验证的点。