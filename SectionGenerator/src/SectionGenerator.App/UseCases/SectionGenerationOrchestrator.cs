using SectionGenerator.App.Abstractions;

namespace SectionGenerator.App.UseCases;

public sealed class SectionGenerationOrchestrator : CadServiceBase
{
    public SectionGenerationOrchestrator(ICadSession session, ITransactionRunner tr)
        : base(session, tr)
    {
    }

    // 这里后续承接 Commands 中的业务编排：
    // - 选择剖切线/辅助实体
    // - 读取几何 → 调用 Core 生成剖面结果
    // - 写回 CAD（实体/标注/填充）
}

