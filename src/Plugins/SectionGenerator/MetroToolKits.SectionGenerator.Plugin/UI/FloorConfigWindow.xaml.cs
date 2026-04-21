using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.Plugin.UI;

/// <summary>
/// 楼层配置管理窗口
/// </summary>
public partial class FloorConfigWindow : Window
{
    private readonly ILogger<FloorConfigWindow> _logger;
    private readonly ObservableCollection<FloorConfig> _floors = new();
    private SectionConfig _config = new();
    private FloorConfig? _currentFloor;

    public SectionConfig CurrentConfig => _config;

    public FloorConfigWindow(
        SectionConfig config,
        ILogger<FloorConfigWindow> logger)
    {
        InitializeComponent();
        _config = config;
        _logger = logger;

        FloorListBox.ItemsSource = _floors;
        BaseFloorComboBox.ItemsSource = _floors;
        LoadConfig();
    }

    // ── 加载 ──────────────────────────────────────────────────────────────────

    private void LoadConfig()
    {
        _floors.Clear();
        foreach (var f in _config.Floors) _floors.Add(f);

        GlobalSlopeCheck.IsChecked     = _config.GlobalSlopeEnabled;
        GlobalSlopeValueBox.Text       = (_config.GlobalSlopeValue * 100).ToString("F2");
        GlobalSlopeTargetBox.SelectedIndex = _config.GlobalSlopeTarget == "FinishLayer" ? 1 : 0;
        BaseFloorComboBox.SelectedItem = _floors.FirstOrDefault(f =>
            string.Equals(f.Name, _config.AlignmentBaseFloorName, StringComparison.OrdinalIgnoreCase));

        if (BaseFloorComboBox.SelectedItem == null && _floors.Count == 1)
        {
            BaseFloorComboBox.SelectedItem = _floors[0];
        }

        _logger.LogDebug("楼层配置窗口加载，楼层数: {Count}", _floors.Count);
    }

    // ── 楼层列表操作 ──────────────────────────────────────────────────────────

