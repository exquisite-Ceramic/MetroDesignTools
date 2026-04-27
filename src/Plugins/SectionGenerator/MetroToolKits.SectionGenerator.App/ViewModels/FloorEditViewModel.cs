using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.App.ViewModels;

public sealed class FloorEditViewModel
{
    public FloorEditViewModel()
        : this(new FloorConfig())
    {
    }

    private FloorEditViewModel(FloorConfig domainFloor)
    {
        DomainFloor = domainFloor;
        ReloadFromDomain();
    }

    public FloorConfig DomainFloor { get; }

    public string Name { get; set; } = string.Empty;

    public double Height { get; set; } = 3000.0;

    public double FinishThickness { get; set; } = 120.0;

    public double BottomSlabThickness { get; set; } = 800.0;

    public double TopSlabThickness { get; set; } = 600.0;

    public bool LegacySlopeEnabled { get; set; }

    public double LegacySlopePercent { get; set; }

    public string LegacySlopeTarget { get; set; } = "StructuralSlab";

    public string TopBoundaryTemplateId { get; set; } = string.Empty;

    public bool TopBoundarySlopeEnabled { get; set; }

    public double TopBoundarySlopePercent { get; set; }

    public string TopBoundarySlopeTarget { get; set; } = "StructuralSlab";

    public string BottomBoundaryTemplateId { get; set; } = string.Empty;

    public bool BottomBoundarySlopeEnabled { get; set; }

    public double BottomBoundarySlopePercent { get; set; }

    public string BottomBoundarySlopeTarget { get; set; } = "StructuralSlab";

    public List<Point3D> AlignmentPoints { get; } = new();

    public ScopeBounds2D? ScopeBounds { get; set; }

    public static FloorEditViewModel FromDomain(FloorConfig floor)
    {
        ArgumentNullException.ThrowIfNull(floor);
        return new FloorEditViewModel(floor);
    }

    public static FloorEditViewModel CreateDefault(string name)
        => FromDomain(new FloorConfig
        {
            Name = name,
            Height = 3000,
            BottomSlabThickness = 800,
            TopSlabThickness = 600,
            FinishThickness = 120,
            TopBoundarySlab = new BoundarySlabConfig
            {
                SlopeEnabled = false,
                SlopeValue = 0,
                SlopeTarget = "StructuralSlab"
            },
            BottomBoundarySlab = new BoundarySlabConfig
            {
                SlopeEnabled = false,
                SlopeValue = 0,
                SlopeTarget = "StructuralSlab"
            }
        });

    public void ReloadFromDomain()
    {
        Name = DomainFloor.Name;
        Height = DomainFloor.Height;
        FinishThickness = DomainFloor.FinishThickness;
        BottomSlabThickness = DomainFloor.BottomSlabThickness;
        TopSlabThickness = DomainFloor.TopSlabThickness;
        LegacySlopeEnabled = DomainFloor.HasSlope;
        LegacySlopePercent = ToPercent(DomainFloor.SlopeValue);
        LegacySlopeTarget = DomainFloor.SlopeTarget;
        TopBoundaryTemplateId = DomainFloor.TopBoundarySlab.TemplateId;
        TopBoundarySlopeEnabled = DomainFloor.TopBoundarySlab.SlopeEnabled;
        TopBoundarySlopePercent = ToPercent(DomainFloor.TopBoundarySlab.SlopeValue);
        TopBoundarySlopeTarget = DomainFloor.TopBoundarySlab.SlopeTarget;
        BottomBoundaryTemplateId = DomainFloor.BottomBoundarySlab.TemplateId;
        BottomBoundarySlopeEnabled = DomainFloor.BottomBoundarySlab.SlopeEnabled;
        BottomBoundarySlopePercent = ToPercent(DomainFloor.BottomBoundarySlab.SlopeValue);
        BottomBoundarySlopeTarget = DomainFloor.BottomBoundarySlab.SlopeTarget;
        AlignmentPoints.Clear();
        AlignmentPoints.AddRange(DomainFloor.AlignmentPoints);
        ScopeBounds = DomainFloor.ScopeBounds;
    }

    public void ApplyToDomain()
    {
        DomainFloor.Name = Name?.Trim() ?? string.Empty;
        DomainFloor.Height = Height;
        DomainFloor.FinishThickness = FinishThickness;
        DomainFloor.BottomSlabThickness = BottomSlabThickness;
        DomainFloor.TopSlabThickness = TopSlabThickness;
        DomainFloor.HasSlope = LegacySlopeEnabled;
        DomainFloor.SlopeValue = ToDecimal(LegacySlopePercent);
        DomainFloor.SlopeTarget = LegacySlopeTarget?.Trim() ?? string.Empty;
        DomainFloor.TopBoundarySlab.TemplateId = TopBoundaryTemplateId?.Trim() ?? string.Empty;
        DomainFloor.TopBoundarySlab.SlopeEnabled = TopBoundarySlopeEnabled;
        DomainFloor.TopBoundarySlab.SlopeValue = ToDecimal(TopBoundarySlopePercent);
        DomainFloor.TopBoundarySlab.SlopeTarget = TopBoundarySlopeTarget?.Trim() ?? string.Empty;
        DomainFloor.BottomBoundarySlab.TemplateId = BottomBoundaryTemplateId?.Trim() ?? string.Empty;
        DomainFloor.BottomBoundarySlab.SlopeEnabled = BottomBoundarySlopeEnabled;
        DomainFloor.BottomBoundarySlab.SlopeValue = ToDecimal(BottomBoundarySlopePercent);
        DomainFloor.BottomBoundarySlab.SlopeTarget = BottomBoundarySlopeTarget?.Trim() ?? string.Empty;
        DomainFloor.AlignmentPoints = AlignmentPoints.ToList();
        DomainFloor.ScopeBounds = ScopeBounds;
    }

    private static double ToPercent(double decimalValue) => decimalValue * 100.0;

    private static double ToDecimal(double percentValue) => percentValue / 100.0;
}
