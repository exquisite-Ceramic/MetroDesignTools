using System.Collections.ObjectModel;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.Models;
using MetroToolKits.SectionGenerator.Contracts.Floors;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.App.ViewModels;

public sealed class FloorConfigViewModel
{
    public ObservableCollection<FloorEditViewModel> Floors { get; } = new();

    public FloorEditViewModel? SelectedFloor { get; private set; }

    public bool HasSingleFloor => Floors.Count == 1;

    public SectionOutputConfigViewModel OutputConfig { get; } = new();

    public LoadedSectionConfig Document { get; private set; } = new();

    public string AlignmentBaseFloorName { get; set; } = string.Empty;

    public bool GlobalSlopeEnabled { get; set; }

    public double GlobalSlopePercent { get; set; }

    public string GlobalSlopeTarget { get; set; } = "StructuralSlab";

    public bool GlobalTopSlopeEnabled { get; set; }

    public double GlobalTopSlopePercent { get; set; }

    public string GlobalTopSlopeTarget { get; set; } = "StructuralSlab";

    public bool GlobalBottomSlopeEnabled { get; set; }

    public double GlobalBottomSlopePercent { get; set; }

    public string GlobalBottomSlopeTarget { get; set; } = "StructuralSlab";

    public void Load(LoadedSectionConfig document, ISectionOutputConfigMapper outputConfigMapper, string? selectedFloorName = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(outputConfigMapper);

        Document = document;
        Document.Config ??= new SectionConfig();
        Document.OutputConfig ??= new SectionOutputConfig();

        Floors.Clear();
        foreach (var floor in Document.Config.Floors ?? [])
        {
            Floors.Add(FloorEditViewModel.FromDomain(floor));
        }

        AlignmentBaseFloorName = Document.Config.AlignmentBaseFloorName;
        var explicitTopSlopeEnabled = Document.Config.GlobalTopSlopeEnabled;
        var explicitTopSlopePercent = ToPercent(Document.Config.GlobalTopSlopeValue);
        var explicitTopSlopeTarget = Document.Config.GlobalTopSlopeTarget;
        var legacyGlobalSlopeEnabled = Document.Config.GlobalSlopeEnabled;
        var legacyGlobalSlopePercent = ToPercent(Document.Config.GlobalSlopeValue);
        var legacyGlobalSlopeTarget = Document.Config.GlobalSlopeTarget;

        GlobalTopSlopeEnabled = explicitTopSlopeEnabled || legacyGlobalSlopeEnabled;
        GlobalTopSlopePercent = explicitTopSlopeEnabled ? explicitTopSlopePercent : legacyGlobalSlopePercent;
        GlobalTopSlopeTarget = explicitTopSlopeEnabled
            ? explicitTopSlopeTarget
            : legacyGlobalSlopeTarget;
        GlobalBottomSlopeEnabled = Document.Config.GlobalBottomSlopeEnabled;
        GlobalBottomSlopePercent = ToPercent(Document.Config.GlobalBottomSlopeValue);
        GlobalBottomSlopeTarget = Document.Config.GlobalBottomSlopeTarget;
        // Legacy GlobalSlope 字段仅保留兼容用途，当前 UI/保存链路以显式顶板全局坡度为准。
        GlobalSlopeEnabled = GlobalTopSlopeEnabled;
        GlobalSlopePercent = GlobalTopSlopePercent;
        GlobalSlopeTarget = GlobalTopSlopeTarget;
        OutputConfig.Load(outputConfigMapper.ToDto(Document.OutputConfig));

        SelectedFloor = !string.IsNullOrWhiteSpace(selectedFloorName)
            ? Floors.FirstOrDefault(floor => string.Equals(floor.Name, selectedFloorName, StringComparison.OrdinalIgnoreCase))
            : null;
    }

    public FloorEditViewModel AddFloor()
    {
        var floor = FloorEditViewModel.CreateDefault($"F{Floors.Count + 1}");
        Floors.Add(floor);
        SelectedFloor = floor;

        if (Floors.Count == 1 && string.IsNullOrWhiteSpace(AlignmentBaseFloorName))
        {
            AlignmentBaseFloorName = floor.Name;
        }

        return floor;
    }

    public FloorEditViewModel? DeleteSelectedFloor()
    {
        if (SelectedFloor == null)
        {
            return null;
        }

        var removedIndex = Floors.IndexOf(SelectedFloor);
        var removedName = SelectedFloor.Name;
        Floors.Remove(SelectedFloor);

        if (string.Equals(AlignmentBaseFloorName, removedName, StringComparison.OrdinalIgnoreCase))
        {
            AlignmentBaseFloorName = Floors.FirstOrDefault()?.Name ?? string.Empty;
        }

        if (Floors.Count == 0)
        {
            SelectedFloor = null;
            return null;
        }

        SelectedFloor = Floors[Math.Min(removedIndex, Floors.Count - 1)];
        return SelectedFloor;
    }

    public bool MoveSelectedFloorUp()
    {
        if (SelectedFloor == null)
        {
            return false;
        }

        var index = Floors.IndexOf(SelectedFloor);
        if (index <= 0)
        {
            return false;
        }

        Floors.Move(index, index - 1);
        return true;
    }

    public bool MoveSelectedFloorDown()
    {
        if (SelectedFloor == null)
        {
            return false;
        }

        var index = Floors.IndexOf(SelectedFloor);
        if (index < 0 || index >= Floors.Count - 1)
        {
            return false;
        }

        Floors.Move(index, index + 1);
        return true;
    }

