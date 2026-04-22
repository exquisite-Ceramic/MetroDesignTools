using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.UseCases;
using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.Foundation.Cad.Services;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.Plugin.UI;

/// <summary>
/// 图层映射管理器窗口
/// </summary>
public partial class LayerMappingManager : Window
{
    private readonly ILayerService _layerService;
    private readonly IElementTypeCatalog _typeCatalog;
    private readonly IWallAssemblyTemplateCatalog _wallTemplateCatalog;
    private readonly ISlabAssemblyTemplateCatalog _slabTemplateCatalog;
    private readonly IElementConversionUseCase _conversionUseCase;
    private readonly ObservableCollection<LayerInfo> _layers = new();
    private readonly ObservableCollection<ElementTypeDefinition> _types = new();
    private readonly ObservableCollection<LayerMapping> _mappings = new();
    private List<ElementTypeDefinition> _allTypes = new();

    public LayerMappingManager(
        ILayerService layerService,
        IElementTypeCatalog typeCatalog,
        IWallAssemblyTemplateCatalog wallTemplateCatalog,
        ISlabAssemblyTemplateCatalog slabTemplateCatalog,
        IElementConversionUseCase conversionUseCase)
    {
        InitializeComponent();
        _layerService = layerService;
        _typeCatalog = typeCatalog;
        _wallTemplateCatalog = wallTemplateCatalog;
        _slabTemplateCatalog = slabTemplateCatalog;
        _conversionUseCase = conversionUseCase;

        LoadData();
    }

    private void LoadData()
    {
        // 加载图层
        _layers.Clear();
        var layerNames = _layerService.GetAllLayerNames();
        foreach (var name in layerNames.OrderBy(n => n))
        {
            _layers.Add(new LayerInfo { LayerName = name });
        }
        LayerListBox.ItemsSource = _layers;

        // 加载构件类型
        _types.Clear();
        _allTypes = _typeCatalog.GetAllTypes().ToList();
        foreach (var type in _allTypes.Where(t => t.IsEnabled))
        {
            _types.Add(type);
        }
        TypeListBox.ItemsSource = _types;

        // 清空映射
        _mappings.Clear();
        MappingListBox.ItemsSource = _mappings;
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var searchText = SearchTextBox.Text.ToLower();
        var filtered = _layers.Where(l => 
            string.IsNullOrEmpty(searchText) || 
            l.LayerName.ToLower().Contains(searchText)).ToList();
        LayerListBox.ItemsSource = filtered;
    }

