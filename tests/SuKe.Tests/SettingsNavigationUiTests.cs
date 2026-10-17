using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SuKe.UI.Shell;

namespace SuKe.Tests;

[Collection("WPF presentation")]
public class SettingsNavigationUiTests
{
    [Fact]
    public void Category_navigation_retargets_vertically_and_keyboard_navigation_is_immediate()
    {
        SmoothScrollUiTests.RunSta(() =>
        {
            var first = new RadioButton { GroupName = "Category", IsChecked = true };
            var second = new RadioButton { GroupName = "Category" };
            var third = new RadioButton { GroupName = "Category" };
            var content = new StackPanel { Width = 1000 };
            var sections = new[] { new StackPanel { Height = 360 }, new StackPanel { Height = 360 }, new StackPanel { Height = 360 } };
            foreach (StackPanel section in sections) content.Children.Add(section);
            var viewer = new ScrollViewer { Content = content, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
            _ = new SettingsSectionNavigator(viewer, new Dictionary<RadioButton, FrameworkElement>
            {
                [first] = sections[0], [second] = sections[1], [third] = sections[2],
            });
            var categories = new StackPanel { Orientation = Orientation.Horizontal };
            categories.Children.Add(first);
            categories.Children.Add(second);
            categories.Children.Add(third);
            var layout = new DockPanel();
            DockPanel.SetDock(categories, Dock.Top);
            layout.Children.Add(categories);
            layout.Children.Add(viewer);
            var window = new Window { Content = layout, Width = 450, Height = 300, ShowInTaskbar = false };
            try
            {
                window.Show();
                SmoothScrollUiTests.Pump(50);
                viewer.ScrollToHorizontalOffset(120);
                SmoothScrollUiTests.Pump(40);
                third.IsChecked = true;
                SmoothScrollUiTests.Pump(35);
                second.IsChecked = true;
                SmoothScrollUiTests.Pump(350);
                Assert.Equal(360, viewer.VerticalOffset, 1);
                Assert.Equal(120, viewer.HorizontalOffset, 1);

                first.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), Environment.TickCount, Key.Left)
                {
                    RoutedEvent = Keyboard.PreviewKeyDownEvent,
                });
                first.IsChecked = true;
                window.UpdateLayout();
                SmoothScrollUiTests.Pump(20);
                Assert.Equal(0, viewer.VerticalOffset);
                Assert.Equal(120, viewer.HorizontalOffset, 1);
            }
            finally { window.Close(); }
        });
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Settings_entry_selects_the_settings_tab_and_restores_the_window(bool hidden, bool minimized)
    {
        SmoothScrollUiTests.RunSta(() =>
        {
            var current = new RadioButton { Name = "NavWorkspaces", Content = "Workspaces", GroupName = "Navigation", IsChecked = true };
            var settings = new RadioButton { Name = "NavSettings", Content = "Settings", GroupName = "Navigation" };
            var panel = new StackPanel();
            panel.Children.Add(current);
            panel.Children.Add(settings);
            var window = new Window { Content = panel, Width = 400, Height = 250, ShowInTaskbar = false };
            NameScope.SetNameScope(window, new NameScope());
            window.RegisterName(current.Name, current);
            window.RegisterName(settings.Name, settings);
            try
            {
                window.Show();
                SmoothScrollUiTests.Pump(40);
                if (hidden) window.Hide();
                if (minimized) window.WindowState = WindowState.Minimized;
                App.ActivateSettingsPage(window);
                SmoothScrollUiTests.Pump(40);
                Assert.True(settings.IsChecked);
                Assert.False(current.IsChecked);
                Assert.True(window.IsVisible);
                Assert.Equal(WindowState.Normal, window.WindowState);
                App.ActivateWorkspacesPage(window);
                SmoothScrollUiTests.Pump(40);
                Assert.True(current.IsChecked);
                Assert.False(settings.IsChecked);
            }
            finally { window.Close(); }
        });
    }
}
