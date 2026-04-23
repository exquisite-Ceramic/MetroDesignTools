using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.Foundation.Core.Hosting;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.UseCases;
using MetroToolKits.SectionGenerator.Core.Sections;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace MetroToolKits.SectionGenerator.Plugin.Commands;

/// <summary>
/// 区域引导转换命令 - 框选区域后逐层分配构件类型
/// </summary>
[CommandBinding(SectionGeneratorCommandNames.ConvertRegion)]
public class ConvertRegionElementsCommand
{
    private readonly IElementTypeCatalog _typeCatalog;
    private readonly IWallAssemblyTemplateCatalog _wallTemplateCatalog;
    private readonly ISlabAssemblyTemplateCatalog _slabTemplateCatalog;
    private readonly IElementConversionUseCase _conversionUseCase;

    public ConvertRegionElementsCommand(
        IElementTypeCatalog typeCatalog,
        IWallAssemblyTemplateCatalog wallTemplateCatalog,
        ISlabAssemblyTemplateCatalog slabTemplateCatalog,
        IElementConversionUseCase conversionUseCase)
    {
        _typeCatalog = typeCatalog;
        _wallTemplateCatalog = wallTemplateCatalog;
        _slabTemplateCatalog = slabTemplateCatalog;
        _conversionUseCase = conversionUseCase;
    }

