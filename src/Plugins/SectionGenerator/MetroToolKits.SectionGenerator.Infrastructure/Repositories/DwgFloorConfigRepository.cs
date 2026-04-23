using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Microsoft.Extensions.Logging;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.Core.Sections;
using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.Foundation.Core.Geometry;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace MetroToolKits.SectionGenerator.Infrastructure.Repositories;

/// <summary>
/// 将楼层配置直接内嵌到 DWG 中，保证复制/移动图纸时配置随图一起走。
/// 未保存图纸只使用当前会话的临时配置。
/// </summary>
public sealed class DwgFloorConfigRepository : IFloorConfigRepository
{
    private const string ConfigDictionaryName = "MK_SectionGenerator";
    private const string ConfigRecordName = "FloorConfig";
    private const int MaxChunkLength = 2000;

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true
    };

    private readonly ILogger<DwgFloorConfigRepository> _logger;
    private readonly ConditionalWeakTable<Database, SectionConfig> _transientConfigs = new();

    public DwgFloorConfigRepository(ILogger<DwgFloorConfigRepository> logger)
    {
        _logger = logger;
    }

    public SectionConfig Load()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null)
        {
            return CreateAnnotatedDefaultConfig(null, SectionConfigStorageSource.Missing, hasPersistedConfig: false);
        }

        var db = doc.Database;
        if (_transientConfigs.TryGetValue(db, out var transientConfig))
        {
            return Annotate(CloneConfig(transientConfig), doc, SectionConfigStorageSource.TransientUnsavedDrawing, hasPersistedConfig: false);
        }

        if (!IsDrawingSaved(db))
        {
            return CreateAnnotatedDefaultConfig(doc, SectionConfigStorageSource.TransientUnsavedDrawing, hasPersistedConfig: false);
        }

        using var tr = db.TransactionManager.StartTransaction();
        try
        {
            var loaded = TryLoadEmbeddedConfig(tr, db);
            tr.Commit();

            if (loaded != null)
            {
                _logger.LogDebug("已从 DWG 内嵌配置加载楼层配置: {Drawing}", GetDrawingDisplayName(doc));
                loaded.OutputConfig = NormalizeOutputConfig(loaded.OutputConfig);
                return Annotate(loaded, doc, SectionConfigStorageSource.EmbeddedDwg, hasPersistedConfig: true);
            }
        }
        catch (Exception ex)
        {
            tr.Abort();
            _logger.LogError(ex, "读取 DWG 内嵌楼层配置失败，回退到默认配置: {Drawing}", GetDrawingDisplayName(doc));
        }

        _logger.LogInformation("当前图纸未找到内嵌楼层配置，使用默认空白配置: {Drawing}", GetDrawingDisplayName(doc));
        return CreateAnnotatedDefaultConfig(doc, SectionConfigStorageSource.Missing, hasPersistedConfig: false);
    }

    public void Save(SectionConfig config)
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null)
        {
            return;
        }

        var db = doc.Database;
        var configToSave = CloneConfig(config);

        if (!IsDrawingSaved(db))
        {
            _transientConfigs.Remove(db);
            _transientConfigs.Add(db, configToSave);
            _logger.LogInformation("未保存图纸，楼层配置已写入会话内存: {Drawing}", GetDrawingDisplayName(doc));
            return;
        }

        using var lockDoc = doc.LockDocument();
        using var tr = db.TransactionManager.StartTransaction();
        try
        {
            SaveEmbeddedConfig(tr, db, configToSave);
            tr.Commit();
            _transientConfigs.Remove(db);
            _logger.LogInformation("楼层配置已写入 DWG: {Drawing}", GetDrawingDisplayName(doc));
        }
        catch (Exception ex)
        {
            tr.Abort();
            _logger.LogError(ex, "写入 DWG 内嵌楼层配置失败: {Drawing}", GetDrawingDisplayName(doc));
            throw;
        }
    }

    private static bool IsDrawingSaved(Database db)
        => !string.IsNullOrWhiteSpace(db.Filename);

    private static string GetDrawingDisplayName(Document? doc)
    {
        if (doc == null)
        {
            return "UnknownDrawing";
        }

        var fileName = doc.Database?.Filename;
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            return Path.GetFileName(fileName);
        }

        return doc.Name;
    }

    private static SectionConfig CreateDefault()
        => new()
        {
            GlobalTopSlopeEnabled = true,
            GlobalTopSlopeValue = 0.002,
            GlobalTopSlopeTarget = "StructuralSlab",
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

    private static SectionConfig CreateAnnotatedDefaultConfig(
        Document? doc,
        SectionConfigStorageSource source,
        bool hasPersistedConfig)
    {
        return Annotate(CreateDefault(), doc, source, hasPersistedConfig);
    }

    private static SectionConfig Annotate(
        SectionConfig config,
        Document? doc,
        SectionConfigStorageSource source,
        bool hasPersistedConfig)
    {
        config.RuntimeState = new SectionConfigRuntimeState
        {
            Source = source,
            HasPersistedConfig = hasPersistedConfig,
            IsCurrentDrawingSaved = doc != null && IsDrawingSaved(doc.Database),
            DrawingDisplayName = GetDrawingDisplayName(doc),
            DrawingPath = doc != null && IsDrawingSaved(doc.Database) ? doc.Database.Filename : null
        };
        return config;
    }

    private static SectionConfig CloneConfig(SectionConfig config)
    {
        var json = JsonSerializer.Serialize(config, WriteOptions);
        var cloned = JsonSerializer.Deserialize<SectionConfig>(json, ReadOptions) ?? CreateDefault();
        cloned.OutputConfig = NormalizeOutputConfig(cloned.OutputConfig);
        return cloned;
    }

    private static SectionConfig? TryLoadEmbeddedConfig(Transaction tr, Database db)
    {
        var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
        if (!nod.Contains(ConfigDictionaryName))
        {
            return null;
        }

        var configDictionary = (DBDictionary)tr.GetObject(nod.GetAt(ConfigDictionaryName), OpenMode.ForRead);
        if (!configDictionary.Contains(ConfigRecordName))
        {
            return null;
        }

        var xrecord = (Xrecord)tr.GetObject(configDictionary.GetAt(ConfigRecordName), OpenMode.ForRead);
        if (xrecord.Data == null)
        {
            return null;
        }

        var json = new StringBuilder();
        foreach (var value in xrecord.Data)
        {
            if (value.TypeCode == (int)DxfCode.Text && value.Value is string chunk)
            {
                json.Append(chunk);
            }
        }

        if (json.Length == 0)
        {
            return null;
        }

        var config = JsonSerializer.Deserialize<SectionConfig>(json.ToString(), ReadOptions);
        if (config != null)
        {
            config.OutputConfig = NormalizeOutputConfig(config.OutputConfig);
        }

        return config;
    }

    private static void SaveEmbeddedConfig(Transaction tr, Database db, SectionConfig config)
    {
        var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
        DBDictionary configDictionary;
        if (nod.Contains(ConfigDictionaryName))
        {
            configDictionary = (DBDictionary)tr.GetObject(nod.GetAt(ConfigDictionaryName), OpenMode.ForWrite);
        }
        else
        {
            nod.UpgradeOpen();
            configDictionary = new DBDictionary();
            nod.SetAt(ConfigDictionaryName, configDictionary);
            tr.AddNewlyCreatedDBObject(configDictionary, true);
        }

        var json = JsonSerializer.Serialize(config, WriteOptions);
        var buffer = new ResultBuffer(SplitToTypedValues(json).ToArray());

        if (configDictionary.Contains(ConfigRecordName))
        {
            var xrecord = (Xrecord)tr.GetObject(configDictionary.GetAt(ConfigRecordName), OpenMode.ForWrite);
            xrecord.Data = buffer;
        }
        else
        {
            var xrecord = new Xrecord
            {
                Data = buffer
            };
            configDictionary.SetAt(ConfigRecordName, xrecord);
            tr.AddNewlyCreatedDBObject(xrecord, true);
        }
    }

    private static IEnumerable<TypedValue> SplitToTypedValues(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield return new TypedValue((int)DxfCode.Text, string.Empty);
            yield break;
        }

        for (var index = 0; index < text.Length; index += MaxChunkLength)
        {
            var length = Math.Min(MaxChunkLength, text.Length - index);
            yield return new TypedValue((int)DxfCode.Text, text.Substring(index, length));
        }
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
}
