namespace MetroToolKits.Foundation.Core.Hosting;

/// <summary>
/// SectionGenerator 插件对外暴露的命令契约。
/// </summary>
public static class SectionGeneratorCommandNames
{
    public const string GenSection = "GenSection";
    public const string FloorConfig = "FloorConfig";
    public const string CheckSectionUpdates = "CheckSectionUpdates";
    public const string UpdateSection = "UpdateSection";
    public const string LocateSourceElement = "LocateSourceElement";
    public const string FindRelatedSections = "FindRelatedSections";
    public const string ShowToolbox = "ShowToolbox";
    public const string ConvertRegion = "ConvertRegion";
    public const string RevertConversion = "RevertConversion";
    public const string RevertAllConversions = "RevertAllConversions";
    public const string LayerMapping = "LayerMapping";
    public const string SectionPreflight = "SectionPreflight";
    public const string CheckBeforeGenerate = "CheckBeforeGenerate";
    public const string SectionSelfTestInternal = "MKSectionSelfTestInternal";
    public const string SectionHostAcceptanceInternal = "MKSectionHostAcceptanceInternal";
}
