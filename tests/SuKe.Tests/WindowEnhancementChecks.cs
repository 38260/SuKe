using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using SuKe.UI;
using SuKe.Services;

namespace SuKe.Tests;

internal sealed class IsolatedShellApp : App
{
    // 只加载真实 XAML 与事件绑定，不运行真实存储、热键或启动恢复。
    protected override void OnStartup(StartupEventArgs e) { }
}

internal static class WindowEnhancementChecks
{
    internal static void VerifyTrayEntry(App app, SettingsWindow shell)
    {
        using var tray = (H.NotifyIcon.TaskbarIcon)app.FindResource("TrayIcon");
        var item = tray.ContextMenu.Items.OfType<MenuItem>()
            .Single(menuItem => Equals(menuItem.Header, "Open Main Window"));
        Localization.Loc.Apply(tray.ContextMenu);
        Assert.Equal(Localization.Loc.T("Open Main Window"), item.Header);
        var field = typeof(App).GetField("_settingsWindow", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        field.SetValue(app, shell);
        try
        {
            shell.WindowState = WindowState.Minimized;
            Pump();
            item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Pump();
            Assert.True(shell.IsVisible);
            Assert.Equal(WindowState.Normal, shell.WindowState);
            shell.Hide();
            item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Pump();
            Assert.True(shell.IsVisible);
            Assert.Same(shell, field.GetValue(app));
        }
        finally { field.SetValue(app, null); }
    }

    internal static void VerifyMainWindowActivation(Window shell)
    {
        shell.WindowState = WindowState.Minimized;
        Pump();
        App.ActivateMainWindow(shell);
        Pump();
        Assert.True(shell.IsVisible);
        Assert.Equal(WindowState.Normal, shell.WindowState);
        shell.Hide();
        App.ActivateMainWindow(shell);
        Pump();
        Assert.True(shell.IsVisible);
        shell.WindowState = WindowState.Maximized;
        App.ActivateMainWindow(shell);
        Pump();
        Assert.Equal(WindowState.Maximized, shell.WindowState);
        shell.WindowState = WindowState.Normal;
        Pump();
    }

    internal static void VerifyTypography(Window shell, SettingsService settings, StorageService storage, string settingsPath)
    {
        ((RadioButton)shell.FindName("NavSettings")).IsChecked = true;
        ((RadioButton)shell.FindName("CategoryAppearance")).IsChecked = true;
        Pump();
        var combo = Find<ComboBox>(shell, "InterfaceFontScale");
        var text = Walk(shell).OfType<TextBlock>().First(block => block.FontSize == 21);
        var icon = Walk(shell).OfType<Wpf.Ui.Controls.SymbolIcon>().First();
        double iconSize = icon.FontSize;
        foreach (double scale in new[] { 1.5, 0.8, 1.25, 1d })
        {
            combo.SelectedItem = combo.Items.OfType<ComboBoxItem>().Single(item => Equals(item.Tag, scale));
            Pump();
            Assert.Equal(21 * TypographyService.Scale, text.FontSize, 5);
            Assert.Equal(iconSize, icon.FontSize);
            Assert.Equal(scale, new SettingsService(settingsPath, storage).Settings.UiFontScale);
        }
        combo.SelectedItem = combo.Items.OfType<ComboBoxItem>().Single(item => Equals(item.Tag, 1.25));
        Pump();
        var inherited = new TextBlock { Text = "Inherited" };
        var heading = new TextBlock { Text = "Heading", FontSize = 22 };
        var panel = new StackPanel();
        panel.Children.Add(inherited);
        panel.Children.Add(heading);
        var dialog = new Window { Content = panel, Width = 400, Height = 200, Owner = shell, FontSize = 14 };
        try
        {
            dialog.Show();
            Pump();
            Assert.True(Math.Abs(inherited.FontSize - 14 * TypographyService.Scale) < 0.00001,
                $"Scale={TypographyService.Scale}; window={dialog.FontSize}; inherited={inherited.FontSize}; heading={heading.FontSize}; source={DependencyPropertyHelper.GetValueSource(inherited, TextBlock.FontSizeProperty).BaseValueSource}");
            Assert.Equal(22 * TypographyService.Scale, heading.FontSize, 5);
            TypographyService.Apply(dialog);
            TypographyService.Apply(dialog);
            Assert.Equal(14 * TypographyService.Scale, inherited.FontSize, 5);
            Assert.Equal(22 * TypographyService.Scale, heading.FontSize, 5);
            TypographyService.SetScale(1);
            TypographyService.Initialize(new SettingsService(settingsPath, storage).Settings.UiFontScale);
            Assert.Equal(22 * TypographyService.Scale, heading.FontSize, 5);

            var menu = new ContextMenu();
            var item = new MenuItem { Header = "Menu", FontSize = 14 };
            menu.Items.Add(item);
            TypographyService.Apply(menu);
            Assert.Equal(14 * TypographyService.Scale, item.FontSize, 5);
        }
        finally
        {
            dialog.Close();
            combo.SelectedItem = combo.Items.OfType<ComboBoxItem>().Single(item => Equals(item.Tag, 1d));
            TypographyService.SetScale(1);
        }
        Assert.Equal(1, settings.Settings.UiFontScale);
        ((RadioButton)shell.FindName("NavWorkspaces")).IsChecked = true;
        Pump();
    }

    internal static void VerifyPaging(Window owner)
    {
        var pages = new[] { Colors.Red, Colors.Green, Colors.Blue }
            .Select((color, index) => new ScreenPreviewPage(
                new DrawingImage(new GeometryDrawing(new SolidColorBrush(color), null,
                    new RectangleGeometry(new Rect(0, 0, 2600, 1600)))), $"Screen {index + 1}", null))
            .ToArray();
        var viewer = new ScreenPreviewViewerWindow(pages, 1, owner);
        try
        {
            viewer.Show();
            ScreenPreviewPagingChecks.Verify(viewer, pages.Length, 1);
            var previous = Find<Button>(viewer, "ScreenPreviewPrevious");
            var next = Find<Button>(viewer, "ScreenPreviewNext");
            previous.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Pump();
            Assert.True(previous.IsEnabled && next.IsEnabled);
            var zoom = Walk(viewer).OfType<Button>()
                .Single(button => Equals(button.Content, Localization.Loc.T("Actual size")));
            zoom.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Pump();
            var image = Walk(viewer).OfType<Image>().Single();
            var scroll = Walk(viewer).OfType<ScrollViewer>().Single();
            Assert.Equal(Stretch.None, image.Stretch);
            Assert.True(Bounds(previous, viewer).Right <= Bounds(scroll, viewer).Left);
            Assert.True(Bounds(next, viewer).Left >= Bounds(scroll, viewer).Right);
            scroll.ScrollToBottom();
            scroll.ScrollToRightEnd();
            previous.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Pump();
            Assert.Equal(0, scroll.VerticalOffset);
            Assert.Equal(0, scroll.HorizontalOffset);
            Assert.Equal(WindowState.Maximized, viewer.WindowState);
        }
        finally { viewer.Close(); }
    }

    private static Rect Bounds(FrameworkElement element, Visual ancestor) =>
        element.TransformToAncestor(ancestor).TransformBounds(new Rect(element.RenderSize));

    private static T Find<T>(DependencyObject root, string id) where T : DependencyObject =>
        Walk(root).OfType<T>().Single(item => AutomationProperties.GetAutomationId(item) == id);

    private static IEnumerable<DependencyObject> Walk(DependencyObject root)
    {
        yield return root;
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            foreach (DependencyObject child in Walk(VisualTreeHelper.GetChild(root, index)))
                yield return child;
    }

    private static void Pump() => Dispatcher.CurrentDispatcher.Invoke(
        () => { }, DispatcherPriority.ApplicationIdle);
}
