using System.Windows;
using System.Windows.Controls;
using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.SectionGenerator.App.Models;

namespace MetroToolKits.SectionGenerator.Plugin.UI;

public sealed class WallTemplateBindingDialog : Window
{
    private readonly ComboBox _templateComboBox = new();

    public WallTemplateBindingDialog(
        IReadOnlyList<ConvertedElementMetadataIssue> issues,
        IReadOnlyList<WallAssemblyTemplate> templates)
    {
        Title = "检测到复制墙体缺少模板信息";
        Width = 560;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;

        var root = new StackPanel
        {
            Margin = new Thickness(18)
        };

        root.Children.Add(new TextBlock
        {
            Text = $"检测到 {issues.Count} 个墙体缺少模板信息，但无法从同图层唯一推断模板。",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        });

        root.Children.Add(new TextBlock
        {
            Text = "请选择要绑定的墙体模板。确认后只会给这些复制实体写入 TemplateId 和 ConvertedType，不会修改模板定义。",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 14)
        });

        _templateComboBox.ItemsSource = templates
            .Select(static template => new WallTemplateSelectionItem(template))
            .ToList();
        _templateComboBox.DisplayMemberPath = nameof(WallTemplateSelectionItem.DisplayText);
        _templateComboBox.SelectedIndex = 0;
        _templateComboBox.Margin = new Thickness(0, 0, 0, 14);
        root.Children.Add(_templateComboBox);

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var confirmButton = new Button
        {
            Content = "绑定并继续",
            MinWidth = 96,
            Margin = new Thickness(0, 0, 8, 0),
            IsDefault = true
        };
        confirmButton.Click += (_, _) =>
        {
            if (_templateComboBox.SelectedItem is WallTemplateSelectionItem item)
            {
                SelectedTemplate = item.Template;
                DialogResult = true;
                Close();
            }
        };

        var cancelButton = new Button
        {
            Content = "取消生成",
            MinWidth = 96,
            IsCancel = true
        };
        cancelButton.Click += (_, _) =>
        {
            DialogResult = false;
            Close();
        };

        buttonPanel.Children.Add(confirmButton);
        buttonPanel.Children.Add(cancelButton);
        root.Children.Add(buttonPanel);

        Content = root;
    }

    public WallAssemblyTemplate? SelectedTemplate { get; private set; }

    private sealed class WallTemplateSelectionItem
    {
        public WallTemplateSelectionItem(WallAssemblyTemplate template)
        {
            Template = template;
        }

        public WallAssemblyTemplate Template { get; }

        public string DisplayText => $"{Template.TemplateName} ({Template.TemplateId})";
    }
}