    private void FloorListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FloorListBox.SelectedItem is FloorConfig floor)
        {
            SaveCurrentEdits();
            _currentFloor = floor;
            BindFloorToUI(floor);
            EditPanel.IsEnabled = true;
        }
        else
        {
            EditPanel.IsEnabled = false;
        }
    }

    private void AddFloor_Click(object sender, RoutedEventArgs e)
    {
        var newFloor = new FloorConfig
        {
            Name                = $"F{_floors.Count + 1}",
            Height              = 3000,
            BottomSlabThickness = 800,
            TopSlabThickness    = 600,
            FinishThickness     = 120
        };
        _floors.Add(newFloor);
        if (_floors.Count == 1)
        {
            BaseFloorComboBox.SelectedItem = newFloor;
        }
        FloorListBox.SelectedItem = newFloor;
        _logger.LogInformation("添加楼层: {FloorName}", newFloor.Name);
    }

    private void DeleteFloor_Click(object sender, RoutedEventArgs e)
    {
        if (_currentFloor == null) return;
        var name = _currentFloor.Name;
        _floors.Remove(_currentFloor);
        if (BaseFloorComboBox.SelectedItem is FloorConfig selectedBase &&
            string.Equals(selectedBase.Name, name, StringComparison.OrdinalIgnoreCase))
        {
            BaseFloorComboBox.SelectedItem = _floors.FirstOrDefault();
        }
        _currentFloor = null;
        EditPanel.IsEnabled = false;
        _logger.LogInformation("删除楼层: {FloorName}", name);
    }

    private void MoveUp_Click(object sender, RoutedEventArgs e)
    {
        var idx = FloorListBox.SelectedIndex;
        if (idx <= 0) return;
        _floors.Move(idx, idx - 1);
    }

    private void MoveDown_Click(object sender, RoutedEventArgs e)
    {
        var idx = FloorListBox.SelectedIndex;
        if (idx < 0 || idx >= _floors.Count - 1) return;
        _floors.Move(idx, idx + 1);
    }

    // ── 参数编辑 ──────────────────────────────────────────────────────────────

    private void BindFloorToUI(FloorConfig floor)
    {
        NameBox.Text       = floor.Name;
        HeightBox.Text     = floor.Height.ToString("F0");
        BottomSlabBox.Text = floor.BottomSlabThickness.ToString("F0");
        TopSlabBox.Text    = floor.TopSlabThickness.ToString("F0");
        FinishBox.Text     = floor.FinishThickness.ToString("F0");
        SlopeCheck.IsChecked = floor.HasSlope;
        SlopeValueBox.Text = (floor.SlopeValue * 100).ToString("F2");
        SlopeValueBox.IsEnabled = floor.HasSlope;

        var pointCount = floor.AlignmentPoints.Count;
        AlignmentLabel.Content = BaseFloorComboBox.SelectedItem is FloorConfig baseFloor &&
                                 string.Equals(baseFloor.Name, floor.Name, StringComparison.OrdinalIgnoreCase)
            ? "基准点:"
            : "本层对齐点:";
        AlignmentStatus.Text = pointCount >= 3 ? "已设置（3点）" : "未设置";
        AlignmentStatus.Foreground = pointCount >= 3
            ? System.Windows.Media.Brushes.Green
            : System.Windows.Media.Brushes.Gray;
    }

    private void SaveCurrentEdits()
    {
        if (_currentFloor == null) return;

        _currentFloor.Name = NameBox.Text.Trim();
        if (double.TryParse(HeightBox.Text, out var h))     _currentFloor.Height = h;
        if (double.TryParse(BottomSlabBox.Text, out var bs)) _currentFloor.BottomSlabThickness = bs;
        if (double.TryParse(TopSlabBox.Text, out var ts))   _currentFloor.TopSlabThickness = ts;
        if (double.TryParse(FinishBox.Text, out var ft))    _currentFloor.FinishThickness = ft;
        _currentFloor.HasSlope = SlopeCheck.IsChecked == true;
        if (double.TryParse(SlopeValueBox.Text, out var sv)) _currentFloor.SlopeValue = sv / 100.0;
    }

    private void SlopeCheck_Changed(object sender, RoutedEventArgs e)
        => SlopeValueBox.IsEnabled = SlopeCheck.IsChecked == true;

    private void BaseFloorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_currentFloor != null)
        {
            BindFloorToUI(_currentFloor);
        }
    }

    // ── 对齐点拾取 ────────────────────────────────────────────────────────────

    private void PickAlignment_Click(object sender, RoutedEventArgs e)
    {
        if (_currentFloor == null) return;
        SaveCurrentEdits();

        // 关闭窗口，在 CAD 中拾取点
        DialogResult = null;
        Tag = ("PickAlignment", _currentFloor);
        Close();
    }

    private void ClearAlignment_Click(object sender, RoutedEventArgs e)
    {
        if (_currentFloor == null) return;
        _currentFloor.AlignmentPoints.Clear();
        AlignmentStatus.Text = "未设置";
        AlignmentStatus.Foreground = System.Windows.Media.Brushes.Gray;
        _logger.LogInformation("清除楼层 {FloorName} 对齐点", _currentFloor.Name);
    }

    // ── 保存/取消 ─────────────────────────────────────────────────────────────

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrentEdits();

        _config.Floors = _floors.ToList();
        _config.GlobalSlopeEnabled = GlobalSlopeCheck.IsChecked == true;
        if (double.TryParse(GlobalSlopeValueBox.Text, out var gsv))
            _config.GlobalSlopeValue = gsv / 100.0;
        _config.GlobalSlopeTarget = (GlobalSlopeTargetBox.SelectedItem as ComboBoxItem)?.Tag?.ToString()
                                    ?? "StructuralSlab";
        _config.AlignmentBaseFloorName = (BaseFloorComboBox.SelectedItem as FloorConfig)?.Name ?? string.Empty;

        if (!ValidateBeforeSave(out var validationMessage))
        {
            MessageBox.Show(validationMessage, "配置无效", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _logger.LogInformation("楼层配置编辑完成，待命令层保存，楼层数: {Count}", _config.Floors.Count);

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _logger.LogDebug("楼层配置窗口取消");
        DialogResult = false;
        Close();
    }

    private bool ValidateBeforeSave(out string message)
    {
        if (_config.Floors.Count <= 1)
        {
            message = string.Empty;
            return true;
        }

        if (BaseFloorComboBox.SelectedItem is not FloorConfig baseFloor)
        {
            message = "多楼层模式下必须选择基准层。";
            return false;
        }

        if (!FloorSectionLineTransformer.TryValidateAlignmentPoints(baseFloor.AlignmentPoints, out var baseError))
        {
            message = $"基准层 {baseFloor.Name} 的对齐点无效: {baseError}";
            return false;
        }

        message = string.Empty;
        return true;
    }
}
