using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Microsoft.Extensions.Logging;
using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.Bootstrap;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.UseCases;
using MetroToolKits.SectionGenerator.Core.Sections;
using MetroToolKits.SectionGenerator.Infrastructure.Services;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using AcadLine = Autodesk.AutoCAD.DatabaseServices.Line;
using AcadPoint3d = Autodesk.AutoCAD.Geometry.Point3d;
using ToolkitPoint3D = MetroToolKits.Foundation.Core.Geometry.Point3D;

namespace MetroToolKits.SectionGenerator.Plugin.Commands;

/// <summary>
/// 宿主内完整验收命令。
/// 在当前图纸中创建最小样例，串行验证生成、快照、关联查询、源定位、变更检测和更新链路。
/// </summary>
[CommandBinding(SectionGeneratorCommandNames.SectionHostAcceptance)]
public sealed class SectionGeneratorHostAcceptanceCommand
{
    private const string SectionSourceRefAppName = "MK_SectionSourceRef";

    private readonly IFloorConfigRepository _floorConfigRepository;
    private readonly IGenerateSectionUseCase _generateSectionUseCase;
    private readonly ICheckSectionUpdatesUseCase _checkSectionUpdatesUseCase;
    private readonly IUpdateSectionUseCase _updateSectionUseCase;
    private readonly ILocateSourceElementUseCase _locateSourceElementUseCase;
    private readonly IFindRelatedSectionsUseCase _findRelatedSectionsUseCase;
    private readonly ISectionSnapshotRepository _sectionSnapshotRepository;
    private readonly IElementRecognizer _elementRecognizer;
    private readonly FloorGeometryHasher _floorGeometryHasher;
    private readonly ElementConversionBackupService _elementConversionBackupService;
    private readonly ILogger<SectionGeneratorHostAcceptanceCommand> _logger;

    public SectionGeneratorHostAcceptanceCommand(
        IFloorConfigRepository floorConfigRepository,
        IGenerateSectionUseCase generateSectionUseCase,
        ICheckSectionUpdatesUseCase checkSectionUpdatesUseCase,
        IUpdateSectionUseCase updateSectionUseCase,
        ILocateSourceElementUseCase locateSourceElementUseCase,
        IFindRelatedSectionsUseCase findRelatedSectionsUseCase,
        ISectionSnapshotRepository sectionSnapshotRepository,
        IElementRecognizer elementRecognizer,
        FloorGeometryHasher floorGeometryHasher,
        ElementConversionBackupService elementConversionBackupService,
        ILogger<SectionGeneratorHostAcceptanceCommand> logger)
    {
        _floorConfigRepository = floorConfigRepository;
        _generateSectionUseCase = generateSectionUseCase;
        _checkSectionUpdatesUseCase = checkSectionUpdatesUseCase;
        _updateSectionUseCase = updateSectionUseCase;
        _locateSourceElementUseCase = locateSourceElementUseCase;
        _findRelatedSectionsUseCase = findRelatedSectionsUseCase;
        _sectionSnapshotRepository = sectionSnapshotRepository;
        _elementRecognizer = elementRecognizer;
        _floorGeometryHasher = floorGeometryHasher;
        _elementConversionBackupService = elementConversionBackupService;
        _logger = logger;
    }

    public void Execute()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        var ed = doc?.Editor;
        if (doc == null || ed == null)
            return;

        var originalConfig = _floorConfigRepository.Load();

