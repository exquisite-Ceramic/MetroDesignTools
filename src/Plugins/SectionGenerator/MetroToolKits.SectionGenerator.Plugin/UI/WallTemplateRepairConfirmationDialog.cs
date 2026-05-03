using System.Windows;
using System.Windows.Controls;

namespace MetroToolKits.SectionGenerator.Plugin.UI;

public sealed class WallTemplateRepairConfirmationDialog : Window
{
    public WallTemplateRepairConfirmationDialog(string message, string title)
    {
        Title = title;
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
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16)
        });

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
            Confirmed = true;
            Close();
        };

        var cancelButton = new Button
        {
            Content = "取消生成",
            MinWidth = 96,
            IsCancel = true
        };
        cancelButton.Click += (_, _) =>
        {
            Confirmed = false;
            Close();
        };

        buttonPanel.Children.Add(confirmButton);
        buttonPanel.Children.Add(cancelButton);
        root.Children.Add(buttonPanel);

        Content = root;
    }

    public bool Confirmed { get; private set; }
}