    private void LayerListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (LayerListBox.SelectedItem is LayerInfo layer)
        {
            ShowTypeSelectionDialog(layer);
        }
    }

    private void MapButton_Click(object sender, RoutedEventArgs e)
    {
        if (LayerListBox.SelectedItem is LayerInfo layer && 
            TypeListBox.SelectedItem is ElementTypeDefinition type)
        {
            AddMapping(layer.LayerName, type);
        }
    }

    private void UnmapButton_Click(object sender, RoutedEventArgs e)
    {
        if (MappingListBox.SelectedItem is LayerMapping mapping)
        {
            _mappings.Remove(mapping);
        }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        LoadData();
    }

    private void TypeListBox_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetData(typeof(LayerInfo)) is LayerInfo layer &&
            TypeListBox.SelectedItem is ElementTypeDefinition type)
        {
            AddMapping(layer.LayerName, type);
        }
    }

    private void TypeListBox_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(LayerInfo)) 
            ? DragDropEffects.Copy 
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void AddMapping(string layerName, ElementTypeDefinition type)
    {
        string? templateId = null;
        string? templateName = null;
        if (string.Equals(type.TypeId, "Wall", StringComparison.OrdinalIgnoreCase))
        {
            var selectedTemplate = ChooseWallTemplate();
            if (selectedTemplate == null)
            {
                return;
            }

            templateId = selectedTemplate.TemplateId;
            templateName = selectedTemplate.TemplateName;
        }
        else if (string.Equals(type.TypeId, "Slab", StringComparison.OrdinalIgnoreCase))
        {
            var selectedTemplate = ChooseSlabTemplate();
            if (selectedTemplate == null)
            {
                return;
            }

            templateId = selectedTemplate.TemplateId;
            templateName = selectedTemplate.TemplateName;
        }

        // 检查是否已存在映射
        var existing = _mappings.FirstOrDefault(m => m.LayerName == layerName);
        if (existing != null)
        {
            _mappings.Remove(existing);
        }

        _mappings.Add(new LayerMapping 
        { 
            LayerName = layerName, 
            TypeName = type.TypeName,
            TypeId = type.TypeId,
            TargetLayerPrefix = type.TargetLayerPrefix,
            ColorIndex = type.LayerColorIndex,
            TemplateId = templateId,
            TemplateName = templateName
        });
    }

    private void ShowTypeSelectionDialog(LayerInfo layer)
    {
        // 简化实现：使用快捷键选择
        var dialog = new Window
        {
            Title = "选择构件类型",
            Width = 300,
            Height = 400,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this
        };

        var listBox = new ListBox { Margin = new Thickness(10) };
        foreach (var type in _allTypes.Where(t => t.IsEnabled))
        {
            listBox.Items.Add($"{type.TypeName} ({type.ShortcutKey})");
        }

        var okButton = new Button 
        { 
            Content = "确定", 
            Width = 80, 
            Height = 25,
            Margin = new Thickness(10),
            HorizontalAlignment = HorizontalAlignment.Right
        };

        okButton.Click += (s, e) =>
        {
            if (listBox.SelectedIndex >= 0)
            {
                var selectedType = _allTypes.Where(t => t.IsEnabled).ElementAt(listBox.SelectedIndex);
                AddMapping(layer.LayerName, selectedType);
            }
            dialog.DialogResult = true;
            dialog.Close();
        };

        var panel = new StackPanel();
        panel.Children.Add(listBox);
        panel.Children.Add(okButton);
        dialog.Content = panel;

        dialog.ShowDialog();
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_mappings.Count == 0)
        {
            MessageBox.Show("请先建立图层映射关系。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show(
            $"即将应用 {_mappings.Count} 个图层映射，是否继续？\n此操作将修改图元图层并备份原始属性。",
            "确认", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        ApplyMappings();
        DialogResult = true;
        Close();
    }

    private void ApplyMappings()
    {
        var result = _conversionUseCase.ApplyMappings(new ElementConversionApplyRequest
        {
            ApplyToEntireDrawing = true,
            LayerMappings = _mappings.Select(static mapping => new LayerTypeAssignment
            {
                SourceLayerName = mapping.LayerName,
                TypeId = mapping.TypeId,
                TemplateId = mapping.TemplateId
            }).ToList()
        });

        if (!result.Success)
        {
            MessageBox.Show(result.ErrorMessage ?? "图层映射应用失败。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        MessageBox.Show(
            $"已应用 {result.MappedLayerCount} 个图层映射，共转换 {result.ConvertedCount} 个实体。",
            "完成",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void WallTemplatesButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new WallAssemblyTemplateManager(_wallTemplateCatalog)
        {
            Owner = this
        };

        window.ShowDialog();
    }

    private void SlabTemplatesButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new SlabAssemblyTemplateManager(_slabTemplateCatalog)
        {
            Owner = this
        };

        window.ShowDialog();
    }

    private WallAssemblyTemplate? ChooseWallTemplate()
    {
        var templates = _wallTemplateCatalog.GetAllTemplates().ToList();
        if (templates.Count == 0)
        {
            MessageBox.Show("请先维护至少一个墙体模板。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return null;
        }

        var dialog = new Window
        {
            Title = "选择墙体模板",
            Width = 320,
            Height = 420,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this
        };

        var listBox = new ListBox { Margin = new Thickness(10) };
        foreach (var template in templates)
        {
            listBox.Items.Add(template);
        }

        listBox.DisplayMemberPath = nameof(WallAssemblyTemplate.TemplateName);

        var okButton = new Button
        {
            Content = "确定",
            Width = 80,
            Height = 28,
            Margin = new Thickness(10),
            HorizontalAlignment = HorizontalAlignment.Right
        };

        WallAssemblyTemplate? selectedTemplate = null;
        okButton.Click += (_, _) =>
        {
            selectedTemplate = listBox.SelectedItem as WallAssemblyTemplate;
            dialog.DialogResult = selectedTemplate != null;
            dialog.Close();
        };

        var panel = new DockPanel();
        DockPanel.SetDock(okButton, Dock.Bottom);
        panel.Children.Add(okButton);
        panel.Children.Add(listBox);

        dialog.Content = panel;
        return dialog.ShowDialog() == true ? selectedTemplate : null;
    }

    private SlabAssemblyTemplate? ChooseSlabTemplate()
    {
        var templates = _slabTemplateCatalog.GetAllTemplates().ToList();
        if (templates.Count == 0)
        {
            MessageBox.Show("请先维护至少一个楼板模板。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return null;
        }

        var dialog = new Window
        {
            Title = "选择楼板模板",
            Width = 320,
            Height = 420,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this
        };

        var listBox = new ListBox { Margin = new Thickness(10) };
        foreach (var template in templates)
        {
            listBox.Items.Add(template);
        }

        listBox.DisplayMemberPath = nameof(SlabAssemblyTemplate.TemplateName);

        var okButton = new Button
        {
            Content = "确定",
            Width = 80,
            Height = 28,
            Margin = new Thickness(10),
            HorizontalAlignment = HorizontalAlignment.Right
        };

        SlabAssemblyTemplate? selectedTemplate = null;
        okButton.Click += (_, _) =>
        {
            selectedTemplate = listBox.SelectedItem as SlabAssemblyTemplate;
            dialog.DialogResult = selectedTemplate != null;
            dialog.Close();
        };

        var panel = new DockPanel();
        DockPanel.SetDock(okButton, Dock.Bottom);
        panel.Children.Add(okButton);
        panel.Children.Add(listBox);

        dialog.Content = panel;
        return dialog.ShowDialog() == true ? selectedTemplate : null;
    }
}

/// <summary>
/// 图层信息
/// </summary>
public class LayerInfo
{
    public string LayerName { get; set; } = string.Empty;
    public System.Windows.Media.Brush ColorBrush => System.Windows.Media.Brushes.Gray;
}

/// <summary>
/// 图层映射
/// </summary>
public class LayerMapping
{
    public string LayerName { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public string TypeId { get; set; } = string.Empty;
    public string TargetLayerPrefix { get; set; } = string.Empty;
    public short ColorIndex { get; set; }
    public string? TemplateId { get; set; }
    public string? TemplateName { get; set; }
}
