using MetroToolKits.Foundation.Core.Logging;
using System.Collections.ObjectModel;
using System.Windows;
using Microsoft.Extensions.Logging;
using MetroToolKits.Foundation.Core.Diagnostics;
using MetroToolKits.SectionGenerator.App.UseCases;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.Plugin.UI;

/// <summary>
/// 剖面更新检查对话框（非模态）
/// </summary>
public partial class SectionUpdateDialog : Window
{
    private readonly ICheckSectionUpdatesUseCase _checkUseCase;
    private readonly IUpdateSectionUseCase _updateUseCase;
    private readonly ILogger<SectionUpdateDialog> _logger;
    private readonly IUserLogger _userLogger;
    private readonly ObservableCollection<SectionResultViewModel> _items = new();

    public SectionUpdateDialog(
        CheckSectionUpdatesResult initialResult,
        ICheckSectionUpdatesUseCase checkUseCase,
        IUpdateSectionUseCase updateUseCase,
        ILogger<SectionUpdateDialog> logger,
        IUserLogger userLogger)
    {
        InitializeComponent();
        _checkUseCase  = checkUseCase;
        _updateUseCase = updateUseCase;
        _logger        = logger;
        _userLogger    = userLogger;

        ResultGrid.ItemsSource = _items;
        ApplyResult(initialResult);
    }

    private void RunCheck()
    {
        _userLogger.CheckingUpdates();
        ApplyResult(_checkUseCase.Execute());
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RunCheck();

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in _items) item.IsSelected = true;
    }

    private void SelectNone_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in _items) item.IsSelected = false;
    }

    private void UpdateSelected_Click(object sender, RoutedEventArgs e)
    {
        var selected = _items.Where(i => i.IsSelected).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show("请先选择要更新的剖面。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        RunBatchUpdate(selected);
    }

    private void UpdateAll_Click(object sender, RoutedEventArgs e)
    {
        var outdated = _items.Where(i => i.Status == SectionUpdateStatus.Outdated).ToList();
        if (outdated.Count == 0)
        {
            MessageBox.Show("所有剖面均为最新状态。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        RunBatchUpdate(outdated);
    }

    private void RunBatchUpdate(IList<SectionResultViewModel> targets)
    {
        _userLogger.BatchUpdateStarted(targets.Count);
        _logger.LogInformation("批量更新 {Count} 个剖面", targets.Count);

        int success = 0, failed = 0;
        foreach (var item in targets)
        {
            var result = _updateUseCase.Execute(new UpdateSectionRequest
            {
                BlockHandle = item.BlockHandle
            });

            if (result.Success)
            {
                success++;
                item.StatusText = "已更新";
            }
            else
            {
                failed++;
                item.StatusText = $"失败: {result.ErrorMessage}";
                _logger.LogWarning("更新剖面 {BlockName} 失败: {Error}", item.BlockName, result.ErrorMessage);
            }
        }

        _userLogger.BatchUpdateCompleted(success, failed);
        _logger.LogInformation("批量更新完成，成功 {Success}，失败 {Failed}", success, failed);

        // 刷新列表
        RunCheck();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void ApplyResult(CheckSectionUpdatesResult result)
    {
        _items.Clear();

        if (result.Status == OperationStatus.Failed)
        {
            SummaryText.Text = $"检测失败: {result.Failure?.UserMessage ?? "未知错误"}";
            MessageBox.Show(
                result.Failure?.UserMessage ?? "变更检测失败",
                "变更检测失败",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        foreach (var item in result.Items)
        {
            _items.Add(new SectionResultViewModel(item));
        }

        var outdated = result.Items.Count(r => r.Status == SectionUpdateStatus.Outdated);
        var partial = result.Items.Count(r => r.SkippedFloors.Count > 0);
        SummaryText.Text = result.Status == OperationStatus.PartialSuccess
            ? $"共 {result.Items.Count} 个剖面，其中 {outdated} 个需要更新，{partial} 个为部分检查"
            : $"共 {result.Items.Count} 个剖面，其中 {outdated} 个需要更新";
        _userLogger.CheckResult(result.Items.Count, outdated);
    }
}

/// <summary>
/// 剖面检测结果视图模型
/// </summary>
public sealed class SectionResultViewModel
{
    private readonly SectionCheckResult _result;

    public SectionResultViewModel(SectionCheckResult result)
    {
        _result    = result;
        IsSelected = result.Status == SectionUpdateStatus.Outdated;
        StatusText = result.Status switch
        {
            SectionUpdateStatus.UpToDate when result.SkippedFloors.Count > 0 => "△ 部分检查",
            SectionUpdateStatus.UpToDate => "✓ 最新",
            SectionUpdateStatus.Outdated when result.SkippedFloors.Count > 0 => "⚠ 需更新（部分检查）",
            SectionUpdateStatus.Outdated => "⚠ 需更新",
            _                            => "? 未知"
        };
    }

    public bool   IsSelected        { get; set; }
    public string BlockHandle        => _result.BlockHandle;
    public string BlockName          => _result.BlockName;
    public string StatusText         { get; set; }
    public SectionUpdateStatus Status => _result.Status;
    public string OutdatedFloorsText  =>
        _result.OutdatedFloors.Count > 0
            ? string.Join(", ", _result.OutdatedFloors)
            : "-";
    public string SkippedFloorsText =>
        _result.SkippedFloors.Count > 0
            ? string.Join(", ", _result.SkippedFloors)
            : "-";
}
