using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SuKe.Localization;
using SuKe.Models;
using SuKe.Services;
using SuKe.UI;
using SuKe.UI.Shell;
using Xunit.Abstractions;

namespace SuKe.Tests;

public class ShellUiTests(ITestOutputHelper output)
{
    [Fact]
    public void Real_wpf_shell_displays_live_screen_previews_and_preserves_workflows_in_isolated_storage()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { VerifyShell(output); }
            catch (Exception ex) { failure = ex; }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(45)), "WPF UI verification timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static void VerifyShell(ITestOutputHelper output)
    {
        using var directory = new TestDirectory();
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        var app = new IsolatedShellApp { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.InitializeComponent();
        Loc.Install();
        var storage = new StorageService(directory.Path);
        string settingsPath = Path.Combine(directory.Path, "settings.json");
        var settings = new SettingsService(settingsPath, storage);
        // Mirrors App.OnStartup: the stored language and theme are applied before the first window.
        Loc.SetLanguage(settings.Settings.UiLanguage);
        ThemeService.Initialize(settings.Settings.ThemeMode, settings);
        var inventory = new FakeWindowInventory();
        var mutation = new RecordingWindowMutation();
        var monitors = new FakeMonitorInventory { Fingerprint = "live", Monitors = [] };
        var nativeMonitors = new MonitorService();
        monitors.Monitors = nativeMonitors.GetCurrentMonitors();
        ScreenPreviewCaptureResult liveCapture = ScreenPreviewCapture.Capture(monitors.Monitors);
        Assert.NotEmpty(monitors.Monitors);
        Assert.Equal(monitors.Monitors.Count, liveCapture.ScreenCount);
        output.WriteLine($"Live desktop capture: {liveCapture.Images.Count}/{liveCapture.ScreenCount} display images available.");
        if (liveCapture.Images.Count == 0)
            output.WriteLine("The test host could not read the interactive desktop; its GDI screen handle is unavailable.");
        foreach (ScreenPreview preview in liveCapture.Images)
        {
            (int colors, int range) = MeasureScreenContent(preview.Png);
            output.WriteLine($"Display {preview.ScreenIndex + 1}: {colors} sampled colors, brightness range {range}.");
            Assert.True(colors > 2 && range > 12,
                $"Display {preview.ScreenIndex + 1} capture contains insufficient visible detail.");
        }
        var service = new WorkspaceService(storage, inventory, mutation, monitors,
            new JumpListService(), settingsService: settings);
        var coordinator = new LayoutCoordinator(nativeMonitors, service,
            new WorkspaceSwitchEngine(new WindowService()));
        SettingsWindow CreateShell() => new(service, storage, coordinator, settings, nativeMonitors, () => { });
        SettingsWindow? shell = null;
        try
        {
            shell = CreateShell();
            shell.Show();
            Pump();
            WindowEnhancementChecks.VerifyPaging(shell);
            WindowEnhancementChecks.VerifyTypography(shell, settings, storage, settingsPath);
            WindowEnhancementChecks.VerifyMainWindowActivation(shell);
            WindowEnhancementChecks.VerifyTrayEntry(app, shell);
            Assert.Equal(Visibility.Visible, Find<FrameworkElement>(shell, "WorkspacesEmpty").Visibility);
            Assert.True(Find<RadioButton>(shell, "NavWorkspaces").Focus());
            Find<RadioButton>(shell, "NavActivity").IsChecked = true;
            Pump();
            var host = Find<ContentControl>(shell, "ActivityPageHost");
            var activity = Assert.IsType<ActivityPage>(host.Content);
            Assert.Equal(Visibility.Visible, Find<Border>(activity, "EmptyCard").Visibility);

            // 测试数据只写入 TestDirectory，不接触真实用户工作区。
            var workspace = new WorkspaceSnapshot
            {
                Name = "Activity", SavedAt = DateTime.UtcNow,
                Monitors = monitors.Monitors.ToList(),
                Entries = monitors.Monitors.SelectMany(monitor => new[]
                {
                    TestEntry("test-editor", monitor, 0.04, 0.04, 0.58, 0.92),
                    TestEntry("test-browser", monitor, 0.61, 0.04, 0.96, 0.92),
                }).ToList(),
            };
            storage.SaveWorkspace(workspace);
            storage.Previews.Save(workspace, liveCapture.Images);
            if (liveCapture.Images.Count > 0)
                ScreenPreviewPagingChecks.VerifyStartup(workspace, storage.Previews, shell);
            Find<RadioButton>(shell, "NavWorkspaces").IsChecked = true;
            Find<RadioButton>(shell, "NavActivity").IsChecked = true;
            Pump();
            var groups = Find<ItemsControl>(activity, "GroupList").Items.Cast<ActivityGroupItem>().ToArray();
            ActivityRowItem row = Assert.Single(Assert.Single(groups).Rows);
            Assert.Contains("Activity", row.Title);
            Assert.Equal("保存", row.CategoryLabel);
            var savedFilter = Find<ToggleButton>(activity, "FilterSaved");
            savedFilter.IsChecked = true;
            savedFilter.IsChecked = false;
            Assert.True(savedFilter.IsChecked);
            Find<ToggleButton>(activity, "FilterRestore").IsChecked = true;
            Assert.Equal(Visibility.Visible, Find<Border>(activity, "EmptyCard").Visibility);
            Find<ToggleButton>(activity, "FilterAll").IsChecked = true;
            var details = ActivityPage.CreateDetailsWindow(row, shell);
            details.Show();
            Pump();
            Assert.Contains("不是完整的保存历史", Text(details));
            details.Close();
            storage.SaveWorkspace(new WorkspaceSnapshot
            {
                Name = "专注工作", SavedAt = DateTime.UtcNow.AddDays(-1),
                Monitors = [workspace.Monitors[0]], Entries = workspace.Entries.Take(2).ToList(),
            });
            activity.RefreshActivity();
            Pump();
            Assert.Equal(2, Find<ItemsControl>(activity, "GroupList").Items.Count);
            shell.Width = 1584;
            shell.Height = 994;
            Pump();

            Find<RadioButton>(shell, "NavSettings").IsChecked = true;
            Pump();
            foreach (string category in new[] { "System", "Appearance", "Browser", "Paths", "Matching", "Restore", "Startup", "Hotkeys", "Monitors" })
            {
                Find<RadioButton>(shell, "Category" + category).IsChecked = true;
                Pump();
            }

            // ── Appearance and language: stored preference, applied in place ──
            Find<RadioButton>(shell, "CategoryAppearance").IsChecked = true;
            Pump();
            var languageCombo = Find<ComboBox>(shell, "InterfaceLanguageCombo");
            var themeCombo = Find<ComboBox>(shell, "ThemeModeCombo");
            Assert.Equal(0, languageCombo.SelectedIndex);
            Assert.Equal(1, themeCombo.SelectedIndex);
            Assert.Equal("简体中文", ((ComboBoxItem)languageCombo.Items[0]).Content);
            Assert.Equal("跟随系统", ((ComboBoxItem)themeCombo.Items[0]).Content);
            Assert.EndsWith("Theme.Light.xaml", PaletteSource(), StringComparison.Ordinal);
            Assert.True(Luminance(shell, "WaCanvasBrush") > 0.8);
            Assert.True(Luminance(shell, "TextFillColorPrimaryBrush") < 0.2);
            Assert.True(Contrast(shell, "TextFillColorPrimaryBrush", "WaCardInnerBrush") >= 7);
            themeCombo.SelectedIndex = 2;
            Pump();
            Assert.EndsWith("Theme.Dark.xaml", PaletteSource(), StringComparison.Ordinal);

            // The delivered dark palette must be readable: light text on dark surfaces.
            Assert.True(Luminance(shell, "WaCanvasBrush") < 0.25, "The dark canvas should be dark.");
            Assert.True(Luminance(shell, "TextFillColorPrimaryBrush") > 0.8,
                "Primary text should be light in the dark theme.");
            Assert.True(Contrast(shell, "TextFillColorPrimaryBrush", "WaCardInnerBrush") >= 7,
                "Primary text must stay legible on a card in the dark theme.");

            themeCombo.SelectedIndex = 1; // Light
            Pump();
            Assert.EndsWith("Theme.Light.xaml", PaletteSource(), StringComparison.Ordinal);
            Assert.Equal(AppThemeMode.Light, new SettingsService(settingsPath, storage).Settings.ThemeMode);
            // The light palette must invert the surface/text relationship, not just tint it.
            Assert.True(Luminance(shell, "WaCanvasBrush") > 0.8, "The light canvas should be light.");
            Assert.True(Luminance(shell, "WaCardInnerBrush") > 0.9, "Light cards should be near-white.");
            Assert.True(Luminance(shell, "TextFillColorPrimaryBrush") < 0.2,
                "Primary text should be dark in the light theme.");
            Assert.True(Contrast(shell, "TextFillColorPrimaryBrush", "WaCardInnerBrush") >= 7,
                "Primary text must stay legible on a card in the light theme.");
            Assert.True(Contrast(shell, "TextFillColorSecondaryBrush", "WaCardInnerBrush") >= 4.5,
                "Secondary text must stay legible on a card in the light theme.");
            Assert.True(Contrast(shell, "TextFillColorTertiaryBrush", "WaCardInnerBrush") >= 4.5,
                "Tertiary text must stay legible on a card in the light theme.");
            themeCombo.SelectedIndex = 2; // back to the designed dark palette
            Pump();
            Assert.EndsWith("Theme.Dark.xaml", PaletteSource(), StringComparison.Ordinal);
            Assert.Equal(AppThemeMode.Dark, new SettingsService(settingsPath, storage).Settings.ThemeMode);
            themeCombo.SelectedIndex = 1;
            Pump();

            languageCombo.SelectedIndex = 1; // English
            Pump();
            Assert.False(Loc.Enabled);
            Assert.Contains("Saved Workspaces", Text(shell));
            Assert.Contains("Appearance & Language", Text(shell));
            Assert.Equal(UiLanguage.English, new SettingsService(settingsPath, storage).Settings.UiLanguage);
            languageCombo.SelectedIndex = 0; // back to Chinese
            Pump();
            Assert.True(Loc.Enabled);
            Assert.Contains("已保存的工作区", Text(shell));
            Assert.Contains("外观与语言", Text(shell));
            Assert.Equal(UiLanguage.ChineseSimplified, new SettingsService(settingsPath, storage).Settings.UiLanguage);

            Find<RadioButton>(shell, "CategorySystem").IsChecked = true;
            Pump();
            foreach (string tooltip in new[] { "更多恢复选项", "其他浏览器与连接管理" })
            {
                var options = Walk(shell).OfType<System.Windows.Controls.Button>()
                    .Single(button => Equals(button.ToolTip, tooltip));
                options.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                Pump();
                Assert.True(options.ContextMenu.IsOpen);
                Assert.NotEmpty(Walk(options.ContextMenu).OfType<FrameworkElement>());
                options.ContextMenu.IsOpen = false;
            }
            Find<ToggleButton>(shell, "NotificationsToggle").IsChecked = false;
            Find<ToggleButton>(shell, "RestorePreviewToggle").IsChecked = false;
            Find<ToggleButton>(shell, "RestoreCheckpointToggle").IsChecked = false;
            Pump();
            Assert.False(new SettingsService(settingsPath, storage).Settings.NotificationsEnabled);
            Assert.False(new SettingsService(settingsPath, storage).Settings.ShowRestorePreview);
            Assert.False(new SettingsService(settingsPath, storage).Settings.CreateRestoreCheckpoints);
            shell.Close();
            shell = CreateShell();
            shell.Show();
            Pump();
            Find<RadioButton>(shell, "NavSettings").IsChecked = true;
            Pump();
            Assert.False(Find<ToggleButton>(shell, "NotificationsToggle").IsChecked);
            Assert.False(Find<ToggleButton>(shell, "RestorePreviewToggle").IsChecked);
            Assert.False(Find<ToggleButton>(shell, "RestoreCheckpointToggle").IsChecked);

            foreach (double width in new[] { 760d, 1040d, 1440d, 1584d })
            {
                shell.Width = width;
                shell.Height = width == 760 ? 520 : width == 1584 ? 994 : 900;
                Pump();
                var content = Walk(shell).OfType<ScrollViewer>()
                    .Single(scroll => System.Windows.Automation.AutomationProperties.GetName(scroll) == "设置内容");
                Assert.True(content.ViewportWidth > 0);
                if (width == 760) Assert.True(content.ScrollableWidth > 0);
                if (width >= 1440) Assert.InRange(content.ScrollableWidth, 0, 1);
                Find<RadioButton>(shell, "NavWorkspaces").IsChecked = true;
                Pump();
                var cards = Find<ItemsControl>(shell, "WorkspacesList");
                Assert.Equal(2, cards.Items.Count);
                Assert.Contains(Walk(cards).OfType<System.Windows.Controls.Button>(), button => Equals(button.Content, "恢复"));
                Assert.Contains(Walk(cards).OfType<System.Windows.Controls.Button>(), button => Equals(button.Content, "屏幕预览"));
                Assert.Contains("Activity", Text(cards));
                WorkspacePreviewControl preview = Walk(cards).OfType<WorkspacePreviewControl>().First();
                Assert.Equal(2, Walk(cards).OfType<WorkspacePreviewControl>().Count());
                Image[] previewImages = Walk(preview).OfType<Image>().ToArray();
                if (liveCapture.Images.Count == 0)
                {
                    Assert.Empty(previewImages);
                    Assert.Contains("此工作区没有可用的屏幕截图", Text(preview));
                }
                else
                {
                    Assert.Equal(liveCapture.Images.Count, previewImages.Length);
                    Assert.All(previewImages, image =>
                    {
                        Assert.True(image.IsVisible);
                        Assert.True(image.ActualWidth >= 400);
                        Assert.True(image.ActualHeight >= 200);
                    });
                    var hide = Walk(preview).OfType<System.Windows.Controls.Button>()
                        .Single(button => Equals(button.Content, "隐藏屏幕截图"));
                    hide.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                    Pump();
                    Assert.All(Walk(preview).OfType<Image>(), image => Assert.False(image.IsVisible));
                    Walk(preview).OfType<System.Windows.Controls.Button>()
                        .Single(button => Equals(button.Content, "显示屏幕截图"))
                        .RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                    Pump();
                    Assert.Equal(liveCapture.Images.Count, Walk(preview).OfType<Image>().Count(image => image.IsVisible));

                    // 单击缩略图即放大：不再需要双击，也不触发卡片上的恢复动作。
                    int clickedIndex = 0;
                    foreach (Border frame in Walk(preview).OfType<Border>()
                        .Where(border => border.ToolTip is "点击截图即可放大查看。"))
                    {
                        MouseDownAndInspectDialog<ScreenPreviewViewerWindow>(frame, viewer =>
                        {
                            Assert.False(string.IsNullOrWhiteSpace(viewer.Title));
                            Assert.Single(Walk(viewer).OfType<Image>());
                            Assert.Contains("实际大小", Text(viewer));
                            Assert.Contains("关闭", Text(viewer));
                            ScreenPreviewPagingChecks.Verify(viewer, liveCapture.Images.Count, clickedIndex);
                        });
                        clickedIndex++;
                    }
                }
                var restoreButton = Walk(cards).OfType<System.Windows.Controls.Button>()
                    .First(button => Equals(button.Content, "恢复"));
                Assert.Contains(Walk(restoreButton).OfType<TextBlock>(),
                    text => text.Text == "恢复" && text.Foreground is SolidColorBrush brush && brush.Color == Colors.White);
                var screenPreview = Walk(cards).OfType<System.Windows.Controls.Button>()
                    .First(button => Equals(button.Content, "屏幕预览"));
                ClickAndInspectDialog<WorkspaceScreenPreviewDialog>(screenPreview, dialog =>
                {
                    if (liveCapture.Images.Count == 0)
                        Assert.Contains("此工作区没有可用的屏幕截图。", Text(dialog));
                    else
                    {
                        Image[] thumbnails = Walk(dialog).OfType<Image>().ToArray();
                        Assert.Equal(liveCapture.Images.Count, thumbnails.Length);
                        for (int index = 0; index < thumbnails.Length; index++)
                        {
                            int selected = index;
                            MouseDownAndInspectDialog<ScreenPreviewViewerWindow>(thumbnails[index],
                                viewer => ScreenPreviewPagingChecks.Verify(viewer, thumbnails.Length, selected));
                        }
                    }
                });
                Find<RadioButton>(shell, "NavSettings").IsChecked = true;
            }
            Find<RadioButton>(shell, "NavWorkspaces").IsChecked = true;
            Pump();
            var tools = Walk(shell).OfType<System.Windows.Controls.Button>()
                .Single(button => Equals(button.ToolTip, "工作区操作"));
            tools.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Pump();
            Assert.True(tools.ContextMenu.IsOpen);
            var selectAll = Walk(tools.ContextMenu).OfType<CheckBox>().Single();
            selectAll.IsChecked = true;
            Pump();
            Assert.Equal(Visibility.Visible, Find<Border>(shell, "BulkActionBar").Visibility);
            selectAll.IsChecked = false;
            tools.ContextMenu.IsOpen = false;
            Assert.Equal(Visibility.Collapsed, Find<Border>(shell, "BulkActionBar").Visibility);
            settings.Settings.ShowRestorePreview = true;
            var restore = Walk(shell).OfType<System.Windows.Controls.Button>()
                .First(button => Equals(button.Content, "恢复"));
            ClickAndCancel<RestorePlanPreviewDialog>(restore);
            var save = Walk(shell).OfType<System.Windows.Controls.Button>()
                .Single(button => Equals(button.Content, "保存当前工作区"));
            ClickAndCancel<SaveWorkspaceDialog>(save);
            Assert.Empty(mutation.Restores);
        }
        finally
        {
            shell?.Close();
            coordinator.DisposeAsync().AsTask().GetAwaiter().GetResult();
            service.DisposeAsync().AsTask().GetAwaiter().GetResult();
            app.Shutdown();
        }
    }

    private static T Find<T>(FrameworkElement root, string name) where T : FrameworkElement =>
        Assert.IsAssignableFrom<T>(root.FindName(name));

    /// <summary>Relative luminance of a brush resolved through the window's current palette.</summary>
    /// <remarks>Surfaces are gradients, so a gradient is averaged across its stops.</remarks>
    private static double Luminance(FrameworkElement element, string resourceKey) =>
        (element.FindResource(resourceKey)) switch
        {
            SolidColorBrush solid => RelativeLuminance(solid.Color),
            GradientBrush gradient => gradient.GradientStops
                .Select(stop => RelativeLuminance(stop.Color))
                .Average(),
            _ => throw new InvalidOperationException($"'{resourceKey}' is not a colour brush.")
        };

    /// <summary>WCAG contrast ratio between two palette brushes.</summary>
    private static double Contrast(FrameworkElement element, string foreground, string background)
    {
        double first = Luminance(element, foreground);
        double second = Luminance(element, background);
        double lighter = Math.Max(first, second);
        double darker = Math.Min(first, second);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance(Color color)
    {
        static double Channel(byte value)
        {
            double normalized = value / 255d;
            return normalized <= 0.03928
                ? normalized / 12.92
                : Math.Pow((normalized + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
    }

    /// <summary>
    /// The palette currently merged into the application. The palette is application-scoped so that
    /// styles defined in shared dictionaries re-resolve their brushes when the theme changes.
    /// </summary>
    private static string PaletteSource() =>
        ThemeService.AppliedPaletteSource?.OriginalString ?? "";

    private static IEnumerable<DependencyObject> Walk(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (DependencyObject descendant in Walk(child)) yield return descendant;
        }
    }

    private static string Text(DependencyObject root) => string.Join("\n", Walk(root).OfType<TextBlock>().Select(t => t.Text));

    private static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

    private static void ClickAndCancel<T>(System.Windows.Controls.Button button) where T : Window
    {
        bool opened = false;
        var timeout = System.Diagnostics.Stopwatch.StartNew();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        timer.Tick += (_, _) =>
        {
            T? dialog = Application.Current.Windows.OfType<T>().FirstOrDefault();
            if (dialog is not null)
            {
                opened = true;
                dialog.Close();
                timer.Stop();
            }
            else if (timeout.Elapsed > TimeSpan.FromSeconds(5))
            {
                timer.Stop();
                foreach (Window popup in Application.Current.Windows.Cast<Window>().Where(w => w.Owner is not null).ToArray())
                    popup.Close();
            }
        };
        timer.Start();
        try
        {
            button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            while (!opened && timeout.Elapsed < TimeSpan.FromSeconds(6)) Pump();
            Assert.True(opened, $"The existing {typeof(T).Name} workflow did not open.");
        }
        finally { timer.Stop(); }
    }

    private static void InspectDialogOpenedBy<T>(Action open, Action<T> inspect, string description)
        where T : Window
    {
        Exception? failure = null;
        bool opened = false;
        var timeout = System.Diagnostics.Stopwatch.StartNew();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        timer.Tick += (_, _) =>
        {
            T? dialog = Application.Current.Windows.OfType<T>().FirstOrDefault();
            if (dialog is not null)
            {
                opened = true;
                try { inspect(dialog); }
                catch (Exception ex) { failure = ex; }
                finally { dialog.Close(); timer.Stop(); }
            }
            else if (timeout.Elapsed > TimeSpan.FromSeconds(5))
            {
                timer.Stop();
                foreach (Window popup in Application.Current.Windows.Cast<Window>().Where(w => w.Owner is not null).ToArray())
                    popup.Close();
            }
        };
        timer.Start();
        try
        {
            open();
            while (!opened && timeout.Elapsed < TimeSpan.FromSeconds(6)) Pump();
            Assert.True(opened, $"The {description} did not open.");
            if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
        }
        finally { timer.Stop(); }
    }

    private static void ClickAndInspectDialog<T>(System.Windows.Controls.Button button, Action<T> inspect)
        where T : Window =>
        InspectDialogOpenedBy<T>(
            () => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)),
            inspect,
            $"{typeof(T).Name} preview dialog");

    /// <summary>
    /// Raises a real primary-button press on an element. Used for the preview thumbnails, which open
    /// the enlarged viewer from a single click rather than a button click.
    /// </summary>
    private static void MouseDownAndInspectDialog<T>(UIElement target, Action<T> inspect)
        where T : Window =>
        InspectDialogOpenedBy<T>(
            () => target.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
            {
                RoutedEvent = UIElement.MouseLeftButtonDownEvent,
                Source = target
            }),
            inspect,
            $"{typeof(T).Name} enlarged preview");

    private static WorkspaceEntry TestEntry(string process, MonitorInfo monitor,
        double left, double top, double right, double bottom)
    {
        int width = monitor.BoundsRight - monitor.BoundsLeft;
        int height = monitor.BoundsBottom - monitor.BoundsTop;
        return new WorkspaceEntry
        {
            ProcessName = process,
            MonitorId = monitor.MonitorId,
            Position = new WindowRecord
            {
                NormalLeft = monitor.BoundsLeft + (int)(width * left),
                NormalTop = monitor.BoundsTop + (int)(height * top),
                NormalRight = monitor.BoundsLeft + (int)(width * right),
                NormalBottom = monitor.BoundsTop + (int)(height * bottom),
            },
        };
    }

    private static (int Colors, int BrightnessRange) MeasureScreenContent(byte[] png)
    {
        using var stream = new MemoryStream(png, writable: false);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.DecodePixelWidth = 32;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        var pixels = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
        pixels.Freeze();
        int stride = pixels.PixelWidth * 4;
        byte[] buffer = new byte[stride * pixels.PixelHeight];
        pixels.CopyPixels(buffer, stride, 0);
        var colors = new HashSet<int>();
        byte darkest = byte.MaxValue;
        byte brightest = byte.MinValue;
        for (int i = 0; i < buffer.Length; i += 4)
        {
            byte brightness = (byte)((buffer[i] + buffer[i + 1] + buffer[i + 2]) / 3);
            darkest = Math.Min(darkest, brightness);
            brightest = Math.Max(brightest, brightness);
            colors.Add((buffer[i] << 16) | (buffer[i + 1] << 8) | buffer[i + 2]);
        }
        return (colors.Count, brightest - darkest);
    }
}
