using Microsoft.Extensions.Logging;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.Diagnostics;
using MetroToolKits.SectionGenerator.Core.Sections;
using MetroToolKits.Foundation.Core.Diagnostics;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.Foundation.Building.Types;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MetroToolKits.SectionGenerator.Infrastructure.Repositories;

/// <summary>
/// JSON 楼层配置仓储
/// </summary>
public sealed class JsonFloorConfigRepository : IFloorConfigRepository
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _filePath;
    private readonly string? _templatePath;
    private readonly ILogger<JsonFloorConfigRepository> _logger;

    public JsonFloorConfigRepository(
        string filePath,
        string? templatePath,
        ILogger<JsonFloorConfigRepository> logger)
    {
        _filePath = filePath;
        _templatePath = templatePath;
        _logger   = logger;
    }

    public SectionConfig Load()
    {
        _logger.LogDebug("加载配置文件: {FilePath}", _filePath);

        if (!File.Exists(_filePath))
        {
            if (TrySeedFromTemplate())
            {
                _logger.LogInformation("配置文件不存在，已从模板复制到用户目录: {FilePath}", _filePath);
            }
            else
            {
                _logger.LogWarning("配置文件不存在，使用默认配置: {FilePath}", _filePath);
                var defaultConfig = CreateDefault();
                Save(defaultConfig);
                return defaultConfig;
            }
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var document = JsonSerializer.Deserialize<SectionConfigDocument>(json, ReadOptions);
            if (document == null)
            {
                _logger.LogWarning("配置文件为空或无法反序列化，回退到默认配置: {FilePath}", _filePath);
                return CreateDefault();
            }

            var migrationDiagnostics = new List<OperationDiagnostic>();
            var config = MapToConfig(document, migrationDiagnostics, out var migratedFromLegacy, out var hasMigrationConflict);
            config.OutputConfig = NormalizeOutputConfig(config.OutputConfig);

            if (migrationDiagnostics.Count > 0)
            {
                config.RuntimeDiagnostics.AddRange(migrationDiagnostics);
                foreach (var diagnostic in migrationDiagnostics)
                {
                    _logger.LogWarning(
                        "配置迁移诊断，Code={Code}，Message={Message}",
                        diagnostic.Code,
                        diagnostic.Message);
                }
            }

            if (migratedFromLegacy && !hasMigrationConflict)
            {
                Save(config);
                _logger.LogInformation("检测到旧版楼层配置，已自动迁移并保存为新格式: {FilePath}", _filePath);
            }

            _logger.LogDebug("配置加载成功，楼层数: {FloorCount}", config.Floors.Count);
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "配置文件解析失败: {FilePath}", _filePath);
            return CreateDefault();
        }
    }

    public void Save(SectionConfig config)
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var document = MapToDocument(config);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(document, WriteOptions));
        _logger.LogDebug("配置已保存: {FilePath}", _filePath);
    }

    private bool TrySeedFromTemplate()
    {
        if (string.IsNullOrWhiteSpace(_templatePath) || !File.Exists(_templatePath))
        {
            return false;
        }

        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.Copy(_templatePath, _filePath, overwrite: false);
        return true;
    }

    private static SectionConfig CreateDefault() => new()
    {
        GlobalTopSlopeEnabled = true,
        GlobalTopSlopeValue   = 0.002,
        GlobalTopSlopeTarget  = "StructuralSlab",
        GlobalBottomSlopeEnabled = false,
        GlobalBottomSlopeValue = 0,
        GlobalBottomSlopeTarget = "StructuralSlab",
        AlignmentBaseFloorName = "F1",
        OutputConfig = new SectionOutputConfig
        {
            AnnotationOptions = new AnnotationOptions
            {
                GenerateAnnotations = false
            },
            HatchOptions = new HatchOptions
            {
                Enabled = false
            },
            LayerOptions = new LayerOptions()
        },
        Floors = new List<FloorConfig>
        {
            new()
            {
                Name = "F1",
                Height = 5200,
                FinishThickness = 120,
                BottomSlabThickness = 800,
                TopSlabThickness = 600,
                HasSlope = true,
                SlopeValue = 0.002,
                BottomBoundarySlab = new BoundarySlabConfig
                {
                    SlopeEnabled = false,
                    SlopeValue = 0,
                    SlopeTarget = "StructuralSlab"
                },
                TopBoundarySlab = new BoundarySlabConfig
                {
                    SlopeEnabled = true,
                    SlopeValue = 0.002,
                    SlopeTarget = "StructuralSlab"
                }
            }
        }
    };

    private static SectionConfig MapToConfig(
        SectionConfigDocument document,
        ICollection<OperationDiagnostic> migrationDiagnostics,
        out bool migratedFromLegacy,
        out bool hasMigrationConflict)
    {
        migratedFromLegacy = UsesLegacyAlignment(document);
        hasMigrationConflict = false;

        var config = new SectionConfig
        {
            GlobalSlopeEnabled = document.GlobalSlopeEnabled,
            GlobalSlopeValue = document.GlobalSlopeValue,
            GlobalSlopeTarget = document.GlobalSlopeTarget ?? "StructuralSlab",
            GlobalTopSlopeEnabled = document.GlobalTopSlopeEnabled ?? document.GlobalSlopeEnabled,
            GlobalTopSlopeValue = document.GlobalTopSlopeValue ?? document.GlobalSlopeValue,
            GlobalTopSlopeTarget = document.GlobalTopSlopeTarget ?? document.GlobalSlopeTarget ?? "StructuralSlab",
            GlobalBottomSlopeEnabled = document.GlobalBottomSlopeEnabled ?? false,
            GlobalBottomSlopeValue = document.GlobalBottomSlopeValue ?? 0,
            GlobalBottomSlopeTarget = document.GlobalBottomSlopeTarget ?? "StructuralSlab",
            AlignmentBaseFloorName = document.AlignmentBaseFloorName ?? string.Empty,
            OutputConfig = NormalizeOutputConfig(document.OutputConfig),
            Floors = document.Floors.Select(MapFloor).ToList()
        };

        if (!migratedFromLegacy)
            return config;

        var canonicalSource = document.Floors
            .Select(f => ClonePoints(f.AlignmentSourcePoints))
            .FirstOrDefault(points => points.Count > 0)
            ?? new List<Point3D>();

        string baseFloorName = string.Empty;
        if (canonicalSource.Count > 0)
        {
            var baseFloorDocument = document.Floors.FirstOrDefault(f =>
                ArePointSetsEquivalent(ClonePoints(f.AlignmentTargetPoints), canonicalSource));
            baseFloorName = baseFloorDocument?.Name ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(baseFloorName))
        {
            baseFloorName = document.Floors.FirstOrDefault(f =>
                ClonePoints(f.AlignmentTargetPoints).Count > 0 ||
                ClonePoints(f.AlignmentSourcePoints).Count > 0)?.Name ?? string.Empty;
        }

        config.AlignmentBaseFloorName = baseFloorName;

        foreach (var floor in config.Floors)
        {
            var legacyFloor = document.Floors.First(f => string.Equals(f.Name, floor.Name, StringComparison.OrdinalIgnoreCase));
            floor.AlignmentPoints = ClonePoints(legacyFloor.AlignmentTargetPoints);

            var legacySourcePoints = ClonePoints(legacyFloor.AlignmentSourcePoints);
            if (canonicalSource.Count > 0 &&
                legacySourcePoints.Count > 0 &&
                !ArePointSetsEquivalent(legacySourcePoints, canonicalSource))
            {
                floor.AlignmentPoints.Clear();
                hasMigrationConflict = true;
                migrationDiagnostics.Add(SectionGenerationDiagnosticFactory.AlignmentMigrationConflict(
                    floor.Name,
                    "旧版 AlignmentSourcePoints 与全局基准源点不一致"));
            }
        }

        return config;
    }

    private static SectionConfigDocument MapToDocument(SectionConfig config)
    {
        return new SectionConfigDocument
        {
            GlobalSlopeEnabled = config.GlobalSlopeEnabled,
            GlobalSlopeValue = config.GlobalSlopeValue,
            GlobalSlopeTarget = config.GlobalSlopeTarget,
            GlobalTopSlopeEnabled = config.GlobalTopSlopeEnabled,
            GlobalTopSlopeValue = config.GlobalTopSlopeValue,
            GlobalTopSlopeTarget = config.GlobalTopSlopeTarget,
            GlobalBottomSlopeEnabled = config.GlobalBottomSlopeEnabled,
            GlobalBottomSlopeValue = config.GlobalBottomSlopeValue,
            GlobalBottomSlopeTarget = config.GlobalBottomSlopeTarget,
            AlignmentBaseFloorName = config.AlignmentBaseFloorName,
            OutputConfig = NormalizeOutputConfig(config.OutputConfig),
            Floors = config.Floors.Select(floor => new FloorConfigDocument
            {
                Name = floor.Name,
                Height = floor.Height,
                FinishThickness = floor.FinishThickness,
                BottomSlabThickness = floor.BottomSlabThickness,
                TopSlabThickness = floor.TopSlabThickness,
                HasSlope = floor.HasSlope,
                SlopeValue = floor.SlopeValue,
                SlopeTarget = floor.SlopeTarget,
                BottomBoundarySlab = ToBoundarySlabDocument(floor.BottomBoundarySlab),
                TopBoundarySlab = ToBoundarySlabDocument(floor.TopBoundarySlab),
                AlignmentPoints = ToPointDocuments(floor.AlignmentPoints),
                ScopeBounds = ToScopeBoundsDocument(floor.ScopeBounds)
            }).ToList()
        };
    }

    private static FloorConfig MapFloor(FloorConfigDocument document) => new()
    {
        Name = document.Name ?? string.Empty,
        Height = document.Height,
        FinishThickness = document.FinishThickness,
        BottomSlabThickness = document.BottomSlabThickness,
        TopSlabThickness = document.TopSlabThickness,
        HasSlope = document.HasSlope,
        SlopeValue = document.SlopeValue,
        SlopeTarget = document.SlopeTarget ?? "StructuralSlab",
        BottomBoundarySlab = ToBoundarySlab(
            document.BottomBoundarySlab,
            fallbackSlopeEnabled: false,
            fallbackSlopeValue: 0,
            fallbackSlopeTarget: "StructuralSlab"),
        TopBoundarySlab = ToBoundarySlab(
            document.TopBoundarySlab,
            fallbackSlopeEnabled: document.HasSlope,
            fallbackSlopeValue: document.SlopeValue,
            fallbackSlopeTarget: document.SlopeTarget ?? "StructuralSlab"),
        AlignmentPoints = ToPoint3Ds(document.AlignmentPoints),
        ScopeBounds = ToScopeBounds(document.ScopeBounds)
    };

    private static bool UsesLegacyAlignment(SectionConfigDocument document)
    {
        return document.Floors.Any(f =>
            (f.AlignmentSourcePoints?.Count ?? 0) > 0 ||
            (f.AlignmentTargetPoints?.Count ?? 0) > 0);
    }

    private static List<Point3D> ClonePoints(IReadOnlyList<Point3D>? points)
        => points?.ToList() ?? new List<Point3D>();

    private static List<Point3D> ClonePoints(IReadOnlyList<PointDocument>? points)
        => ToPoint3Ds(points);

    private static List<Point3D> ToPoint3Ds(IReadOnlyList<PointDocument>? points)
    {
        if (points == null)
            return new List<Point3D>();

        return points
            .Select(point => new Point3D(point.X, point.Y, point.Z))
            .ToList();
    }

    private static List<PointDocument> ToPointDocuments(IReadOnlyList<Point3D>? points)
    {
        if (points == null)
            return new List<PointDocument>();

        return points
            .Select(point => new PointDocument
            {
                X = point.X,
                Y = point.Y,
                Z = point.Z
            })
            .ToList();
    }

    private static bool ArePointSetsEquivalent(
        IReadOnlyList<Point3D> left,
        IReadOnlyList<Point3D> right)
    {
        if (left.Count != right.Count)
            return false;

        for (int i = 0; i < left.Count; i++)
        {
            if (left[i].DistanceTo(right[i]) > FloorSectionLineTransformer.AlignmentTolerance)
                return false;
        }

        return true;
    }

    private static ScopeBounds2D? ToScopeBounds(ScopeBoundsDocument? document)
    {
        if (document == null)
            return null;

        return new ScopeBounds2D
        {
            MinX = document.MinX,
            MinY = document.MinY,
            MaxX = document.MaxX,
            MaxY = document.MaxY
        };
    }

    private static ScopeBoundsDocument? ToScopeBoundsDocument(ScopeBounds2D? scopeBounds)
    {
        if (!scopeBounds.HasValue)
            return null;

        return new ScopeBoundsDocument
        {
            MinX = scopeBounds.Value.MinX,
            MinY = scopeBounds.Value.MinY,
            MaxX = scopeBounds.Value.MaxX,
            MaxY = scopeBounds.Value.MaxY
        };
    }

    private static BoundarySlabConfig ToBoundarySlab(
        BoundarySlabDocument? document,
        bool fallbackSlopeEnabled,
        double fallbackSlopeValue,
        string fallbackSlopeTarget)
    {
        if (document == null)
        {
            return new BoundarySlabConfig
            {
                SlopeEnabled = fallbackSlopeEnabled,
                SlopeValue = fallbackSlopeValue,
                SlopeTarget = fallbackSlopeTarget
            };
        }

        return new BoundarySlabConfig
        {
            SlopeEnabled = document.SlopeEnabled,
            SlopeValue = document.SlopeValue,
            SlopeTarget = document.SlopeTarget ?? fallbackSlopeTarget
        };
    }

    private static BoundarySlabDocument ToBoundarySlabDocument(BoundarySlabConfig config)
    {
        return new BoundarySlabDocument
        {
            SlopeEnabled = config.SlopeEnabled,
            SlopeValue = config.SlopeValue,
            SlopeTarget = config.SlopeTarget
        };
    }

    private static SectionOutputConfig NormalizeOutputConfig(SectionOutputConfig? outputConfig)
    {
        outputConfig ??= new SectionOutputConfig();
        outputConfig.AnnotationOptions ??= new AnnotationOptions();
        outputConfig.HatchOptions ??= new HatchOptions();
        outputConfig.HatchOptions.WallHatch ??= HatchStyleOptions.CreateDefault();
        outputConfig.HatchOptions.ColumnHatch ??= HatchStyleOptions.CreateDefault();
        outputConfig.HatchOptions.SlabHatch ??= HatchStyleOptions.CreateDefault();
        outputConfig.LayerOptions ??= new LayerOptions();
        return outputConfig;
    }

    private sealed class SectionConfigDocument
    {
        public bool GlobalSlopeEnabled { get; set; }
        public double GlobalSlopeValue { get; set; } = 0.002;
        public string? GlobalSlopeTarget { get; set; } = "StructuralSlab";
        public bool? GlobalTopSlopeEnabled { get; set; }
        public double? GlobalTopSlopeValue { get; set; }
        public string? GlobalTopSlopeTarget { get; set; }
        public bool? GlobalBottomSlopeEnabled { get; set; }
        public double? GlobalBottomSlopeValue { get; set; }
        public string? GlobalBottomSlopeTarget { get; set; }
        public string? AlignmentBaseFloorName { get; set; }
        public SectionOutputConfig? OutputConfig { get; set; }
        public List<FloorConfigDocument> Floors { get; set; } = new();
    }

    private sealed class FloorConfigDocument
    {
        public string? Name { get; set; }
        public double Height { get; set; } = 3000.0;
        public double FinishThickness { get; set; } = 120.0;
        public double BottomSlabThickness { get; set; } = 800.0;
        public double TopSlabThickness { get; set; } = 600.0;
        public bool HasSlope { get; set; }
        public double SlopeValue { get; set; }
        public string? SlopeTarget { get; set; } = "StructuralSlab";
        public BoundarySlabDocument? BottomBoundarySlab { get; set; }
        public BoundarySlabDocument? TopBoundarySlab { get; set; }
        public List<PointDocument>? AlignmentPoints { get; set; }
        public ScopeBoundsDocument? ScopeBounds { get; set; }
        public List<PointDocument>? AlignmentSourcePoints { get; set; }
        public List<PointDocument>? AlignmentTargetPoints { get; set; }
    }

    private sealed class PointDocument
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }

    private sealed class ScopeBoundsDocument
    {
        public double MinX { get; set; }
        public double MinY { get; set; }
        public double MaxX { get; set; }
        public double MaxY { get; set; }
    }

    private sealed class BoundarySlabDocument
    {
        public bool SlopeEnabled { get; set; }
        public double SlopeValue { get; set; }
        public string? SlopeTarget { get; set; } = "StructuralSlab";
    }
}
