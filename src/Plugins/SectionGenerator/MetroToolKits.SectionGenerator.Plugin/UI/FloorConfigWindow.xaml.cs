using System.Windows;
using Microsoft.Extensions.Logging;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.Models;
using MetroToolKits.SectionGenerator.Contracts.Floors;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.Plugin.UI;

/// <summary>
/// 楼层配置管理窗口壳。
/// </summary>
public partial class FloorConfigWindow : Window
{
    public LoadedSectionConfig CurrentDocument => Panel.CurrentDocument;

    public SaveFloorConfigDocumentRequestDto? SaveRequest { get; private set; }

    public FloorConfigWindow(
        LoadedSectionConfig document,
        ISlabAssemblyTemplateCatalog slabTemplateCatalog,
        IFloorConfigDocumentAssembler floorConfigDocumentAssembler,
        IFloorConfigSaveRequestMapper saveRequestMapper,
        ISectionOutputConfigMapper sectionOutputConfigMapper,
        ILogger<FloorConfigWindow> logger)
    {
        InitializeComponent();

        Panel.Initialize(
            document,
            slabTemplateCatalog,
            floorConfigDocumentAssembler,
            saveRequestMapper,
            sectionOutputConfigMapper,
            logger);

        Panel.SaveRequested += Panel_SaveCompleted;
        Panel.CancelRequested += Panel_CancelRequested;
        Panel.PickAlignmentRequested += Panel_PickAlignmentRequested;
        Panel.PickScopeRequested += Panel_PickScopeRequested;
    }

    private void Panel_SaveCompleted(object? sender, FloorConfigSaveRequestedEventArgs e)
    {
        SaveRequest = e.Request;
        DialogResult = true;
        Close();
    }

    private void Panel_CancelRequested(object? sender, EventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Panel_PickAlignmentRequested(object? sender, FloorConfigSelectionRequestedEventArgs e)
    {
        DialogResult = null;
        Tag = ("PickAlignment", e.Floor);
        Close();
    }

    private void Panel_PickScopeRequested(object? sender, FloorConfigSelectionRequestedEventArgs e)
    {
        DialogResult = null;
        Tag = ("PickScope", e.Floor);
        Close();
    }
}
