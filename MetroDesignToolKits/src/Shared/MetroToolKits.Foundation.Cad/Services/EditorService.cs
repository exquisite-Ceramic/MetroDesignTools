using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace MetroToolKits.Foundation.Cad.Services;

/// <summary>
/// 编辑器服务实现
/// </summary>
public class EditorService : IEditorService
{
    private Editor? GetEditor()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        return doc?.Editor;
    }

    public Point3d? GetPoint(string message)
    {
        var ed = GetEditor();
        if (ed == null) return null;

        var result = ed.GetPoint(message);
        return result.Status == PromptStatus.OK ? result.Value : null;
    }

    public PromptEntityResult? GetEntity(string message, Type allowedType)
    {
        var ed = GetEditor();
        if (ed == null) return null;

        var options = new PromptEntityOptions(message);
        options.SetRejectMessage("选择类型不匹配");
        options.AddAllowedClass(allowedType, true);

        return ed.GetEntity(options);
    }

    public PromptSelectionResult? GetSelection(string message, SelectionFilter? filter = null)
    {
        var ed = GetEditor();
        if (ed == null) return null;

        var options = new PromptSelectionOptions
        {
            MessageForAdding = message
        };

        return ed.GetSelection(options, filter);
    }

    public void WriteMessage(string message)
    {
        var ed = GetEditor();
        ed?.WriteMessage($"\n{message}");
    }

    public string? GetKeyword(string message, string[] keywords, string defaultKeyword)
    {
        var ed = GetEditor();
        if (ed == null) return defaultKeyword;

        var options = new PromptKeywordOptions(message);
        foreach (var keyword in keywords)
        {
            options.Keywords.Add(keyword);
        }
        options.Keywords.Default = defaultKeyword;

        var result = ed.GetKeywords(options);
        return result.Status == PromptStatus.OK ? result.StringResult : defaultKeyword;
    }

    public double? GetDouble(string message, double defaultValue)
    {
        var ed = GetEditor();
        if (ed == null) return defaultValue;

        var options = new PromptDoubleOptions(message)
        {
            DefaultValue = defaultValue,
            AllowNegative = false
        };

        var result = ed.GetDouble(options);
        return result.Status == PromptStatus.OK ? result.Value : defaultValue;
    }
}
