using MetroToolKits.SectionGenerator.Infrastructure.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.Foundation.Cad.Services;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace MetroToolKits.SectionGenerator.Plugin.UI;

/// <summary>
/// 图层映射管理器窗口
/// </summary>
public partial class LayerMappingManager : Window
{
    private readonly ILayerService _layerService;
    private readonly ElementConversionBackupService _backupService;
    private readonly ElementTypeLoader _typeLoader;
    private readonly ObservableCollection<LayerInfo> _layers = new();
    private readonly ObservableCollection<ElementTypeDefinition> _types = new();
    private readonly ObservableCollection<LayerMapping> _mappings = new();
    private List<ElementTypeDefinition> _allTypes = new();

    public LayerMappingManager(ILayerService layerService, ElementConversionBackupService backupService, ElementTypeLoader typeLoader)
    {
        InitializeComponent();
        _layerService = layerService;
        _backupService = backupService;
        _typeLoader = typeLoader;

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
        _allTypes = _typeLoader.Load().ToList();
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
            ColorIndex = type.LayerColorIndex
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
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null) return;

        using var lockDoc = doc.LockDocument();
        var db = doc.Database;

        using var tr = db.TransactionManager.StartTransaction();
        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

        foreach (var mapping in _mappings)
        {
            var targetLayer = $"{mapping.TargetLayerPrefix}_{mapping.LayerName}";
            var layerId = _layerService.GetOrCreateLayer(targetLayer);
            _layerService.SetLayerColor(targetLayer, mapping.ColorIndex);

            // 遍历模型空间中的实体
            btr.UpgradeOpen();
            foreach (var objId in btr)
            {
                var entity = (Entity)tr.GetObject(objId, OpenMode.ForRead);
                if (entity.Layer == mapping.LayerName)
                {
                    entity.UpgradeOpen();
                    _backupService.BackupEntity(entity, mapping.TypeId);
                    entity.Layer = targetLayer;
                }
            }
        }

        tr.Commit();
        doc.Editor.WriteMessage($"\n已应用 {_mappings.Count} 个图层映射。");
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
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
}

