你正在修改仓库：

E:/CAD二次开发/MetroDesignToolKits



当前已确认：

Core 层默认使用 FloorStackBoundaryPolicy.ShareInteriorBoundaries：

\- 单层：bottom/top 都生效

\- 多层首层：bottom/top 都生效

\- 多层非首层：bottom 不输出、不填充、不计高；top 生效



当前 UI 问题：

FloorConfigPanel 仍按旧模型显示每层都有“底板厚度 / 底边界板模板 / 底板坡度”，导致用户误以为非首层 bottom 配置会参与生成。

实际在 ShareInteriorBoundaries 下，非首层 bottom 配置会保留，但当前生成不使用。



本次目标：

让 FloorConfigPanel 具备楼层角色感知：

1\. 当前楼层显示：单层 / 底层 / 中间层 / 顶层。

2\. 显示堆叠策略：共享层间板。

3\. 非首层禁用 bottom 相关输入，并提示“由上一层上部层间板表达”。

4\. 动态调整 bottom/top 文案。

5\. 楼层列表显示角色。

6\. 添加、删除、上移、下移、选择楼层后，角色 UI 必须刷新。

7\. 不修改 Core 生成逻辑。

8\. 不修改 FloorConfig 保存结构。

9\. 非首层 bottom 配置值不删除、不清空，只是在当前策略下禁用并提示。

10\. 不修改 CadDrawingService、HatchWriter、SightLine、复制墙模板元数据逻辑。

11\. 不触碰无关文件，例如 AGENTS.md。

12\. 完成后编译 App / Plugin，并输出修改文件、刷新入口、编译结果。

