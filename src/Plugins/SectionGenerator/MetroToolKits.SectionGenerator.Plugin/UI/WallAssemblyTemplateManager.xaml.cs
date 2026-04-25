using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.Support;
using MetroToolKits.SectionGenerator.Contracts.Templates;

namespace MetroToolKits.SectionGenerator.Plugin.UI;

public partial class WallAssemblyTemplateManager : Window
{
    private readonly IWallAssemblyTemplateCatalog _templateCatalog;
    private readonly IWallTemplateCatalogMapper _templateCatalogMapper;
    private readonly ObservableCollection<WallAssemblyTemplateDto> _templates = new();
    private WallAssemblyTemplateDto? _currentTemplate;

    public WallAssemblyTemplateManager(IWallAssemblyTemplateCatalog templateCatalog)
        : this(templateCatalog, new WallTemplateCatalogMapper())
    {
    }

    public WallAssemblyTemplateManager(
        IWallAssemblyTemplateCatalog templateCatalog,
        IWallTemplateCatalogMapper templateCatalogMapper)
    {
        InitializeComponent();
        _templateCatalog = templateCatalog;
        _templateCatalogMapper = templateCatalogMapper;

        RecognitionModeBox.ItemsSource = Enum.GetValues<WallCoreRecognitionMode>();
        VerticalAnchorModeBox.ItemsSource = Enum.GetValues<WallVerticalAnchorMode>();
        LoadTemplates();
    }

    private void LoadTemplates()
    {
        _templates.Clear();
        var catalogDto = _templateCatalogMapper.ToDto(_templateCatalog.GetAllTemplates());
        foreach (var template in catalogDto.Templates)
        {
            _templates.Add(template);
        }

        TemplateListBox.ItemsSource = _templates;
        TemplateListBox.SelectedIndex = _templates.Count > 0 ? 0 : -1;
    }

