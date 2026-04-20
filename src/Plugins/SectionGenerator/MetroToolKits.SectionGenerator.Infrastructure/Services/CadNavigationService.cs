using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using MetroToolKits.SectionGenerator.App.Abstractions;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace MetroToolKits.SectionGenerator.Infrastructure.Services;

/// <summary>
/// CAD 实体与块定位服务
/// </summary>
public sealed class CadNavigationService : IEntityNavigationService
{
    public NavigationResult FocusEntity(string handle) => Focus(handle, requireBlockReference: false);

    public NavigationResult FocusBlock(string blockHandle) => Focus(blockHandle, requireBlockReference: true);

    private static NavigationResult Focus(string handle, bool requireBlockReference)
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null)
        {
            return new NavigationResult { Success = false, ErrorMessage = "当前没有活动图纸" };
        }

        if (string.IsNullOrWhiteSpace(handle))
        {
            return new NavigationResult { Success = false, ErrorMessage = "未提供有效句柄" };
        }

        var db = doc.Database;
        var ed = doc.Editor;

        try
        {
            using var tr = db.TransactionManager.StartTransaction();
            var objId = db.GetObjectId(false, new Handle(Convert.ToInt64(handle, 16)), 0);
            if (objId == ObjectId.Null)
            {
                return new NavigationResult { Success = false, ErrorMessage = "找不到对应实体" };
            }

            if (tr.GetObject(objId, OpenMode.ForRead) is not Entity entity)
            {
                return new NavigationResult { Success = false, ErrorMessage = "目标对象不是可定位实体" };
            }

            if (requireBlockReference && entity is not BlockReference)
            {
                return new NavigationResult { Success = false, ErrorMessage = "所选对象不是剖面块" };
            }

            var extents = entity.GeometricExtents;
            tr.Commit();

            ZoomToExtents(ed, extents);
            ed.SetImpliedSelection(new[] { objId });
            Application.UpdateScreen();

            return new NavigationResult { Success = true };
        }
        catch
        {
            return new NavigationResult { Success = false, ErrorMessage = "定位失败，目标可能已被删除或无法缩放" };
        }
    }

    private static void ZoomToExtents(Editor editor, Extents3d extents)
    {
        using var view = editor.GetCurrentView();

        var worldToEye =
            Matrix3d.Rotation(-view.ViewTwist, view.ViewDirection, view.Target) *
            Matrix3d.Displacement(Point3d.Origin - view.Target) *
            Matrix3d.WorldToPlane(new Plane(Point3d.Origin, view.ViewDirection));

        var minPoint = extents.MinPoint.TransformBy(worldToEye);
        var maxPoint = extents.MaxPoint.TransformBy(worldToEye);

        var width = Math.Abs(maxPoint.X - minPoint.X);
        var height = Math.Abs(maxPoint.Y - minPoint.Y);

        width = Math.Max(width, 1000);
        height = Math.Max(height, 1000);

        var aspectRatio = view.Width / view.Height;
        if (width / height > aspectRatio)
        {
            height = width / aspectRatio;
        }
        else
        {
            width = height * aspectRatio;
        }

        view.CenterPoint = new Point2d(
            (minPoint.X + maxPoint.X) / 2,
            (minPoint.Y + maxPoint.Y) / 2);
        view.Width = width * 1.2;
        view.Height = height * 1.2;

        editor.SetCurrentView(view);
    }
}