    public void Execute()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null) return;

        var ed = doc.Editor;
        var db = doc.Database;

        // 加载构件类型
        var types = _typeCatalog.GetEnabledTypes();
        if (types.Count == 0)
        {
            ed.WriteMessage("\n未找到构件类型配置，请检查 ElementTypes.json 文件。");
            return;
        }

        // 1. 框选范围
        var selectionResult = ed.GetSelection(new PromptSelectionOptions
        {
            MessageForAdding = "\n选择要转换的区域（框选）: "
        });

        if (selectionResult.Status != PromptStatus.OK)
        {
            ed.WriteMessage("\n已取消选择。");
            return;
        }

        var selection = selectionResult.Value;
        ed.WriteMessage($"\n已选择 {selection.Count} 个实体。");
        var selectedHandles = selection.GetObjectIds()
            .Select(id => id.Handle.ToString())
            .ToArray();

        // 2. 提取区域内图层列表
        var description = _conversionUseCase.DescribeSelection(new ElementConversionSelectionRequest
        {
            EntityHandles = selectedHandles
        });
        if (!description.Success || description.Layers.Count == 0)
        {
            ed.WriteMessage($"\n{description.ErrorMessage ?? "所选区域内未找到图层。"}");
            return;
        }
        var layers = description.Layers;

        ed.WriteMessage($"\n发现 {layers.Count} 个图层:");
        foreach (var layer in layers)
        {
            ed.WriteMessage($"\n  - {layer}");
        }

        // 3. 逐层高亮预览并等待用户输入快捷字母
        var layerMappings = new Dictionary<string, LayerTypeAssignment>();

        using var lockDoc = doc.LockDocument();
        using var tr = db.TransactionManager.StartTransaction();

        try
        {
            foreach (var layerName in layers)
            {
                // 高亮当前图层上的实体
                HighlightLayerEntities(tr, db, selection, layerName);

                // 显示提示
                var typeHints = string.Join(" | ", types.Select(t => $"{t.TypeName}({t.ShortcutKey})"));
                ed.WriteMessage($"\n\n当前图层: {layerName}");
                ed.WriteMessage($"\n可用类型: {typeHints}");
                ed.WriteMessage("\n输入快捷字母分配类型，ESC 跳过，Q 退出: ");

                // 获取用户输入
                var keyResult = GetKeyInput(ed);

                if (keyResult == null)
                {
                    // ESC - 跳过当前图层
                    ed.WriteMessage(" 已跳过");
                    UnhighlightLayerEntities(tr, db, selection, layerName);
                    continue;
                }

                if (keyResult.Equals("Q", StringComparison.OrdinalIgnoreCase))
                {
                    // Q - 退出
                    ed.WriteMessage(" 已退出");
                    break;
                }

                // 查找匹配的类型
                var matchedType = types.FirstOrDefault(t =>
                    t.ShortcutKey.Equals(keyResult, StringComparison.OrdinalIgnoreCase));

                if (matchedType != null)
                {
                    var assignment = new LayerTypeAssignment
                    {
                        SourceLayerName = layerName,
                        TypeId = matchedType.TypeId
                    };

                    if (string.Equals(matchedType.TypeId, "Wall", StringComparison.OrdinalIgnoreCase))
                    {
                        var selectedTemplate = PromptWallTemplate(ed);
                        if (selectedTemplate.Cancelled)
                        {
                            ed.WriteMessage(" 已取消模板选择");
                            UnhighlightLayerEntities(tr, db, selection, layerName);
                            continue;
                        }

                        assignment.TemplateId = selectedTemplate.Template?.TemplateId;
                        ed.WriteMessage(selectedTemplate.Template == null
                            ? $" → {matchedType.TypeName}[稳定模式]"
                            : $" → {matchedType.TypeName}[{selectedTemplate.Template.TemplateName}]");
                    }
                    else if (string.Equals(matchedType.TypeId, "Slab", StringComparison.OrdinalIgnoreCase))
                    {
                        var selectedTemplate = PromptSlabTemplate(ed);
                        if (selectedTemplate.Cancelled)
                        {
                            ed.WriteMessage(" 已取消模板选择");
                            UnhighlightLayerEntities(tr, db, selection, layerName);
                            continue;
                        }

                        assignment.TemplateId = selectedTemplate.Template?.TemplateId;
                        ed.WriteMessage(selectedTemplate.Template == null
                            ? $" → {matchedType.TypeName}[稳定模式]"
                            : $" → {matchedType.TypeName}[{selectedTemplate.Template.TemplateName}]");
                    }
                    else
                    {
                        ed.WriteMessage($" → {matchedType.TypeName}");
                    }

                    layerMappings[layerName] = assignment;
                }
                else
                {
                    ed.WriteMessage(" 无效输入，已跳过");
                }

                // 取消高亮
                UnhighlightLayerEntities(tr, db, selection, layerName);
            }

            // 4. 应用转换
            if (layerMappings.Count > 0)
            {
                var confirmResult = ed.GetKeywords(
                    $"\n\n已分配 {layerMappings.Count} 个图层映射。是否应用？",
                    new[] { "是", "否" });

                if (confirmResult.Status == PromptStatus.OK && confirmResult.StringResult == "是")
                {
                    tr.Abort();
                    var result = _conversionUseCase.ApplyMappings(new ElementConversionApplyRequest
                    {
                        ApplyToEntireDrawing = false,
                        EntityHandles = selectedHandles,
                        LayerMappings = layerMappings
                            .Select(mapping => new LayerTypeAssignment
                            {
                                SourceLayerName = mapping.Key,
                                TypeId = mapping.Value.TypeId,
                                TemplateId = mapping.Value.TemplateId
                            })
                            .ToList()
                    });

                    if (result.Success)
                    {
                        ed.WriteMessage($"\n转换完成，共转换 {result.ConvertedCount} 个实体。");
                    }
                    else
                    {
                        ed.WriteMessage($"\n转换失败：{result.ErrorMessage}");
                    }
                }
                else
                {
                    tr.Abort();
                    ed.WriteMessage("\n已取消转换。");
                }
            }
            else
            {
                tr.Abort();
                ed.WriteMessage("\n未分配任何映射，已退出。");
            }
        }
        catch
        {
            tr.Abort();
            throw;
        }
    }

    private void HighlightLayerEntities(Transaction tr, Database db, SelectionSet selection, string layerName)
    {
        foreach (var objId in selection.GetObjectIds())
        {
            var entity = (Entity)tr.GetObject(objId, OpenMode.ForRead);
            if (entity.Layer == layerName)
            {
                entity.UpgradeOpen();
                entity.Highlight();
            }
        }
    }

    private void UnhighlightLayerEntities(Transaction tr, Database db, SelectionSet selection, string layerName)
    {
        foreach (var objId in selection.GetObjectIds())
        {
            var entity = (Entity)tr.GetObject(objId, OpenMode.ForRead);
            if (entity.Layer == layerName)
            {
                entity.UpgradeOpen();
                entity.Unhighlight();
            }
        }
    }

    private string? GetKeyInput(Editor ed)
    {
        // 使用 GetString 获取单字符输入
        var options = new PromptStringOptions("")
        {
            AllowSpaces = false
        };

        var result = ed.GetString(options);
        if (result.Status != PromptStatus.OK) return null;

        return result.StringResult?.Trim().ToUpper();
    }

    private TemplateSelectionResult<WallAssemblyTemplate> PromptWallTemplate(Editor editor)
    {
        return PromptTemplate(
            editor,
            "墙体模板",
            _wallTemplateCatalog.GetAllTemplates(),
            template => template.TemplateName);
    }

    private TemplateSelectionResult<SlabAssemblyTemplate> PromptSlabTemplate(Editor editor)
    {
        return PromptTemplate(
            editor,
            "楼板模板",
            _slabTemplateCatalog.GetAllTemplates(),
            template => template.TemplateName);
    }

    private static TemplateSelectionResult<TTemplate> PromptTemplate<TTemplate>(
        Editor editor,
        string templateKindName,
        IReadOnlyList<TTemplate> templates,
        Func<TTemplate, string> displaySelector)
        where TTemplate : class
    {
        if (templates.Count == 0)
        {
            editor.WriteMessage($"\n未配置任何{templateKindName}，本次将沿用稳定模式。");
            return new TemplateSelectionResult<TTemplate> { Cancelled = false };
        }

        editor.WriteMessage($"\n可用{templateKindName}:");
        editor.WriteMessage("\n  0. 不绑定模板（沿用稳定模式）");
        for (int i = 0; i < templates.Count; i++)
        {
            editor.WriteMessage($"\n  {i + 1}. {displaySelector(templates[i])}");
        }

        var result = editor.GetString(new PromptStringOptions("\n输入模板序号（回车默认 0）: ")
        {
            AllowSpaces = false,
            UseDefaultValue = true,
            DefaultValue = "0"
        });

        if (result.Status == PromptStatus.Cancel)
        {
            return new TemplateSelectionResult<TTemplate> { Cancelled = true };
        }

        if (result.Status != PromptStatus.OK || string.IsNullOrWhiteSpace(result.StringResult))
        {
            return new TemplateSelectionResult<TTemplate> { Cancelled = false };
        }

        if (!int.TryParse(result.StringResult.Trim(), out var index) || index < 0 || index > templates.Count)
        {
            editor.WriteMessage("\n模板序号无效，本次将沿用稳定模式。");
            return new TemplateSelectionResult<TTemplate> { Cancelled = false };
        }

        if (index == 0)
        {
            return new TemplateSelectionResult<TTemplate> { Cancelled = false };
        }

        return new TemplateSelectionResult<TTemplate>
        {
            Cancelled = false,
            Template = templates[index - 1]
        };
    }

    private sealed class TemplateSelectionResult<TTemplate>
        where TTemplate : class
    {
        public bool Cancelled { get; init; }

        public TTemplate? Template { get; init; }
    }
}
