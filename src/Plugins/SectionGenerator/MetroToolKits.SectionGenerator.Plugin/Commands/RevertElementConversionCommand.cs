using MetroToolKits.SectionGenerator.Infrastructure.Services;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace MetroToolKits.SectionGenerator.Plugin.Commands;

/// <summary>
/// 恢复构件转换命令 - 从扩展字典读取备份并恢复原始属性
/// </summary>
public class RevertElementConversionCommand
{
    private readonly ElementConversionBackupService _backupService;

    public RevertElementConversionCommand(ElementConversionBackupService backupService)
    {
        _backupService = backupService;
    }

    public void Execute()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null) return;

        var ed = doc.Editor;
        var db = doc.Database;

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

        using var lockDoc = doc.LockDocument();
        using var tr = db.TransactionManager.StartTransaction();

        try
        {
            int restoredCount = 0;
            int skippedCount = 0;

            foreach (var objId in selection.GetObjectIds())
            {
                var entity = (Entity)tr.GetObject(objId, OpenMode.ForRead);

                if (_backupService.HasBackup(entity))
                {
                    var convertedType = _backupService.GetConvertedType(entity);
                    entity.UpgradeOpen();

                    if (_backupService.RestoreEntity(entity))
                    {
                        restoredCount++;
                        ed.WriteMessage($"\n已恢复: {entity.Handle} (原类型: {convertedType})");
                    }
                }
                else
                {
                    skippedCount++;
                }
            }

            tr.Commit();

            ed.WriteMessage($"\n\n=== 恢复完成 ===");
            ed.WriteMessage($"\n已恢复: {restoredCount} 个实体");
            ed.WriteMessage($"\n已跳过: {skippedCount} 个实体（无备份记录）");
        }
        catch
        {
            tr.Abort();
            throw;
        }
    }

    public void RevertAll()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null) return;

        var ed = doc.Editor;
        var db = doc.Database;

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

        using var lockDoc = doc.LockDocument();
        using var tr = db.TransactionManager.StartTransaction();

        try
        {
            int restoredCount = 0;

            // 遍历模型空间
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            foreach (var objId in btr)
            {
                var entity = (Entity)tr.GetObject(objId, OpenMode.ForRead);

                if (_backupService.HasBackup(entity))
                {
                    entity.UpgradeOpen();
                    if (_backupService.RestoreEntity(entity))
                    {
                        restoredCount++;
                    }
                }
            }

            tr.Commit();
            ed.WriteMessage($"\n已恢复 {restoredCount} 个实体的原始属性。");
        }
        catch
        {
            tr.Abort();
            throw;
        }
    }
}

