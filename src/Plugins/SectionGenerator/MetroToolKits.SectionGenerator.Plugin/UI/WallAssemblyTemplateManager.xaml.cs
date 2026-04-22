using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.SectionGenerator.App.Abstractions;

namespace MetroToolKits.SectionGenerator.Plugin.UI;

public partial class WallAssemblyTemplateManager : Window
{
    private readonly IWallAssemblyTemplateCatalog _templateCatalog;
    private readonly ObservableCollection<WallAssemblyTemplate> _templates = new();
    private WallAssemblyTemplate? _currentTemplate;

    public WallAssemblyTemplateManager(IWallAssemblyTemplateCatalog templateCatalog)
    {
        InitializeComponent();
        _templateCatalog = templateCatalog;

        RecognitionModeBox.ItemsSource = Enum.GetValues<WallCoreRecognitionMode>();
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
        _currentTemplate = TemplateListBox.SelectedItem as WallAssemblyTemplate;
        BindCurrentTemplate();
    }

    private void AddTemplate_Click(object sender, RoutedEventArgs e)
    {
        CommitCurrentTemplate();
        var template = new WallAssemblyTemplate
        {
            TemplateId = $"wall-template-{_templates.Count + 1}",
            TemplateName = $"墙体模板 {_templates.Count + 1}",
            CoreRule = new WallCoreRule
            {
                Name = "结构芯",
                Thickness = 200,
                MaterialOrCategory = "结构",
                VisibleInSection = true,
                RecognitionMode = WallCoreRecognitionMode.BoundaryPair
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

    private void AddLeftLayer_Click(object sender, RoutedEventArgs e)
    {
        EnsureCurrentTemplate();
        _currentTemplate!.LeftLayers.Add(new WallLayerRule
        {
            Name = $"左层 {_currentTemplate.LeftLayers.Count + 1}",
            Side = WallLayerSide.Left,
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
        if (LeftLayerGrid.SelectedItem is not WallLayerRule rule)
        {
            return;
        }

        _currentTemplate!.LeftLayers.Remove(rule);
        RefreshLayerGrids();
    }

    private void AddRightLayer_Click(object sender, RoutedEventArgs e)
    {
        EnsureCurrentTemplate();
        _currentTemplate!.RightLayers.Add(new WallLayerRule
        {
            Name = $"右层 {_currentTemplate.RightLayers.Count + 1}",
            Side = WallLayerSide.Right,
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
        if (RightLayerGrid.SelectedItem is not WallLayerRule rule)
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

        _templateCatalog.SaveAll(_templates.ToList());
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
        RecognitionModeBox.SelectedItem = _currentTemplate.CoreRule.RecognitionMode;
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

        LeftLayerGrid.ItemsSource = new ObservableCollection<WallLayerRule>(_currentTemplate.LeftLayers.OrderBy(layer => layer.Order));
        RightLayerGrid.ItemsSource = new ObservableCollection<WallLayerRule>(_currentTemplate.RightLayers.OrderBy(layer => layer.Order));
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
        _currentTemplate.CoreRule.RecognitionMode = RecognitionModeBox.SelectedItem is WallCoreRecognitionMode mode
            ? mode
            : WallCoreRecognitionMode.BoundaryPair;

        if (double.TryParse(CoreThicknessBox.Text, out var coreThickness) && coreThickness > 0)
        {
            _currentTemplate.CoreRule.Thickness = coreThickness;
        }

        _currentTemplate.LeftLayers = ReadGridRules(LeftLayerGrid, WallLayerSide.Left);
        _currentTemplate.RightLayers = ReadGridRules(RightLayerGrid, WallLayerSide.Right);
    }

    private static List<WallLayerRule> ReadGridRules(DataGrid grid, WallLayerSide side)
    {
        var rules = new List<WallLayerRule>();
        foreach (var item in grid.Items.OfType<WallLayerRule>())
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

    private static WallAssemblyTemplate CloneTemplate(WallAssemblyTemplate template)
    {
        return new WallAssemblyTemplate
        {
            TemplateId = template.TemplateId,
            TemplateName = template.TemplateName,
            CoreRule = new WallCoreRule
            {
                Name = template.CoreRule.Name,
                Thickness = template.CoreRule.Thickness,
                MaterialOrCategory = template.CoreRule.MaterialOrCategory,
                VisibleInSection = template.CoreRule.VisibleInSection,
                RecognitionMode = template.CoreRule.RecognitionMode
            },
            LeftLayers = template.LeftLayers
                .Select(CloneRule)
                .ToList(),
            RightLayers = template.RightLayers
                .Select(CloneRule)
                .ToList()
        };
    }

    private static WallLayerRule CloneRule(WallLayerRule rule)
    {
        return new WallLayerRule
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