    private void TemplateListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        CommitCurrentTemplate();
        _currentTemplate = TemplateListBox.SelectedItem as WallAssemblyTemplateDto;
        BindCurrentTemplate();
    }

    private void AddTemplate_Click(object sender, RoutedEventArgs e)
    {
        CommitCurrentTemplate();
        var template = new WallAssemblyTemplateDto
        {
            TemplateId = $"wall-template-{_templates.Count + 1}",
            TemplateName = $"墙体模板 {_templates.Count + 1}",
            CoreRule = new WallCoreRuleDto
            {
                Name = "结构芯",
                Thickness = 200,
                MaterialOrCategory = "结构",
                VisibleInSection = true,
                RecognitionMode = WallCoreRecognitionMode.BoundaryPair.ToString()
            },
            VerticalAnchorMode = WallVerticalAnchorMode.StructuralSlabFaces.ToString()
        };

        _templates.Add(template);
        TemplateListBox.SelectedItem = template;
    }

    private void RemoveTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (_currentTemplate == null)
        {
            return;
        }

        var result = MessageBox.Show(
            $"确定删除模板 {_currentTemplate.TemplateName} 吗？",
            "确认",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        _templates.Remove(_currentTemplate);
        _currentTemplate = null;
        TemplateListBox.SelectedIndex = _templates.Count > 0 ? 0 : -1;
        BindCurrentTemplate();
    }

    private void AddLeftLayer_Click(object sender, RoutedEventArgs e)
    {
        EnsureCurrentTemplate();
        _currentTemplate!.LeftLayers.Add(new WallLayerRuleDto
        {
            Name = $"左层 {_currentTemplate.LeftLayers.Count + 1}",
            Side = WallLayerSide.Left.ToString(),
            Order = _currentTemplate.LeftLayers.Count + 1,
            Thickness = 20,
            MaterialOrCategory = "附加层",
            VisibleInSection = true
        });
        RefreshLayerGrids();
    }

    private void RemoveLeftLayer_Click(object sender, RoutedEventArgs e)
    {
        EnsureCurrentTemplate();
        if (LeftLayerGrid.SelectedItem is not WallLayerRuleDto rule)
        {
            return;
        }

        _currentTemplate!.LeftLayers.Remove(rule);
        RefreshLayerGrids();
    }

    private void AddRightLayer_Click(object sender, RoutedEventArgs e)
    {
        EnsureCurrentTemplate();
        _currentTemplate!.RightLayers.Add(new WallLayerRuleDto
        {
            Name = $"右层 {_currentTemplate.RightLayers.Count + 1}",
            Side = WallLayerSide.Right.ToString(),
            Order = _currentTemplate.RightLayers.Count + 1,
            Thickness = 20,
            MaterialOrCategory = "附加层",
            VisibleInSection = true
        });
        RefreshLayerGrids();
    }

    private void RemoveRightLayer_Click(object sender, RoutedEventArgs e)
    {
        EnsureCurrentTemplate();
        if (RightLayerGrid.SelectedItem is not WallLayerRuleDto rule)
        {
            return;
        }

        _currentTemplate!.RightLayers.Remove(rule);
        RefreshLayerGrids();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        CommitCurrentTemplate();

        if (_templates.Count == 0)
        {
            MessageBox.Show("请至少保留一个墙体模板。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_templates.Any(template => string.IsNullOrWhiteSpace(template.TemplateId) || string.IsNullOrWhiteSpace(template.TemplateName)))
        {
            MessageBox.Show("模板名称和模板标识不能为空。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_templates.GroupBy(template => template.TemplateId, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
        {
            MessageBox.Show("模板标识必须唯一。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var catalogDto = new WallTemplateCatalogDto
        {
            Templates = _templates.ToList()
        };
        _templateCatalog.SaveAll(_templateCatalogMapper.ToDomain(catalogDto));
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void BindCurrentTemplate()
    {
        var enabled = _currentTemplate != null;
        TemplateNameBox.IsEnabled = enabled;
        TemplateIdBox.IsEnabled = enabled;
        CoreNameBox.IsEnabled = enabled;
        CoreThicknessBox.IsEnabled = enabled;
        CoreCategoryBox.IsEnabled = enabled;
        VerticalAnchorModeBox.IsEnabled = enabled;
        RecognitionModeBox.IsEnabled = enabled;
        LeftLayerGrid.IsEnabled = enabled;
        RightLayerGrid.IsEnabled = enabled;

        if (!enabled)
        {
            TemplateNameBox.Text = string.Empty;
            TemplateIdBox.Text = string.Empty;
            CoreNameBox.Text = string.Empty;
            CoreThicknessBox.Text = string.Empty;
            CoreCategoryBox.Text = string.Empty;
            VerticalAnchorModeBox.SelectedItem = null;
            RecognitionModeBox.SelectedItem = null;
            LeftLayerGrid.ItemsSource = null;
            RightLayerGrid.ItemsSource = null;
            return;
        }

        TemplateNameBox.Text = _currentTemplate!.TemplateName;
        TemplateIdBox.Text = _currentTemplate.TemplateId;
        CoreNameBox.Text = _currentTemplate.CoreRule.Name;
        CoreThicknessBox.Text = _currentTemplate.CoreRule.Thickness.ToString("F0");
        CoreCategoryBox.Text = _currentTemplate.CoreRule.MaterialOrCategory;
        VerticalAnchorModeBox.SelectedItem = ParseEnum(_currentTemplate.VerticalAnchorMode, new WallAssemblyTemplate().VerticalAnchorMode);
        RecognitionModeBox.SelectedItem = ParseEnum(_currentTemplate.CoreRule.RecognitionMode, new WallCoreRule().RecognitionMode);
        RefreshLayerGrids();
    }

    private void RefreshLayerGrids()
    {
        if (_currentTemplate == null)
        {
            LeftLayerGrid.ItemsSource = null;
            RightLayerGrid.ItemsSource = null;
            return;
        }

        LeftLayerGrid.ItemsSource = new ObservableCollection<WallLayerRuleDto>(_currentTemplate.LeftLayers.OrderBy(layer => layer.Order));
        RightLayerGrid.ItemsSource = new ObservableCollection<WallLayerRuleDto>(_currentTemplate.RightLayers.OrderBy(layer => layer.Order));
    }

    private void CommitCurrentTemplate()
    {
        if (_currentTemplate == null)
        {
            return;
        }

        _currentTemplate.TemplateName = TemplateNameBox.Text.Trim();
        _currentTemplate.TemplateId = TemplateIdBox.Text.Trim();
        _currentTemplate.CoreRule.Name = CoreNameBox.Text.Trim();
        _currentTemplate.CoreRule.MaterialOrCategory = CoreCategoryBox.Text.Trim();
        _currentTemplate.VerticalAnchorMode = (VerticalAnchorModeBox.SelectedItem is WallVerticalAnchorMode anchorMode
            ? anchorMode
            : WallVerticalAnchorMode.StructuralSlabFaces).ToString();
        _currentTemplate.CoreRule.RecognitionMode = (RecognitionModeBox.SelectedItem is WallCoreRecognitionMode mode
            ? mode
            : WallCoreRecognitionMode.BoundaryPair).ToString();

        if (double.TryParse(CoreThicknessBox.Text, out var coreThickness) && coreThickness > 0)
        {
            _currentTemplate.CoreRule.Thickness = coreThickness;
        }

        _currentTemplate.LeftLayers = ReadGridRules(LeftLayerGrid, WallLayerSide.Left.ToString());
        _currentTemplate.RightLayers = ReadGridRules(RightLayerGrid, WallLayerSide.Right.ToString());
    }

    private static List<WallLayerRuleDto> ReadGridRules(DataGrid grid, string side)
    {
        var rules = new List<WallLayerRuleDto>();
        foreach (var item in grid.Items.OfType<WallLayerRuleDto>())
        {
            item.Side = side;
            rules.Add(item);
        }

        return rules
            .Where(rule => !string.IsNullOrWhiteSpace(rule.Name) && rule.Thickness > 0)
            .OrderBy(rule => rule.Order)
            .ToList();
    }

    private void EnsureCurrentTemplate()
    {
        if (_currentTemplate != null)
        {
            return;
        }

        AddTemplate_Click(this, new RoutedEventArgs());
    }

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback)
        where TEnum : struct, Enum
        => Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed)
            ? parsed
            : fallback;
}
