using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using SuKe.Localization;

namespace SuKe.UI;

public partial class SettingsWindow
{
    private ComboBox? _fontScaleCombo;

    private void InitialiseFontSizeUI()
    {
        TypographyService.Initialize(_settingsService.Settings.UiFontScale);
        if (_fontScaleCombo is not null || FindName("SectionAppearance") is not StackPanel section) return;

        var row = new Grid { Margin = new Thickness(18, 16, 18, 12) };
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var text = new StackPanel { Margin = new Thickness(0, 0, 18, 0), VerticalAlignment = VerticalAlignment.Center };
        var title = new TextBlock { Text = Loc.T("Text size"), FontSize = 16, FontWeight = FontWeights.SemiBold };
        title.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
        text.Children.Add(title);
        var hint = new TextBlock
        {
            Text = Loc.T("Applies to text in all windows and menus. Saved automatically."),
            FontSize = 14, TextWrapping = TextWrapping.Wrap
        };
        hint.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        text.Children.Add(hint);
        row.Children.Add(text);
        _fontScaleCombo = new ComboBox { MinWidth = 150, VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetAutomationId(_fontScaleCombo, "InterfaceFontScale");
        AutomationProperties.SetName(_fontScaleCombo, Loc.T("Text size"));
        foreach (double value in new[] { 0.8, 0.9, 1, 1.1, 1.25, 1.5, _settingsService.Settings.UiFontScale }.Distinct().Order())
        {
            var item = new ComboBoxItem { Content = $"{value:P0}", Tag = value };
            _fontScaleCombo.Items.Add(item);
            if (value == _settingsService.Settings.UiFontScale) _fontScaleCombo.SelectedItem = item;
        }
        _fontScaleCombo.SelectionChanged += (_, _) =>
        {
            if (_fontScaleCombo.SelectedItem is not ComboBoxItem { Tag: double scale }) return;
            _settingsService.Settings.UiFontScale = scale;
            _settingsService.Save();
            TypographyService.SetScale(scale);
        };
        Grid.SetColumn(_fontScaleCombo, 1);
        row.Children.Add(_fontScaleCombo);
        section.Children.Add(row);
    }
}
