using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.Plugin.UI;

public partial class SlabAssemblyTemplateManager : Window
{
    private readonly ISlabAssemblyTemplateCatalog _templateCatalog;
    private readonly ObservableCollection<SlabAssemblyTemplate> _templates = new();
    private SlabAssemblyTemplate? _currentTemplate;

    public string SelectedTemplateId { get; private set; } = string.Empty;

    public SlabAssemblyTemplateManager(ISlabAssemblyTemplateCatalog templateCatalog)
    {
        InitializeComponent();
        _templateCatalog = templateCatalog;

        WallJunctionModeBox.ItemsSource = Enum.GetValues<SlabWallJunctionMode>();
        LoadTemplates();
    }

    private void LoadTemplates()
    {
        _templates.Clear();
        foreach (var template in _templateCatalog.GetAllTemplates())
        {
            _templates.Add(CloneTemplate(template));
        }

        TemplateListBox.ItemsSource = _templates;
        TemplateListBox.SelectedIndex = _templates.Count > 0 ? 0 : -1;
    }

    private void TemplateListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        CommitCurrentTemplate();
        _currentTemplate = TemplateListBox.SelectedItem as SlabAssemblyTemplate;
        BindCurrentTemplate();
    }

    private void AddTemplate_Click(object sender, RoutedEventArgs e)
    {
        CommitCurrentTemplate();
        var template = new SlabAssemblyTemplate
        {
            TemplateId = $"slab-template-{_templates.Count + 1}",
            TemplateName = $"楼板模板 {_templates.Count + 1}",
            WallJunctionMode = SlabWallJunctionMode.StopAtWallFace,
            CoreRule = new SlabCoreRule
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
        _currentTemplate!.TopLayers.Add(new SlabLayerRule
        {
            Name = $"顶层 {_currentTemplate.TopLayers.Count + 1}",
            Side = SlabLayerSide.Top,
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
        if (TopLayerGrid.SelectedItem is not SlabLayerRule rule)
        {
            return;
        }

        _currentTemplate!.TopLayers.Remove(rule);
        RefreshLayerGrids();
    }

    private void AddBottomLayer_Click(object sender, RoutedEventArgs e)
    {
        EnsureCurrentTemplate();
        _currentTemplate!.BottomLayers.Add(new SlabLayerRule
        {
            Name = $"底层 {_currentTemplate.BottomLayers.Count + 1}",
            Side = SlabLayerSide.Bottom,
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
        if (BottomLayerGrid.SelectedItem is not SlabLayerRule rule)
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

        _templateCatalog.SaveAll(_templates.ToList());
        SelectedTemplateId = (TemplateListBox.SelectedItem as SlabAssemblyTemplate)?.TemplateId
                             ?? _currentTemplate?.TemplateId
                             ?? string.Empty;
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
        WallJunctionModeBox.SelectedItem = _currentTemplate.WallJunctionMode;
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

        TopLayerGrid.ItemsSource = new ObservableCollection<SlabLayerRule>(_currentTemplate.TopLayers.OrderBy(layer => layer.Order));
        BottomLayerGrid.ItemsSource = new ObservableCollection<SlabLayerRule>(_currentTemplate.BottomLayers.OrderBy(layer => layer.Order));
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
        _currentTemplate.WallJunctionMode = WallJunctionModeBox.SelectedItem is SlabWallJunctionMode mode
            ? mode
            : SlabWallJunctionMode.StopAtWallFace;

        if (double.TryParse(CoreThicknessBox.Text, out var coreThickness) && coreThickness > 0)
        {
            _currentTemplate.CoreRule.Thickness = coreThickness;
        }

        _currentTemplate.TopLayers = ReadGridRules(TopLayerGrid, SlabLayerSide.Top);
        _currentTemplate.BottomLayers = ReadGridRules(BottomLayerGrid, SlabLayerSide.Bottom);
    }

    private static List<SlabLayerRule> ReadGridRules(DataGrid grid, SlabLayerSide side)
    {
        var rules = new List<SlabLayerRule>();
        foreach (var item in grid.Items.OfType<SlabLayerRule>())
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

    private static SlabAssemblyTemplate CloneTemplate(SlabAssemblyTemplate template)
    {
        return new SlabAssemblyTemplate
        {
            TemplateId = template.TemplateId,
            TemplateName = template.TemplateName,
            WallJunctionMode = template.WallJunctionMode,
            CoreRule = new SlabCoreRule
            {
                Name = template.CoreRule.Name,
                Thickness = template.CoreRule.Thickness,
                MaterialOrCategory = template.CoreRule.MaterialOrCategory,
                VisibleInSection = template.CoreRule.VisibleInSection
            },
            TopLayers = template.TopLayers.Select(CloneRule).ToList(),
            BottomLayers = template.BottomLayers.Select(CloneRule).ToList()
        };
    }

    private static SlabLayerRule CloneRule(SlabLayerRule rule)
    {
        return new SlabLayerRule
        {
            Name = rule.Name,
            Side = rule.Side,
            Order = rule.Order,
            Thickness = rule.Thickness,
            MaterialOrCategory = rule.MaterialOrCategory,
            VisibleInSection = rule.VisibleInSection
        };
    }
}
