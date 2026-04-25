using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.Contracts.LayerMapping;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.Plugin.UI;

public partial class LayerMappingPanel : UserControl
{
    private ILayerMappingWorkspaceAssembler? _workspaceAssembler;
    private ILayerMappingApplyRequestMapper? _applyRequestMapper;
    private IWallAssemblyTemplateCatalog? _wallTemplateCatalog;
    private ISlabAssemblyTemplateCatalog? _slabTemplateCatalog;
    private readonly ObservableCollection<LayerInfo> _layers = new();
    private readonly ObservableCollection<ElementTypeDefinition> _types = new();
    private readonly ObservableCollection<LayerMapping> _mappings = new();
    private List<ElementTypeDefinition> _allTypes = new();
    private bool _initialized;

    public event EventHandler<ApplyLayerMappingsRequestedEventArgs>? ApplyRequested;

    public event EventHandler? CancelRequested;

    public LayerMappingPanel()
    {
        InitializeComponent();
    }

    public void Initialize(
        ILayerMappingWorkspaceAssembler workspaceAssembler,
        ILayerMappingApplyRequestMapper applyRequestMapper,
        IWallAssemblyTemplateCatalog wallTemplateCatalog,
        ISlabAssemblyTemplateCatalog slabTemplateCatalog)
    {
        if (_initialized)
        {
            throw new InvalidOperationException("LayerMappingPanel has already been initialized.");
        }

        _workspaceAssembler = workspaceAssembler;
        _applyRequestMapper = applyRequestMapper;
        _wallTemplateCatalog = wallTemplateCatalog;
        _slabTemplateCatalog = slabTemplateCatalog;
        _initialized = true;

        LoadData();
    }

    public void ReloadWorkspace()
    {
        EnsureInitialized();
        LoadData();
    }

