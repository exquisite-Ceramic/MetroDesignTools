using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using MetroToolKits.Foundation.Core.Hosting;
using MetroToolKits.SectionGenerator.App.UseCases;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace MetroToolKits.SectionGenerator.Plugin.Commands;

/// <summary>
/// 恢复构件转换命令 - 从扩展字典读取备份并恢复原始属性
/// </summary>
[CommandBinding(SectionGeneratorCommandNames.RevertConversion)]
[CommandBinding(SectionGeneratorCommandNames.RevertAllConversions, nameof(RevertAll))]
public class RevertElementConversionCommand
{
    private readonly IElementConversionUseCase _conversionUseCase;

    public RevertElementConversionCommand(IElementConversionUseCase conversionUseCase)
    {
        _conversionUseCase = conversionUseCase;
    }

    public void Execute()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null) return;

        var ed = doc.Editor;

        ed.WriteMessage("\n=== 恢复构件转换 ===");
        ed.WriteMessage("\n选择要恢复的实体（已转换的构件）: ");

        // 选择实体
        var selectionResult = ed.GetSelection(new PromptSelectionOptions
        {
            MessageForAdding = "\n选择要恢复的实体: "
        });

        if (selectionResult.Status != PromptStatus.OK)
        {
            ed.WriteMessage("\n已取消选择。");
            return;
        }

        var selection = selectionResult.Value;
        ed.WriteMessage($"\n已选择 {selection.Count} 个实体。");
        var handles = selection.GetObjectIds()
            .Select(id => id.Handle.ToString())
            .ToArray();

        var result = _conversionUseCase.Revert(new ElementConversionRevertRequest
        {
            EntityHandles = handles
        });

        if (result.Success)
        {
            ed.WriteMessage($"\n\n=== 恢复完成 ===");
            ed.WriteMessage($"\n已恢复: {result.RestoredCount} 个实体");
            ed.WriteMessage($"\n已跳过: {result.SkippedCount} 个实体（无备份记录）");
        }
        else
        {
            ed.WriteMessage($"\n恢复失败：{result.ErrorMessage}");
        }
    }

    public void RevertAll()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null) return;

        var ed = doc.Editor;

        ed.WriteMessage("\n=== 恢复所有转换 ===");

        // 确认操作
        var confirmResult = ed.GetKeywords(
            "\n此操作将恢复所有已转换的构件，是否继续？",
            new[] { "是", "否" });

        if (confirmResult.Status != PromptStatus.OK || confirmResult.StringResult != "是")
        {
            ed.WriteMessage("\n已取消操作。");
            return;
        }

        var result = _conversionUseCase.RevertAll();
        if (result.Success)
        {
            ed.WriteMessage($"\n已恢复 {result.RestoredCount} 个实体的原始属性。");
        }
        else
        {
            ed.WriteMessage($"\n恢复失败：{result.ErrorMessage}");
        }
    }
}
