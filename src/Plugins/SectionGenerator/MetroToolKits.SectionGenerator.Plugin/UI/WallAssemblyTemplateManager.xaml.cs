using System.Windows;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.Support;

namespace MetroToolKits.SectionGenerator.Plugin.UI;

public partial class WallAssemblyTemplateManager : Window
{
    public WallAssemblyTemplateManager(IWallAssemblyTemplateCatalog templateCatalog)
        : this(templateCatalog, new WallTemplateCatalogMapper())
    {
    }

    public WallAssemblyTemplateManager(
        IWallAssemblyTemplateCatalog templateCatalog,
        IWallTemplateCatalogMapper templateCatalogMapper)
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