    private void LoadData()
    {
        EnsureInitialized();

        var workspace = _workspaceAssembler!.Assemble();

        _layers.Clear();
        foreach (var layer in workspace.Layers)
        {
            _layers.Add(new LayerInfo { LayerName = layer.LayerName });
        }
        LayerListBox.ItemsSource = _layers;

        _types.Clear();
        _allTypes = workspace.ElementTypes
            .Select(static type => new ElementTypeDefinition
            {
                TypeId = type.TypeId,
                TypeName = type.TypeName,
                ShortcutKey = type.ShortcutKey,
                TargetLayerPrefix = type.TargetLayerPrefix,
                LayerColorIndex = type.ColorIndex,
                IsEnabled = type.IsEnabled
            })
            .ToList();
        foreach (var type in _allTypes)
        {
            _types.Add(type);
        }
        TypeListBox.ItemsSource = _types;

        _mappings.Clear();
        foreach (var mapping in workspace.Mappings)
        {
            _mappings.Add(new LayerMapping
            {
                LayerName = mapping.LayerName,
                TypeName = mapping.TypeName,
                TypeId = mapping.TypeId,
                TargetLayerPrefix = mapping.TargetLayerPrefix,
                ColorIndex = mapping.ColorIndex,
                TemplateId = mapping.TemplateId,
                TemplateName = mapping.TemplateName
            });
        }
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

    private void TypeListBox_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(LayerInfo)) is LayerInfo layer &&
            TypeListBox.SelectedItem is ElementTypeDefinition type)
        {
            AddMapping(layer.LayerName, type);
        }
    }

    private void TypeListBox_DragOver(object sender, DragEventArgs e)
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
            if (selectedTemplate.Cancelled)
            {
                return;
            }

            templateId = selectedTemplate.Template?.TemplateId;
            templateName = selectedTemplate.Template?.TemplateName;
        }
        else if (string.Equals(type.TypeId, "Slab", StringComparison.OrdinalIgnoreCase))
        {
            var selectedTemplate = ChooseSlabTemplate();
            if (selectedTemplate.Cancelled)
            {
                return;
            }

            templateId = selectedTemplate.Template?.TemplateId;
            templateName = selectedTemplate.Template?.TemplateName;
        }

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
        var owner = Window.GetWindow(this);
        var dialog = new Window
        {
            Title = "选择构件类型",
            Width = 300,
            Height = 400,
            WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            Owner = owner
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

        okButton.Click += (_, _) =>
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

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        EnsureInitialized();

        var requestDto = _applyRequestMapper!.ToRequest(
            _mappings.Select(static mapping => new LayerMappingDto
            {
                LayerName = mapping.LayerName,
                TypeName = mapping.TypeName,
                TypeId = mapping.TypeId,
                TargetLayerPrefix = mapping.TargetLayerPrefix,
                ColorIndex = mapping.ColorIndex,
                TemplateId = mapping.TemplateId,
                TemplateName = mapping.TemplateName
            }).ToArray(),
            applyToEntireDrawing: true);

        ApplyRequested?.Invoke(this, new ApplyLayerMappingsRequestedEventArgs(requestDto));
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }

    private void WallTemplatesButton_Click(object sender, RoutedEventArgs e)
    {
        EnsureInitialized();

        var owner = Window.GetWindow(this);
        var window = new WallAssemblyTemplateManager(_wallTemplateCatalog!)
        {
            Owner = owner,
            WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner
        };

        window.ShowDialog();
    }

    private void SlabTemplatesButton_Click(object sender, RoutedEventArgs e)
    {
        EnsureInitialized();

        var owner = Window.GetWindow(this);
        var window = new SlabAssemblyTemplateManager(_slabTemplateCatalog!)
        {
            Owner = owner,
            WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner
        };

        window.ShowDialog();
    }

    private TemplateSelectionResult<WallAssemblyTemplate> ChooseWallTemplate()
    {
        EnsureInitialized();

        var templates = _wallTemplateCatalog!.GetAllTemplates().ToList();
        return ChooseTemplate(
            "选择墙体模板",
            "（不绑定模板，沿用稳定模式）",
            templates,
            template => template.TemplateName);
    }

    private TemplateSelectionResult<SlabAssemblyTemplate> ChooseSlabTemplate()
    {
        EnsureInitialized();

        var templates = _slabTemplateCatalog!.GetAllTemplates().ToList();
        return ChooseTemplate(
            "选择楼板模板",
            "（不绑定模板，沿用稳定模式）",
            templates,
            template => template.TemplateName);
    }

    private TemplateSelectionResult<TTemplate> ChooseTemplate<TTemplate>(
        string title,
        string legacyLabel,
        IReadOnlyList<TTemplate> templates,
        Func<TTemplate, string> displayNameSelector)
        where TTemplate : class
    {
        var owner = Window.GetWindow(this);
        var dialog = new Window
        {
            Title = title,
            Width = 360,
            Height = 440,
            WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            Owner = owner
        };

        var options = new List<TemplateSelectionOption<TTemplate>>
        {
            new()
            {
                DisplayName = legacyLabel,
                Template = null
            }
        };
        options.AddRange(templates.Select(template => new TemplateSelectionOption<TTemplate>
        {
            DisplayName = displayNameSelector(template),
            Template = template
        }));

        var listBox = new ListBox { Margin = new Thickness(10) };
        foreach (var option in options)
        {
            listBox.Items.Add(option);
        }

        listBox.DisplayMemberPath = nameof(TemplateSelectionOption<TTemplate>.DisplayName);
        listBox.SelectedIndex = 0;

        var okButton = new Button
        {
            Content = "确定",
            Width = 80,
            Height = 28,
            Margin = new Thickness(10),
            HorizontalAlignment = HorizontalAlignment.Right
        };

        TemplateSelectionResult<TTemplate> selection = new() { Cancelled = true };
        okButton.Click += (_, _) =>
        {
            selection = new TemplateSelectionResult<TTemplate>
            {
                Cancelled = false,
                Template = (listBox.SelectedItem as TemplateSelectionOption<TTemplate>)?.Template
            };
            dialog.DialogResult = true;
            dialog.Close();
        };

        var cancelButton = new Button
        {
            Content = "取消",
            Width = 80,
            Height = 28,
            Margin = new Thickness(10, 0, 10, 10),
            HorizontalAlignment = HorizontalAlignment.Right,
            IsCancel = true
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        buttons.Children.Add(okButton);
        buttons.Children.Add(cancelButton);

        var panel = new DockPanel();
        DockPanel.SetDock(buttons, Dock.Bottom);
        panel.Children.Add(buttons);
        panel.Children.Add(listBox);

        dialog.Content = panel;
        if (dialog.ShowDialog() != true)
        {
            return new TemplateSelectionResult<TTemplate> { Cancelled = true };
        }

        return selection;
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("LayerMappingPanel has not been initialized.");
        }
    }
}

public class LayerInfo
{
    public string LayerName { get; set; } = string.Empty;
    public System.Windows.Media.Brush ColorBrush => System.Windows.Media.Brushes.Gray;
}

public class LayerMapping
{
    public string LayerName { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public string TypeId { get; set; } = string.Empty;
    public string TargetLayerPrefix { get; set; } = string.Empty;
    public short ColorIndex { get; set; }
    public string? TemplateId { get; set; }
    public string? TemplateName { get; set; }

    public string TemplateDisplay => string.IsNullOrWhiteSpace(TemplateName) ? "—" : TemplateName;

    public string ModeText => string.IsNullOrWhiteSpace(TemplateName) ? "稳定模式" : "模板模式";

    public string DisplayText =>
        string.IsNullOrWhiteSpace(TemplateName)
            ? $"{LayerName} → {TypeName}"
            : $"{LayerName} → {TypeName} [{TemplateName}]";
}

internal sealed class TemplateSelectionOption<TTemplate>
    where TTemplate : class
{
    public string DisplayName { get; init; } = string.Empty;

    public TTemplate? Template { get; init; }
}

internal sealed class TemplateSelectionResult<TTemplate>
    where TTemplate : class
{
    public bool Cancelled { get; init; }

    public TTemplate? Template { get; init; }
}

public sealed class ApplyLayerMappingsRequestedEventArgs : EventArgs
{
    public ApplyLayerMappingsRequestedEventArgs(ApplyLayerMappingsRequestDto request)
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
    }

    public ApplyLayerMappingsRequestDto Request { get; }
}
