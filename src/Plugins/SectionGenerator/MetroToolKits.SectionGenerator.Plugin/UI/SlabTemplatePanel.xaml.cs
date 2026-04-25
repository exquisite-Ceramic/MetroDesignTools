using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.Contracts.Templates;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.Plugin.UI;

public partial class SlabTemplatePanel : UserControl
{
    private readonly ObservableCollection<SlabAssemblyTemplateDto> _templates = new();
    private ISlabAssemblyTemplateCatalog? _templateCatalog;
    private ISlabTemplateCatalogMapper? _templateCatalogMapper;
    private SlabAssemblyTemplateDto? _currentTemplate;
    private bool _initialized;

    public event EventHandler? SaveCompleted;

    public event EventHandler? CancelRequested;

    public string SelectedTemplateId { get; private set; } = string.Empty;

    public SlabTemplatePanel()
    {
        InitializeComponent();
    }

    public void Initialize(
        ISlabAssemblyTemplateCatalog templateCatalog,
        ISlabTemplateCatalogMapper templateCatalogMapper)
    {
        if (_initialized)
        {
            throw new InvalidOperationException("SlabTemplatePanel has already been initialized.");
        }

        _templateCatalog = templateCatalog;
        _templateCatalogMapper = templateCatalogMapper;
        _initialized = true;

        WallJunctionModeBox.ItemsSource = Enum.GetValues<SlabWallJunctionMode>();
        LoadTemplates();
    }

    private void LoadTemplates()
    {
        EnsureInitialized();

        _templates.Clear();
        var catalogDto = _templateCatalogMapper!.ToDto(_templateCatalog!.GetAllTemplates());
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
        _currentTemplate = TemplateListBox.SelectedItem as SlabAssemblyTemplateDto;
        BindCurrentTemplate();
    }

