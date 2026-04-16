using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace MetroToolKits.Foundation.Cad.Services;

/// <summary>
/// 编辑器服务接口 - 封装 AutoCAD Editor 操作
/// </summary>
public interface IEditorService
{
    /// <summary>
    /// 获取点
    /// </summary>
    Point3d? GetPoint(string message);

    /// <summary>
    /// 选择实体
    /// </summary>
    PromptEntityResult? GetEntity(string message, Type allowedType);

    /// <summary>
    /// 选择多个实体
    /// </summary>
    PromptSelectionResult? GetSelection(string message, SelectionFilter? filter = null);

    /// <summary>
    /// 输出消息
    /// </summary>
    void WriteMessage(string message);

    /// <summary>
    /// 获取关键字
    /// </summary>
    string? GetKeyword(string message, string[] keywords, string defaultKeyword);

    /// <summary>
    /// 获取双精度数值
    /// </summary>
    double? GetDouble(string message, double defaultValue);
}