        try
        {
            WriteMessage(ed, "=== MetroToolKits SectionGenerator Host Acceptance ===");

            ResetModelSpace(doc.Database);
            _floorConfigRepository.Save(BuildAcceptanceConfig());

            var fixture = CreateAcceptanceFixture(doc.Database, _elementConversionBackupService);
            WriteMessage(ed, $"Fixture Ready: CutLine={fixture.CutLineHandle}, Wall={fixture.PrimaryWallHandle}");

            var generateResult = _generateSectionUseCase.Execute(new GenerateSectionRequest
            {
                CutLineHandle = fixture.CutLineHandle,
                CutLineStart = fixture.CutLineStart,
                CutLineEnd = fixture.CutLineEnd,
                ViewDepth = 3000,
                InsertionPoint = new ToolkitPoint3D(5000, 0, 0)
            });

            Ensure(generateResult.Success, $"Generate failed: {DescribeGenerateFailure(generateResult)}");
            Ensure(!string.IsNullOrWhiteSpace(generateResult.BlockHandle), "Generate did not return a block handle.");
            WriteMessage(ed, $"Generate OK: Block={generateResult.BlockHandle}");
            ValidateProjectedBlockGeometry(doc.Database, generateResult.BlockHandle!);
            var originalInsertion = GetBlockInsertionPoint(doc.Database, generateResult.BlockHandle!);
            WriteMessage(ed, "Projected Geometry OK");

            var snapshot = _sectionSnapshotRepository.Load(generateResult.BlockHandle!);
            Ensure(snapshot != null, "Snapshot was not persisted.");
            var persistedSnapshot = snapshot!;
            Ensure(persistedSnapshot.SourceCutLineHandle == fixture.CutLineHandle, "Snapshot cut line handle does not match fixture.");
            Ensure(persistedSnapshot.FloorSnapshots.Count == 1, "Snapshot floor count should be 1 for acceptance config.");
            Ensure(
                persistedSnapshot.FloorSnapshots[0].SourceElementHandles.Contains(fixture.WallHandle, StringComparer.OrdinalIgnoreCase),
                "Snapshot source element handles do not contain the generated wall.");
            WriteMessage(ed, "Snapshot OK");

            var currentConfig = _floorConfigRepository.Load();
            var currentFloor = currentConfig.Floors.Single(floor => floor.Name == persistedSnapshot.FloorSnapshots[0].FloorName);
            var currentSectionLine = new Line3D(fixture.CutLineStart, fixture.CutLineEnd);
            var currentHash = _floorGeometryHasher.ComputeHash(
                _elementRecognizer.RecognizeElements(currentSectionLine, 3000).Elements);
            WriteMessage(
                ed,
                $"Hash Probe: Snapshot={persistedSnapshot.FloorSnapshots[0].GeometryHash}, Current={currentHash}");

            var relatedResult = _findRelatedSectionsUseCase.Execute(new FindRelatedSectionsRequest
            {
                SourceHandle = fixture.WallHandle
            });

            Ensure(relatedResult.Success, relatedResult.ErrorMessage ?? "FindRelatedSections returned failure.");
            Ensure(
                relatedResult.Sections.Any(section => string.Equals(section.BlockHandle, generateResult.BlockHandle, StringComparison.OrdinalIgnoreCase)),
                "FindRelatedSections did not return the generated block.");
            WriteMessage(ed, "FindRelatedSections OK");

            var sectionEntityHandle = FindFirstSectionEntityHandle(doc.Database, generateResult.BlockHandle!);
            var locateResult = _locateSourceElementUseCase.Execute(new LocateSourceElementRequest
            {
                SectionEntityHandle = sectionEntityHandle
            });

            Ensure(locateResult.Success, locateResult.ErrorMessage ?? "LocateSourceElement returned failure.");
            Ensure(
                string.Equals(locateResult.SourceHandle, fixture.PrimaryWallHandle, StringComparison.OrdinalIgnoreCase),
                "LocateSourceElement returned an unexpected source handle.");
            WriteMessage(ed, $"LocateSourceElement OK: SectionEntity={sectionEntityHandle}");

            var initialCheck = FindCheckResult(generateResult.BlockHandle!, _checkSectionUpdatesUseCase.Execute());
            WriteMessage(
                ed,
                $"CheckSectionUpdates Initial: Status={initialCheck.Status}, Floors={string.Join(",", initialCheck.OutdatedFloors)}");
            Ensure(
                initialCheck.Status == SectionUpdateStatus.UpToDate,
                $"Generated section should be up to date before source edits. Actual={initialCheck.Status}, Floors={string.Join(",", initialCheck.OutdatedFloors)}");
            WriteMessage(ed, "CheckSectionUpdates OK (initial)");

            MoveWall(doc.Database, fixture.WallHandle, deltaY: 400);
            var outdatedCheck = FindCheckResult(generateResult.BlockHandle!, _checkSectionUpdatesUseCase.Execute());
            WriteMessage(
                ed,
                $"CheckSectionUpdates After Edit: Status={outdatedCheck.Status}, Floors={string.Join(",", outdatedCheck.OutdatedFloors)}");
            Ensure(
                outdatedCheck.Status == SectionUpdateStatus.Outdated,
                $"Modified source wall should mark the section as outdated. Actual={outdatedCheck.Status}, Floors={string.Join(",", outdatedCheck.OutdatedFloors)}");
            WriteMessage(ed, "CheckSectionUpdates OK (outdated)");

            var updateResult = _updateSectionUseCase.Execute(new UpdateSectionRequest
            {
                BlockHandle = generateResult.BlockHandle!
            });

            Ensure(updateResult.Success, updateResult.ErrorMessage ?? "UpdateSection returned failure.");

            var remainingHandles = _sectionSnapshotRepository.FindAllSectionBlockHandles();
            Ensure(remainingHandles.Count == 1, $"Expected exactly one section block after update, got {remainingHandles.Count}.");

            var updatedHandle = remainingHandles[0];
            var finalCheck = FindCheckResult(updatedHandle, _checkSectionUpdatesUseCase.Execute());
            Ensure(finalCheck.Status == SectionUpdateStatus.UpToDate, "Updated section should return to up-to-date state.");
            var updatedInsertion = GetBlockInsertionPoint(doc.Database, updatedHandle);
            Ensure(
                Math.Abs(updatedInsertion.X - originalInsertion.X) <= 1e-6 &&
                Math.Abs(updatedInsertion.Y - originalInsertion.Y) <= 1e-6 &&
                Math.Abs(updatedInsertion.Z - originalInsertion.Z) <= 1e-6,
                "Updated section moved away from the original insertion point.");

            var relatedAfterUpdate = _findRelatedSectionsUseCase.Execute(new FindRelatedSectionsRequest
            {
                SourceHandle = fixture.WallHandle
            });
            Ensure(relatedAfterUpdate.Success, relatedAfterUpdate.ErrorMessage ?? "FindRelatedSections failed after update.");
            Ensure(
                relatedAfterUpdate.Sections.Any(section => string.Equals(section.BlockHandle, updatedHandle, StringComparison.OrdinalIgnoreCase)),
                "Updated section block was not returned by FindRelatedSections.");

            WriteMessage(ed, $"UpdateSection OK: NewBlock={updatedHandle}");
            WriteMessage(ed, "SECTION_HOST_ACCEPTANCE:OK");
            _logger.LogInformation("SectionGenerator 宿主验收通过");
        }
        catch (Exception ex)
        {
            WriteMessage(ed, $"SECTION_HOST_ACCEPTANCE:FAIL {ex.Message}");
            _logger.LogError(ex, "SectionGenerator 宿主验收失败");
        }
        finally
        {
            _floorConfigRepository.Save(originalConfig);
        }
    }

    private static SectionConfig BuildAcceptanceConfig() => new()
    {
        AlignmentBaseFloorName = "F1",
        Floors = new List<FloorConfig>
        {
            new()
            {
                Name = "F1",
                Height = 3000,
                BottomSlabThickness = 800,
                TopSlabThickness = 600,
                FinishThickness = 120,
                HasSlope = false,
                SlopeValue = 0,
                BottomBoundarySlab = new BoundarySlabConfig(),
                TopBoundarySlab = new BoundarySlabConfig()
            }
        }
    };

    private static AcceptanceFixture CreateAcceptanceFixture(Database db, ElementConversionBackupService backupService)
    {
        using var tr = db.TransactionManager.StartTransaction();
        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
        EnsureLayer(tr, db, "MK_结构墙");

        var cutLine = new AcadLine(new AcadPoint3d(0, 0, 0), new AcadPoint3d(0, 2000, 0));
        ms.AppendEntity(cutLine);
        tr.AddNewlyCreatedDBObject(cutLine, true);

        var wallLineA = new AcadLine(new AcadPoint3d(-500, 900, 0), new AcadPoint3d(500, 900, 0))
        {
            Layer = "MK_结构墙"
        };
        ms.AppendEntity(wallLineA);
        tr.AddNewlyCreatedDBObject(wallLineA, true);
        backupService.BackupEntity(tr, wallLineA, "Wall", "wall-200-finish");

        var wallLineB = new AcadLine(new AcadPoint3d(-500, 1100, 0), new AcadPoint3d(500, 1100, 0))
        {
            Layer = "MK_结构墙"
        };
        ms.AppendEntity(wallLineB);
        tr.AddNewlyCreatedDBObject(wallLineB, true);
        backupService.BackupEntity(tr, wallLineB, "Wall", "wall-200-finish");

        tr.Commit();

        return new AcceptanceFixture(
            cutLine.Handle.ToString(),
            wallLineA.Handle.ToString(),
            wallLineB.Handle.ToString(),
            new ToolkitPoint3D(cutLine.StartPoint.X, cutLine.StartPoint.Y, cutLine.StartPoint.Z),
            new ToolkitPoint3D(cutLine.EndPoint.X, cutLine.EndPoint.Y, cutLine.EndPoint.Z));
    }

    private static void MoveWall(Database db, string wallHandle, double deltaY)
    {
        using var tr = db.TransactionManager.StartTransaction();
        var objId = db.GetObjectId(false, new Handle(Convert.ToInt64(wallHandle, 16)), 0);
        Ensure(objId != ObjectId.Null, $"Wall handle {wallHandle} was not found.");

        var wallLine = tr.GetObject(objId, OpenMode.ForWrite) as AcadLine;
        Ensure(wallLine != null, $"Wall handle {wallHandle} is not a line.");
        var editableWallLine = wallLine!;

        editableWallLine.StartPoint = new AcadPoint3d(editableWallLine.StartPoint.X, editableWallLine.StartPoint.Y + deltaY, editableWallLine.StartPoint.Z);
        editableWallLine.EndPoint = new AcadPoint3d(editableWallLine.EndPoint.X, editableWallLine.EndPoint.Y + deltaY, editableWallLine.EndPoint.Z);
        tr.Commit();
    }

    private static string FindFirstSectionEntityHandle(Database db, string blockHandle)
    {
        using var tr = db.TransactionManager.StartTransaction();
        var objId = db.GetObjectId(false, new Handle(Convert.ToInt64(blockHandle, 16)), 0);
        Ensure(objId != ObjectId.Null, $"Block handle {blockHandle} was not found.");

        var blockRef = tr.GetObject(objId, OpenMode.ForRead) as BlockReference;
        Ensure(blockRef != null, $"Block handle {blockHandle} is not a block reference.");
        var resolvedBlockRef = blockRef!;

        var blockDef = (BlockTableRecord)tr.GetObject(resolvedBlockRef.BlockTableRecord, OpenMode.ForRead);
        foreach (var entityId in blockDef)
        {
            if (tr.GetObject(entityId, OpenMode.ForRead) is not Entity entity)
                continue;

            if (entity.GetXDataForApplication(SectionSourceRefAppName) != null)
            {
                tr.Commit();
                return entity.Handle.ToString();
            }
        }

        throw new InvalidOperationException("No source-aware section entity was found in the generated block.");
    }

    private static void ValidateProjectedBlockGeometry(Database db, string blockHandle)
    {
        using var tr = db.TransactionManager.StartTransaction();
        var objId = db.GetObjectId(false, new Handle(Convert.ToInt64(blockHandle, 16)), 0);
        Ensure(objId != ObjectId.Null, $"Block handle {blockHandle} was not found.");

        var blockRef = tr.GetObject(objId, OpenMode.ForRead) as BlockReference;
        Ensure(blockRef != null, $"Block handle {blockHandle} is not a block reference.");

        var blockDef = (BlockTableRecord)tr.GetObject(blockRef!.BlockTableRecord, OpenMode.ForRead);
        var lines = new List<AcadLine>();
        foreach (var entityId in blockDef)
        {
            if (tr.GetObject(entityId, OpenMode.ForRead) is AcadLine line)
                lines.Add(line);
        }

        Ensure(lines.Count > 0, "Generated block does not contain any line geometry.");
        Ensure(lines.All(line =>
                Math.Abs(line.StartPoint.Z) <= 1e-6 &&
                Math.Abs(line.EndPoint.Z) <= 1e-6),
            "Generated section geometry is not flattened onto the XY plane.");

        Ensure(lines.Any(line =>
                Math.Abs(line.StartPoint.X - line.EndPoint.X) > 1e-6 &&
                Math.Abs(line.StartPoint.Y - line.EndPoint.Y) <= 1e-6),
            "Generated section block does not contain a non-degenerate horizontal line.");

        Ensure(lines.Any(line =>
                Math.Abs(line.StartPoint.X - line.EndPoint.X) <= 1e-6 &&
                Math.Abs(line.StartPoint.Y - line.EndPoint.Y) > 1e-6),
            "Generated section block does not contain a non-degenerate vertical line.");

        tr.Commit();
    }

    private static ToolkitPoint3D GetBlockInsertionPoint(Database db, string blockHandle)
    {
        using var tr = db.TransactionManager.StartTransaction();
        var objId = db.GetObjectId(false, new Handle(Convert.ToInt64(blockHandle, 16)), 0);
        Ensure(objId != ObjectId.Null, $"Block handle {blockHandle} was not found.");

        var blockRef = tr.GetObject(objId, OpenMode.ForRead) as BlockReference;
        Ensure(blockRef != null, $"Block handle {blockHandle} is not a block reference.");
        var position = blockRef!.Position;
        tr.Commit();
        return new ToolkitPoint3D(position.X, position.Y, position.Z);
    }

    private static SectionCheckResult FindCheckResult(string blockHandle, CheckSectionUpdatesResult result)
    {
        Ensure(result.Status != Foundation.Core.Diagnostics.OperationStatus.Failed,
            $"CheckSectionUpdates failed: {result.Failure?.UserMessage ?? "Unknown failure"}");

        var item = result.Items.FirstOrDefault(checkItem =>
            string.Equals(checkItem.BlockHandle, blockHandle, StringComparison.OrdinalIgnoreCase));

        Ensure(item != null, $"CheckSectionUpdates did not return block {blockHandle}.");
        return item!;
    }

    private static string DescribeGenerateFailure(GenerateSectionResult result)
    {
        if (result.Failure == null)
            return result.ErrorMessage ?? "Unknown failure.";

        return $"{result.Failure.Code} @ {result.Failure.Stage}: {result.Failure.UserMessage}";
    }

    private static void ResetModelSpace(Database db)
    {
        using var tr = db.TransactionManager.StartTransaction();
        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

        foreach (ObjectId objId in ms)
        {
            if (tr.GetObject(objId, OpenMode.ForWrite, false) is Entity entity && !entity.IsErased)
                entity.Erase();
        }

        tr.Commit();
    }

    private static void EnsureLayer(Transaction tr, Database db, string layerName)
    {
        var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
        if (layerTable.Has(layerName))
            return;

        layerTable.UpgradeOpen();
        var layer = new LayerTableRecord { Name = layerName };
        layerTable.Add(layer);
        tr.AddNewlyCreatedDBObject(layer, true);
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void WriteMessage(Editor editor, string message)
        => editor.WriteMessage($"\n{message}");

    private sealed record AcceptanceFixture(
        string CutLineHandle,
        string PrimaryWallHandle,
        string SecondaryWallHandle,
        ToolkitPoint3D CutLineStart,
        ToolkitPoint3D CutLineEnd)
    {
        public string WallHandle => PrimaryWallHandle;
    }
}
