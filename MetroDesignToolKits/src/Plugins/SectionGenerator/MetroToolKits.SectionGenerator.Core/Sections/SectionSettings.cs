namespace SectionGenerator.Core.Sections;

public sealed class SectionSettings
{
    public double BottomSlabThickness { get; set; } = 800.0;   // 底板厚度 (mm)
    public double TopSlabThickness { get; set; } = 600.0;     // 顶板厚度 (mm)
    public double FinishThickness { get; set; } = 120.0;      // 装修面层厚度 (mm)
    public bool HasSlope { get; set; } = true;                // 是否有坡度
    public double SlopeValue { get; set; } = 0.002;           // 坡度值 (千分之二)
    public double FloorHeight { get; set; } = 5200.0;         // 层高 (mm)
}