    public FloorEditViewModel? SelectFloor(string? floorName)
    {
        SelectedFloor = string.IsNullOrWhiteSpace(floorName)
            ? null
            : Floors.FirstOrDefault(floor => string.Equals(floor.Name, floorName, StringComparison.OrdinalIgnoreCase));
        return SelectedFloor;
    }

    public void SelectFloor(FloorEditViewModel? floor)
    {
        SelectedFloor = floor;
    }

    public void ApplyGlobalSettings(
        string? alignmentBaseFloorName,
        bool globalTopSlopeEnabled,
        double globalTopSlopePercent,
        string? globalTopSlopeTarget,
        bool globalBottomSlopeEnabled,
        double globalBottomSlopePercent,
        string? globalBottomSlopeTarget)
    {
        AlignmentBaseFloorName = alignmentBaseFloorName?.Trim() ?? string.Empty;
        GlobalTopSlopeEnabled = globalTopSlopeEnabled;
        GlobalTopSlopePercent = globalTopSlopePercent;
        GlobalTopSlopeTarget = globalTopSlopeTarget?.Trim() ?? string.Empty;
        GlobalBottomSlopeEnabled = globalBottomSlopeEnabled;
        GlobalBottomSlopePercent = globalBottomSlopePercent;
        GlobalBottomSlopeTarget = globalBottomSlopeTarget?.Trim() ?? string.Empty;

        // 兼容旧配置字段：继续把“全局坡度”镜像为顶板全局坡度，避免旧读取方失配。
        GlobalSlopeEnabled = GlobalTopSlopeEnabled;
        GlobalSlopePercent = GlobalTopSlopePercent;
        GlobalSlopeTarget = GlobalTopSlopeTarget;
    }

    public void ClearSelectedFloorAlignment()
    {
        if (SelectedFloor == null)
        {
            return;
        }

        SelectedFloor.AlignmentPoints.Clear();
        SelectedFloor.ApplyToDomain();
    }

    public void ClearSelectedFloorScope()
    {
        if (SelectedFloor == null)
        {
            return;
        }

        SelectedFloor.ScopeBounds = null;
        SelectedFloor.ApplyToDomain();
    }

    public LoadedSectionConfig BuildDisplayDocument()
    {
        var floors = Floors.Select(floor =>
        {
            floor.ApplyToDomain();
            return floor.DomainFloor;
        }).ToList();

        return new LoadedSectionConfig
        {
            Config = new SectionConfig
            {
                GlobalSlopeEnabled = GlobalSlopeEnabled,
                GlobalSlopeValue = ToDecimal(GlobalSlopePercent),
                GlobalSlopeTarget = GlobalSlopeTarget,
                GlobalTopSlopeEnabled = GlobalTopSlopeEnabled,
                GlobalTopSlopeValue = ToDecimal(GlobalTopSlopePercent),
                GlobalTopSlopeTarget = GlobalTopSlopeTarget,
                GlobalBottomSlopeEnabled = GlobalBottomSlopeEnabled,
                GlobalBottomSlopeValue = ToDecimal(GlobalBottomSlopePercent),
                GlobalBottomSlopeTarget = GlobalBottomSlopeTarget,
                AlignmentBaseFloorName = AlignmentBaseFloorName,
                Floors = floors
            },
            OutputConfig = Document.OutputConfig,
            RuntimeDiagnostics = Document.RuntimeDiagnostics,
            RuntimeState = Document.RuntimeState
        };
    }

    public void CommitToDocument(ISectionOutputConfigMapper outputConfigMapper)
    {
        ArgumentNullException.ThrowIfNull(outputConfigMapper);

        Document.Config ??= new SectionConfig();
        foreach (var floor in Floors)
        {
            floor.ApplyToDomain();
        }

        Document.Config.AlignmentBaseFloorName = AlignmentBaseFloorName?.Trim() ?? string.Empty;
        Document.Config.GlobalSlopeEnabled = GlobalSlopeEnabled;
        Document.Config.GlobalSlopeValue = ToDecimal(GlobalSlopePercent);
        Document.Config.GlobalSlopeTarget = GlobalSlopeTarget?.Trim() ?? string.Empty;
        Document.Config.GlobalTopSlopeEnabled = GlobalTopSlopeEnabled;
        Document.Config.GlobalTopSlopeValue = ToDecimal(GlobalTopSlopePercent);
        Document.Config.GlobalTopSlopeTarget = GlobalTopSlopeTarget?.Trim() ?? string.Empty;
        Document.Config.GlobalBottomSlopeEnabled = GlobalBottomSlopeEnabled;
        Document.Config.GlobalBottomSlopeValue = ToDecimal(GlobalBottomSlopePercent);
        Document.Config.GlobalBottomSlopeTarget = GlobalBottomSlopeTarget?.Trim() ?? string.Empty;
        Document.Config.Floors = Floors.Select(floor => floor.DomainFloor).ToList();
        outputConfigMapper.ApplyToDocument(OutputConfig.ToDto(), Document);
    }

    public SaveFloorConfigDocumentRequestDto BuildSaveRequest(
        IFloorConfigSaveRequestMapper saveRequestMapper,
        ISectionOutputConfigMapper outputConfigMapper)
    {
        ArgumentNullException.ThrowIfNull(saveRequestMapper);
        ArgumentNullException.ThrowIfNull(outputConfigMapper);

        CommitToDocument(outputConfigMapper);
        return new SaveFloorConfigDocumentRequestDto
        {
            FloorConfig = saveRequestMapper.ToRequest(Document),
            OutputConfig = OutputConfig.ToDto()
        };
    }

    private static double ToPercent(double decimalValue) => decimalValue * 100.0;

    private static double ToDecimal(double percentValue) => percentValue / 100.0;
}
