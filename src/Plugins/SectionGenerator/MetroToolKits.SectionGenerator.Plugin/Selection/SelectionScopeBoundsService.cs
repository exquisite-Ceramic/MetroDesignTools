using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using MetroToolKits.Foundation.Core.Geometry;

namespace MetroToolKits.SectionGenerator.Plugin.Selection;

internal static class SelectionScopeBoundsService
{
    public static bool TryPickBounds(
        Document document,
        PromptSelectionOptions options,
        out ScopeBounds2D bounds,
        out bool cancelled,
        out string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(options);

        var result = document.Editor.GetSelection(options);
        if (result.Status == PromptStatus.Cancel)
        {
            bounds = default;
            cancelled = true;
            errorMessage = string.Empty;
            return false;
        }

        if (result.Status != PromptStatus.OK || result.Value == null || result.Value.Count == 0)
        {
            bounds = default;
            cancelled = false;
            errorMessage = "未选择任何有效图元。";
            return false;
        }

        return TryComputeBounds(document.Database, result.Value.GetObjectIds(), out bounds, out errorMessage, out cancelled);
    }

    public static bool TryComputeBounds(
        Database database,
        IEnumerable<ObjectId> objectIds,
        out ScopeBounds2D bounds,
        out string errorMessage,
        out bool cancelled)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(objectIds);

        cancelled = false;
        var points = new List<Point3D>();

        using var tr = database.TransactionManager.StartTransaction();
        foreach (var objectId in objectIds)
        {
            if (objectId.IsNull || objectId.IsErased)
                continue;

            if (tr.GetObject(objectId, OpenMode.ForRead, false) is not Entity entity)
                continue;

            try
            {
                var extents = entity.GeometricExtents;
                points.Add(new Point3D(extents.MinPoint.X, extents.MinPoint.Y, 0));
                points.Add(new Point3D(extents.MaxPoint.X, extents.MaxPoint.Y, 0));
            }
            catch
            {
                // 某些代理对象可能没有几何包围盒，直接跳过。
            }
        }

        tr.Commit();

        if (points.Count == 0)
        {
            bounds = default;
            errorMessage = "所选图元没有可用的几何范围，无法计算范围框。";
            return false;
        }

        bounds = ScopeBounds2D.FromPoints(points);
        if (!bounds.IsValid())
        {
            errorMessage = "计算出的范围框无效，请重新选择图元。";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }
}
