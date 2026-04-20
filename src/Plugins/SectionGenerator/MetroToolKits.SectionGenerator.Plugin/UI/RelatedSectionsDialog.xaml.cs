using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.UseCases;

namespace MetroToolKits.SectionGenerator.Plugin.UI;

/// <summary>
/// 关联剖面对话框
/// </summary>
public partial class RelatedSectionsDialog : Window
{
    private readonly IEntityNavigationService _navigationService;
    private readonly ObservableCollection<RelatedSectionViewModel> _items = new();

    public RelatedSectionsDialog(
        string sourceHandle,
        IReadOnlyList<RelatedSectionInfo> sections,
        IEntityNavigationService navigationService)
    {
        InitializeComponent();
        _navigationService = navigationService;

        ResultGrid.ItemsSource = _items;
        foreach (var section in sections)
        {
            _items.Add(new RelatedSectionViewModel(section));
        }

        SummaryText.Text = $"构件 {sourceHandle} 共关联 {_items.Count} 个剖面，双击可跳转。";
    }

    private void LocateSelected_Click(object sender, RoutedEventArgs e)
    {
        LocateSelected();
    }

    private void ResultGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        LocateSelected();
    }

    private void LocateSelected()
    {
        if (ResultGrid.SelectedItem is not RelatedSectionViewModel item)
        {
            MessageBox.Show("请先选择一个剖面。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = _navigationService.FocusBlock(item.BlockHandle);
        if (!result.Success)
        {
            MessageBox.Show(result.ErrorMessage ?? "定位剖面失败。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

public sealed class RelatedSectionViewModel
{
    public RelatedSectionViewModel(RelatedSectionInfo info)
    {
        BlockHandle = info.BlockHandle;
        BlockName = info.BlockName;
        RelatedFloorsText = info.RelatedFloors.Count == 0 ? "-" : string.Join(", ", info.RelatedFloors);
        GeneratedAtText = info.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss");
    }

    public string BlockHandle { get; }
    public string BlockName { get; }
    public string RelatedFloorsText { get; }
    public string GeneratedAtText { get; }
}
