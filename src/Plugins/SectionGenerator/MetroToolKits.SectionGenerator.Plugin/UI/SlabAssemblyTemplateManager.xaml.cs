using System.Windows;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.Support;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.Plugin.UI;

public partial class SlabAssemblyTemplateManager : Window
{
    public string SelectedTemplateId => Panel.SelectedTemplateId;

    public SlabAssemblyTemplateManager(ISlabAssemblyTemplateCatalog templateCatalog)
        : this(templateCatalog, new SlabTemplateCatalogMapper())
    {
    }

    public SlabAssemblyTemplateManager(
        ISlabAssemblyTemplateCatalog templateCatalog,
        ISlabTemplateCatalogMapper templateCatalogMapper)
    {
        InitializeComponent();

        Panel.Initialize(templateCatalog, templateCatalogMapper);
        Panel.SaveCompleted += (_, _) =>
        {
            DialogResult = true;
            Close();
        };
        Panel.CancelRequested += (_, _) =>
        {
            DialogResult = false;
            Close();
        };
    }
}
