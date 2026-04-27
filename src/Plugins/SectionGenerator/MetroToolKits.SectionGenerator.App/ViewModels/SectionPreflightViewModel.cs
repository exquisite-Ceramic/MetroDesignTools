using System.Collections.ObjectModel;
using MetroToolKits.SectionGenerator.Contracts.Preflight;

namespace MetroToolKits.SectionGenerator.App.ViewModels;

public sealed class SectionPreflightViewModel
{
    public ObservableCollection<SectionPreflightCheckItemViewModel> Checks { get; } = new();

    public ObservableCollection<SectionPreflightFloorStatusViewModel> Floors { get; } = new();

    public string DrawingDisplayName { get; private set; } = string.Empty;

    public string DrawingStatusText { get; private set; } = string.Empty;

    public string ConfigSourceText { get; private set; } = string.Empty;

    public string AlignmentBaseFloorName { get; private set; } = string.Empty;

    public string SummaryText { get; private set; } = string.Empty;

    public bool CanGenerate { get; private set; }

    public int FloorCount { get; private set; }

    public int BlockingCount { get; private set; }

    public int WarningCount { get; private set; }

    public int InfoCount { get; private set; }

    public void Load(SectionPreflightReportDto report)
    {
        ArgumentNullException.ThrowIfNull(report);

        DrawingDisplayName = report.DrawingDisplayName;
        DrawingStatusText = report.DrawingStatusText;
        ConfigSourceText = report.ConfigSourceText;
        AlignmentBaseFloorName = report.AlignmentBaseFloorName;
        SummaryText = report.SummaryText;
        CanGenerate = report.CanGenerate;
        FloorCount = report.FloorCount;
        BlockingCount = report.BlockingCount;
        WarningCount = report.WarningCount;
        InfoCount = report.InfoCount;

        Checks.Clear();
        foreach (var check in report.Checks)
        {
            Checks.Add(new SectionPreflightCheckItemViewModel(check));
        }

        Floors.Clear();
        foreach (var floor in report.Floors)
        {
            Floors.Add(new SectionPreflightFloorStatusViewModel(floor));
        }
    }
}

public sealed class SectionPreflightCheckItemViewModel
{
    public SectionPreflightCheckItemViewModel(SectionPreflightCheckItemDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        Title = dto.Title;
        Summary = dto.Summary;
        FloorName = dto.FloorName ?? string.Empty;
        Severity = dto.Severity;
        SeverityText = dto.Severity.ToString();
    }

    public string Title { get; }

    public string Summary { get; }

    public string FloorName { get; }

    public SectionPreflightSeverityDto Severity { get; }

    public string SeverityText { get; }
}

public sealed class SectionPreflightFloorStatusViewModel
{
    public SectionPreflightFloorStatusViewModel(SectionPreflightFloorStatusDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        FloorName = dto.FloorName;
        RoleText = dto.IsBaseFloor ? "基准层" : "参与层";
        AlignmentStatusText = dto.AlignmentStatusText;
        ScopeStatusText = dto.ScopeStatusText;
        BoundaryTemplateStatusText = dto.BoundaryTemplateStatusText;
        SkipReasonText = dto.SkipReasonText;
        ParticipationText = dto.WillBeSkipped ? "跳过" : "参与";
    }

    public string FloorName { get; }

    public string RoleText { get; }

    public string ParticipationText { get; }

    public string AlignmentStatusText { get; }

    public string ScopeStatusText { get; }

    public string BoundaryTemplateStatusText { get; }

    public string SkipReasonText { get; }
}
