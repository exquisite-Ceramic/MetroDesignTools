using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using MetroToolKits.SectionGenerator.App.Abstractions;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace MetroToolKits.SectionGenerator.Infrastructure.Repositories;

/// <summary>
/// 基于 XData 的剖面实体源引用仓储
/// </summary>
public sealed class XDataSectionReferenceRepository : ISectionReferenceRepository
{
    private const string AppName = "MK_SectionSourceRef";

    public SectionEntityReference? GetSectionEntityReference(string sectionEntityHandle)
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null || string.IsNullOrWhiteSpace(sectionEntityHandle))
        {
            return null;
        }

        var db = doc.Database;
        using var tr = db.TransactionManager.StartTransaction();

        try
        {
            var objId = db.GetObjectId(false, new Handle(Convert.ToInt64(sectionEntityHandle, 16)), 0);
            if (objId == ObjectId.Null)
            {
                return null;
            }

            if (tr.GetObject(objId, OpenMode.ForRead) is not Entity entity)
            {
                return null;
            }

            var xdata = entity.GetXDataForApplication(AppName);
            if (xdata == null)
            {
                return null;
            }

            var values = xdata.AsArray();
            if (values.Length < 2)
            {
                return null;
            }

            return new SectionEntityReference
            {
                SourceHandle = values[1].Value?.ToString() ?? string.Empty,
                ElementType = values.Length >= 3 ? values[2].Value?.ToString() ?? string.Empty : string.Empty
            };
        }
        catch
        {
            return null;
        }
    }
}