    private void AddTemplate_Click(object sender, RoutedEventArgs e)
    {
        CommitCurrentTemplate();
        var template = new SlabAssemblyTemplateDto
        {
            TemplateId = $"slab-template-{_templates.Count + 1}",
            TemplateName = $"楼板模板 {_templates.Count + 1}",
            WallJunctionMode = SlabWallJunctionMode.StopAtWallFace.ToString(),
            CoreRule = new SlabCoreRuleDto
            {
                Name = "结构板",
                Thickness = 120,
                MaterialOrCategory = "结构",
                VisibleInSection = true
            }
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

    private void AddTopLayer_Click(object sender, RoutedEventArgs e)
    {
        EnsureCurrentTemplate();
        _currentTemplate!.TopLayers.Add(new SlabLayerRuleDto
        {
            Name = $"顶层 {_currentTemplate.TopLayers.Count + 1}",
            Side = SlabLayerSide.Top.ToString(),
            Order = _currentTemplate.TopLayers.Count + 1,
            Thickness = 20,
            MaterialOrCategory = "装修",
            VisibleInSection = true
        });
        RefreshLayerGrids();
    }

    private void RemoveTopLayer_Click(object sender, RoutedEventArgs e)
    {
        EnsureCurrentTemplate();
        if (TopLayerGrid.SelectedItem is not SlabLayerRuleDto rule)
        {
            return;
        }

        _currentTemplate!.TopLayers.Remove(rule);
        RefreshLayerGrids();
    }

    private void AddBottomLayer_Click(object sender, RoutedEventArgs e)
    {
        EnsureCurrentTemplate();
        _currentTemplate!.BottomLayers.Add(new SlabLayerRuleDto
        {
            Name = $"底层 {_currentTemplate.BottomLayers.Count + 1}",
            Side = SlabLayerSide.Bottom.ToString(),
            Order = _currentTemplate.BottomLayers.Count + 1,
            Thickness = 20,
            MaterialOrCategory = "装修",
            VisibleInSection = true
        });
        RefreshLayerGrids();
    }

    private void RemoveBottomLayer_Click(object sender, RoutedEventArgs e)
    {
        EnsureCurrentTemplate();
        if (BottomLayerGrid.SelectedItem is not SlabLayerRuleDto rule)
        {
            return;
        }

        _currentTemplate!.BottomLayers.Remove(rule);
        RefreshLayerGrids();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        CommitCurrentTemplate();

        if (_templates.Count == 0)
        {
            MessageBox.Show("请至少保留一个楼板模板。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
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

        var catalogDto = new SlabTemplateCatalogDto
        {
            Templates = _templates.ToList()
        };
        _templateCatalog!.SaveAll(_templateCatalogMapper!.ToDomain(catalogDto));
        SelectedTemplateId = (TemplateListBox.SelectedItem as SlabAssemblyTemplateDto)?.TemplateId
                             ?? _currentTemplate?.TemplateId
                             ?? string.Empty;
        SaveCompleted?.Invoke(this, EventArgs.Empty);
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }

    private void BindCurrentTemplate()
    {
        var enabled = _currentTemplate != null;
        TemplateNameBox.IsEnabled = enabled;
        TemplateIdBox.IsEnabled = enabled;
        CoreNameBox.IsEnabled = enabled;
        CoreThicknessBox.IsEnabled = enabled;
        CoreCategoryBox.IsEnabled = enabled;
        WallJunctionModeBox.IsEnabled = enabled;
        TopLayerGrid.IsEnabled = enabled;
        BottomLayerGrid.IsEnabled = enabled;

        if (!enabled)
        {
            TemplateNameBox.Text = string.Empty;
            TemplateIdBox.Text = string.Empty;
            CoreNameBox.Text = string.Empty;
            CoreThicknessBox.Text = string.Empty;
            CoreCategoryBox.Text = string.Empty;
            WallJunctionModeBox.SelectedItem = null;
            TopLayerGrid.ItemsSource = null;
            BottomLayerGrid.ItemsSource = null;
            return;
        }

        TemplateNameBox.Text = _currentTemplate!.TemplateName;
        TemplateIdBox.Text = _currentTemplate.TemplateId;
        CoreNameBox.Text = _currentTemplate.CoreRule.Name;
        CoreThicknessBox.Text = _currentTemplate.CoreRule.Thickness.ToString("F0");
        CoreCategoryBox.Text = _currentTemplate.CoreRule.MaterialOrCategory;
        WallJunctionModeBox.SelectedItem = ParseEnum(_currentTemplate.WallJunctionMode, new SlabAssemblyTemplate().WallJunctionMode);
        RefreshLayerGrids();
    }

    private void RefreshLayerGrids()
    {
        if (_currentTemplate == null)
        {
            TopLayerGrid.ItemsSource = null;
            BottomLayerGrid.ItemsSource = null;
            return;
        }

        TopLayerGrid.ItemsSource = new ObservableCollection<SlabLayerRuleDto>(_currentTemplate.TopLayers.OrderBy(layer => layer.Order));
        BottomLayerGrid.ItemsSource = new ObservableCollection<SlabLayerRuleDto>(_currentTemplate.BottomLayers.OrderBy(layer => layer.Order));
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
        _currentTemplate.WallJunctionMode = (WallJunctionModeBox.SelectedItem is SlabWallJunctionMode mode
            ? mode
            : SlabWallJunctionMode.StopAtWallFace).ToString();

        if (double.TryParse(CoreThicknessBox.Text, out var coreThickness) && coreThickness > 0)
        {
            _currentTemplate.CoreRule.Thickness = coreThickness;
        }

        _currentTemplate.TopLayers = ReadGridRules(TopLayerGrid, SlabLayerSide.Top.ToString());
        _currentTemplate.BottomLayers = ReadGridRules(BottomLayerGrid, SlabLayerSide.Bottom.ToString());
    }

    private static List<SlabLayerRuleDto> ReadGridRules(DataGrid grid, string side)
    {
        var rules = new List<SlabLayerRuleDto>();
        foreach (var item in grid.Items.OfType<SlabLayerRuleDto>())
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

    private void EnsureInitialized()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("SlabTemplatePanel has not been initialized.");
        }
    }

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback)
        where TEnum : struct, Enum
        => Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed)
            ? parsed
            : fallback;
}
