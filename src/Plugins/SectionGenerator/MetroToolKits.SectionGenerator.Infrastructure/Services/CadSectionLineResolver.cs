using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Microsoft.Extensions.Logging;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.App.Abstractions;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using AcadLine = Autodesk.AutoCAD.DatabaseServices.Line;
using SystemException = System.Exception;

namespace MetroToolKits.SectionGenerator.Infrastructure.Services;

/// <summary>
/// 基于 AutoCAD 句柄解析当前源剖切线几何。
/// </summary>
public sealed class CadSectionLineResolver : ISectionLineResolver
{
    private readonly ILogger<CadSectionLineResolver> _logger;

    public CadSectionLineResolver(ILogger<CadSectionLineResolver> logger)
    {
        _logger = logger;
    }

    public Line3D? ResolveCurrentLine(string cutLineHandle)
    {
        if (string.IsNullOrWhiteSpace(cutLineHandle))
            return null;

        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null)
            return null;

        try
        {
            var db = doc.Database;
            using var tr = db.TransactionManager.StartTransaction();

            var objId = db.GetObjectId(false, new Handle(Convert.ToInt64(cutLineHandle, 16)), 0);
            if (objId == ObjectId.Null)
            {
                tr.Abort();
                return null;
            }

            if (tr.GetObject(objId, OpenMode.ForRead, false) is not AcadLine line)
            {
                tr.Abort();
                _logger.LogWarning("源剖切线句柄 {Handle} 不是直线实体", cutLineHandle);
                return null;
            }

            var resolved = new Line3D(
                new Point3D(line.StartPoint.X, line.StartPoint.Y, line.StartPoint.Z),
                new Point3D(line.EndPoint.X, line.EndPoint.Y, line.EndPoint.Z));

            tr.Commit();
            return resolved;
        }
        catch (SystemException ex)
        {
            _logger.LogWarning(ex, "解析源剖切线句柄 {Handle} 失败", cutLineHandle);
            return null;
        }
    }
}
